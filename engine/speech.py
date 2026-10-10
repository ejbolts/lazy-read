"""Local Kokoro v1.0 speech through ONNX Runtime. No PyTorch or GPU is required."""
import contextlib
import hashlib
import os
from pathlib import Path
import sys
import urllib.request

NAME = "Kokoro v1.0"
DEFAULT_VOICE = "af_heart"
# Kokoro v1.0 weights as exported for kokoro-onnx (release model-files-v1.1), pinned by SHA-256.
RELEASE = "https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.1/"
FILES = {
    "kokoro-v1.0.onnx": "beb0d1848dee9a49da392cc3df26958d46cfa35d321edf434f52949153f0df3a",
    "voices-v1.0.bin": "bca610b8308e8d99f32e6fe4197e7ec01679264efed0cac9140fe9c29f1fbf7d",
}


def model_directory() -> Path:
    return Path(os.environ.get("LOCALAPPDATA", Path.home())) / "LazyRead" / "models" / "kokoro-v1.0"


def language_for(voice: str) -> str:
    # Kokoro voice names start with a language letter: a = American English, b = British English.
    return "en-gb" if voice.startswith("b") else "en-us"


def preload():
    # On Windows, NumPy/ONNX Runtime DLL initialization must finish on the main thread
    # before a background thread imports them while the main thread blocks on stdin.
    with contextlib.redirect_stdout(sys.stderr):
        import kokoro_onnx
        import onnxruntime
        import soundfile


class Speech:
    def __init__(self):
        self.model = None

    def load(self, progress):
        directory = model_directory()
        if not all((directory / name).is_file() for name in FILES):
            progress("Downloading Kokoro model files for the first launch…")
            download_verified(directory)
        from kokoro_onnx import Kokoro
        self.model = Kokoro(str(directory / "kokoro-v1.0.onnx"), str(directory / "voices-v1.0.bin"))
        return "CPU"

    def synthesize(self, text, voice):
        return self.model.create(text, voice=voice, lang=language_for(voice))


def download_verified(directory: Path):
    directory.mkdir(parents=True, exist_ok=True)
    for name, expected in FILES.items():
        target = directory / name
        if target.is_file():
            continue
        partial = target.with_suffix(target.suffix + ".part")
        digest = hashlib.sha256()
        with urllib.request.urlopen(RELEASE + name, timeout=60) as response, partial.open("wb") as output:
            while block := response.read(1 << 20):
                digest.update(block)
                output.write(block)
        if digest.hexdigest() != expected:
            partial.unlink(missing_ok=True)
            raise RuntimeError(f"Downloaded {name} failed its integrity check.")
        partial.replace(target)
