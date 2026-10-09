"""Opt-in integration check using the real local model and synthetic sample text."""
import argparse
import array
import json
import math
import os
from pathlib import Path
import queue
import subprocess
import sys
import threading
import time
import wave


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--offline', action='store_true', help='Require cached model files; disable Hub networking.')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    evidence = root / 'artifacts' / 'visual-review' / 'initial-app' / ('offline' if args.offline else 'engine')
    evidence.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, PYTHONUTF8='1', HF_HUB_DISABLE_TELEMETRY='1', LAZY_READ_DEBUG_LOAD='1')
    if args.offline:
        env['HF_HUB_OFFLINE'] = '1'
        env['TRANSFORMERS_OFFLINE'] = '1'
    events = queue.Queue()
    report = {'offline': args.offline, 'checks': []}
    with (evidence / 'worker.log').open('w', encoding='utf-8') as errors:
        process = subprocess.Popen(
            [sys.executable, '-u', str(root / 'engine' / 'worker.py'), '--audio-dir', str(evidence)],
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=errors,
            text=True, encoding='utf-8', env=env, cwd=root)

        def read():
            for line in process.stdout:
                try:
                    event = json.loads(line)
                except json.JSONDecodeError:
                    events.put({'type': 'protocol-failure', 'message': 'Non-JSON stdout'})
                    continue
                events.put(event)
            events.put({'type': 'process-ended'})

        threading.Thread(target=read, daemon=True).start()

        def send(payload):
            process.stdin.write(json.dumps(payload, ensure_ascii=False) + '\n')
            process.stdin.flush()

        def wait(kind, request_id=None, timeout=240):
            end = time.monotonic() + timeout
            while time.monotonic() < end:
                try:
                    event = events.get(timeout=min(10, end - time.monotonic()))
                except queue.Empty:
                    if process.poll() is not None:
                        raise RuntimeError('Worker exited; inspect worker.log')
                    continue
                print(json.dumps({k: v for k, v in event.items() if k != 'path'}), flush=True)
                if event['type'] == kind and (request_id is None or event.get('id') == request_id):
                    return event
                if event['type'] in ('error', 'protocol-failure', 'process-ended'):
                    raise RuntimeError(str(event))
            raise TimeoutError(f'Timed out waiting for {kind}')

        def check_audio(event):
            with wave.open(event['path'], 'rb') as audio:
                assert audio.getsampwidth() == 2 and audio.getnchannels() == 1
                duration = audio.getnframes() / audio.getframerate()
                samples = array.array('h', audio.readframes(audio.getnframes()))
                rms = math.sqrt(sum(value * value for value in samples) / len(samples))
            assert duration > .5 and rms > 5, (duration, rms)
            report['checks'].append({'id': event['id'], 'duration_seconds': round(duration, 2),
                                     'rms': round(rms, 1), 'generation_seconds': event['generation_seconds']})

        try:
            ready = wait('ready', timeout=1800)
            report['device'] = ready['device']
            send({'action': 'speak', 'id': 'invalid', 'text': ''})
            wait('error', 'invalid')
            report['checks'].append('Empty input rejected without terminating the engine')
            send({'action': 'speak', 'id': 'sample', 'text': 'Lazy Read is ready. Your selected text stays on this computer.',
                  'voice': 'Ryan', 'language': 'English'})
            check_audio(wait('audio', 'sample'))
            wait('done', 'sample')
            if not args.offline:
                send({'action': 'speak', 'id': 'old', 'text': 'This reading will be cancelled. ' * 15})
                wait('generating', 'old')
                send({'action': 'cancel', 'id': 'old'})
                send({'action': 'speak', 'id': 'new', 'text': 'The next selection works. Unicode: café.', 'voice': 'Ryan', 'language': 'English'})
                send({'action': 'cancel', 'id': 'old'})
                check_audio(wait('audio', 'new'))
                wait('done', 'new')
                report['checks'].append('Cancellation and replacement; delayed cancel cannot stop newer text')
            (evidence / 'report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
            print('PASS: real local Qwen3-TTS integration', flush=True)
        finally:
            if process.poll() is None:
                send({'action': 'shutdown'})
                try:
                    process.wait(timeout=15)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()


if __name__ == '__main__':
    main()
