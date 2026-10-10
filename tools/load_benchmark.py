"""Time how long Kokoro takes to load, using the same worker process the app starts for every reading.

Usage: .venv\\Scripts\\python.exe tools\\load_benchmark.py [rounds]
"Ready" is the time from starting the worker until the model is in memory and able to speak.
"""
import json
import os
import statistics
import subprocess
import sys
import tempfile
import threading
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PYTHON = ROOT / ".venv" / "Scripts" / "python.exe"
SENTENCE = "This is a short synthetic sentence used only to time the speech engine."


def peak_memory_gib(pid: int) -> float:
    try:
        import psutil
        # A venv python.exe is a launcher; the interpreter holding the model is its child.
        launcher = psutil.Process(pid)
        infos = [process.memory_info() for process in (launcher, *launcher.children(recursive=True))]
        return sum(getattr(info, "peak_wset", info.rss) for info in infos) / 2**30
    except Exception:
        return float("nan")


def run() -> dict:
    audio = tempfile.mkdtemp(prefix="lazyread-bench-")
    started = time.monotonic()
    process = subprocess.Popen([str(PYTHON), "-u", str(ROOT / "engine" / "worker.py"), "--audio-dir", audio],
                               stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                               text=True, encoding="utf-8", cwd=ROOT,
                               env={**os.environ, "PYTHONUTF8": "1", "HF_HUB_DISABLE_TELEMETRY": "1"})
    events: dict = {}

    def pump():
        for line in process.stdout:
            try:
                message = json.loads(line)
            except ValueError:
                continue
            events.setdefault(message.get("type"), (time.monotonic() - started, message))

    threading.Thread(target=pump, daemon=True).start()

    def wait(name, timeout=600):
        end = time.monotonic() + timeout
        while name not in events:
            if "error" in events or time.monotonic() > end or process.poll() is not None:
                raise RuntimeError(f"no '{name}' event; got {events.get('error', list(events))}")
            time.sleep(0.005)
        return events[name]

    try:
        ready, ready_message = wait("ready")
        spoken = time.monotonic()
        process.stdin.write(json.dumps({"action": "speak", "id": "bench", "text": SENTENCE, "voice": "af_heart"}) + "\n")
        process.stdin.flush()
        _, audio_message = wait("audio")
        return {"ready": ready, "first_audio": time.monotonic() - spoken, "audio_seconds": audio_message["seconds"],
                "device": ready_message.get("device"), "ram_gib": peak_memory_gib(process.pid)}
    finally:
        try:
            process.stdin.write(json.dumps({"action": "shutdown"}) + "\n")
            process.stdin.flush()
            process.wait(timeout=20)
        except (OSError, subprocess.TimeoutExpired):
            process.kill()
        for clip in Path(audio).glob("*.wav"):
            clip.unlink()
        Path(audio).rmdir()


def main():
    rounds = int(sys.argv[1]) if len(sys.argv) > 1 else 5
    print("warm-up: downloads and fills the disk cache; not counted", flush=True)
    run()
    runs = []
    for index in range(rounds):
        result = run()
        runs.append(result)
        print(f"round {index + 1}: ready {result['ready']:.2f}s, first audio {result['first_audio']:.2f}s "
              f"for {result['audio_seconds']:.1f}s of speech, peak RAM {result['ram_gib']:.2f} GiB, {result['device']}", flush=True)
    ready = [r["ready"] for r in runs]
    print(f"\nKokoro: load median {statistics.median(ready):.2f}s (range {min(ready):.2f}-{max(ready):.2f}), "
          f"first sentence median {statistics.median(r['first_audio'] for r in runs):.2f}s, peak RAM {max(r['ram_gib'] for r in runs):.2f} GiB")


if __name__ == "__main__":
    main()
