"""Private stdin/stdout JSON protocol. No HTTP listener, text logs, or remote inference."""
import argparse
import contextlib
import json
import os
from pathlib import Path
import queue
import sys
import threading
import time
import uuid

import speech
from text import chunks

PROTOCOL_OUT = sys.stdout
PRINT_LOCK = threading.Lock()


def emit(event: str, **payload):
    with PRINT_LOCK:
        PROTOCOL_OUT.write(json.dumps({"type": event, **payload}, ensure_ascii=False) + "\n")
        PROTOCOL_OUT.flush()


class Worker:
    def __init__(self, audio_dir: Path):
        self.audio_dir = audio_dir
        self.jobs = queue.Queue()
        self.lock = threading.Lock()
        self.current = None
        self.closed = threading.Event()
        self.ready = threading.Event()
        self.speech = speech.Speech()
        self.failed = False

    def cancel(self, request_id=None):
        with self.lock:
            if self.current and (request_id is None or self.current[0] == request_id):
                self.current[1].set()

    def submit(self, request):
        text = request.get("text", "")
        parts = chunks(text)  # Validate before replacing a working request.
        request_id = str(request.get("id", ""))
        if not request_id or len(request_id) > 80:
            raise ValueError("Invalid reading request.")
        with self.lock:
            if self.current:
                self.current[1].set()
            cancelled = threading.Event()
            self.current = (request_id, cancelled)
            # Passage lengths let the app buffer enough audio to play the rest without gaps.
            emit("plan", id=request_id, characters=[len(part) for part in parts])
            self.jobs.put((request, parts, cancelled))

    def run(self):
        try:
            emit("loading", message=f"Loading {speech.NAME}. The first launch downloads the model.")
            # All package output is redirected so the parent receives JSON only.
            with contextlib.redirect_stdout(sys.stderr):
                device = self.speech.load(lambda message: emit("loading", message=message))
            emit("ready", device=device)
            self.ready.set()
        except Exception as error:
            self.failed = True
            emit("error", message=f"Could not load {speech.NAME}: {type(error).__name__}: {error}")
            return

        import soundfile as sf

        while not self.closed.is_set():
            job = self.jobs.get()
            if job is None:
                break
            request, parts, cancelled = job
            request_id = str(request["id"])
            if cancelled.is_set():
                continue
            try:
                for index, part in enumerate(parts):
                    if cancelled.is_set():
                        break
                    emit("generating", id=request_id, index=index, total=len(parts), characters=len(part))
                    started = time.monotonic()
                    with contextlib.redirect_stdout(sys.stderr):
                        samples, sample_rate = self.speech.synthesize(part, request.get("voice", speech.DEFAULT_VOICE))
                    if cancelled.is_set():
                        break
                    # Files are named independently of user text and request identifiers.
                    path = self.audio_dir / f"{uuid.uuid4().hex}.wav"
                    sf.write(path, samples, sample_rate, subtype="PCM_16")
                    emit("audio", id=request_id, path=str(path), index=index,
                         total=len(parts), seconds=len(samples) / sample_rate,
                         generation_seconds=round(time.monotonic() - started, 2))
                emit("cancelled" if cancelled.is_set() else "done", id=request_id)
            except Exception as error:
                if not cancelled.is_set():
                    emit("error", id=request_id, message=f"Speech generation failed: {type(error).__name__}: {error}")
            finally:
                with self.lock:
                    if self.current and self.current[0] == request_id:
                        self.current = None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--audio-dir", type=Path, required=True)
    args = parser.parse_args()
    if os.environ.get('LAZY_READ_DEBUG_LOAD') == '1':
        import faulthandler
        faulthandler.dump_traceback_later(90, repeat=True)
    args.audio_dir.mkdir(parents=True, exist_ok=True)
    os.environ.setdefault("HF_HUB_DISABLE_TELEMETRY", "1")
    emit("loading", message="Starting the local speech libraries…")
    try:
        speech.preload()
        if os.environ.get('LAZY_READ_DEBUG_LOAD') == '1':
            faulthandler.cancel_dump_traceback_later()
    except Exception as error:
        emit("error", message=f"Speech library initialization failed: {type(error).__name__}: {error}")
        return
    worker = Worker(args.audio_dir)
    thread = threading.Thread(target=worker.run, daemon=True)
    thread.start()
    try:
        for line in sys.stdin:
            request = {}
            try:
                request = json.loads(line)
                if not isinstance(request, dict):
                    raise ValueError("Engine requests must be JSON objects.")
                action = request.get("action")
                if action == "speak":
                    if worker.failed:
                        raise RuntimeError("The model failed to load. Restart the engine.")
                    worker.submit(request)
                elif action == "cancel":
                    worker.cancel(request.get("id"))
                elif action == "shutdown":
                    break
                else:
                    raise ValueError("Unknown engine action.")
            except Exception as error:
                emit("error", id=request.get("id", "") if isinstance(request, dict) else "",
                     message=str(error))
    finally:
        worker.cancel()
        worker.closed.set()
        worker.jobs.put(None)
        thread.join(timeout=2)


if __name__ == "__main__":
    main()
