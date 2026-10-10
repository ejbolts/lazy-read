"""Local speech backends. Each imports only its own libraries so lighter models start faster."""
import contextlib
import hashlib
import importlib
import os
from pathlib import Path
import sys
import urllib.request

KOKORO_ID = "kokoro-onnx/kokoro-v1.0"
POCKET_ID = "kyutai/pocket-tts"
# Kokoro v1.0 weights as exported for kokoro-onnx (release model-files-v1.1), pinned by SHA-256.
KOKORO_RELEASE = "https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.1/"
KOKORO_FILES = {
    "kokoro-v1.0.onnx": "beb0d1848dee9a49da392cc3df26958d46cfa35d321edf434f52949153f0df3a",
    "voices-v1.0.bin": "bca610b8308e8d99f32e6fe4197e7ec01679264efed0cac9140fe9c29f1fbf7d",
}


def model_directory() -> Path:
    return Path(os.environ.get("LOCALAPPDATA", Path.home())) / "LazyRead" / "models"


def kokoro_language(voice: str) -> str:
    # Kokoro voice names start with a language letter: a = American English, b = British English.
    return "en-gb" if voice.startswith("b") else "en-us"


class QwenBackend:
    name = "Qwen3-TTS"
    modules = ("torch", "qwen_tts")

    def __init__(self, model_id: str):
        self.model_id = model_id
        self.model = None

    def load(self, progress):
        import torch
        progress("PyTorch loaded. Loading the Qwen3 speech package…")
        from qwen_tts import Qwen3TTSModel
        progress("Speech package loaded. Preparing the model cache…")
        torch.set_num_threads(6)
        device = "cuda:0" if torch.cuda.is_available() else "cpu"
        dtype = torch.bfloat16 if device.startswith("cuda") else torch.float32
        options = dict(device_map=device, dtype=dtype, attn_implementation="sdpa")
        # Qwen's processor/tokenizer loaders do not propagate local_files_only.
        # Resolve the complete snapshot first so cached starts do not hit the Hub.
        from huggingface_hub import snapshot_download
        try:
            model_path = snapshot_download(self.model_id, local_files_only=True)
            self.model = Qwen3TTSModel.from_pretrained(model_path, **options)
        except OSError:
            progress("Downloading Qwen3-TTS model files for the first launch…")
            model_path = snapshot_download(self.model_id)
            self.model = Qwen3TTSModel.from_pretrained(model_path, **options)
        return torch.cuda.get_device_name(0) if device.startswith("cuda") else "CPU"

    def synthesize(self, text, request, cancelled):
        from transformers import StoppingCriteria, StoppingCriteriaList

        class CancelReading(StoppingCriteria):
            def __call__(self, input_ids, scores, **kwargs):
                return cancelled.is_set()

        wavs, sample_rate = self.model.generate_custom_voice(
            text=text,
            language=request.get("language", "English"),
            speaker=request.get("voice", "Ryan"),
            instruct="Read clearly and naturally, with a calm conversational delivery.",
            max_new_tokens=1024,
            stopping_criteria=StoppingCriteriaList([CancelReading()]),
        )
        return wavs[0], sample_rate


class KokoroBackend:
    name = "Kokoro"
    modules = ("onnxruntime", "kokoro_onnx")

    def __init__(self, model_id: str):
        self.model_id = model_id
        self.model = None

    def load(self, progress):
        directory = model_directory() / "kokoro-v1.0"
        if not all((directory / name).is_file() for name in KOKORO_FILES):
            progress("Downloading Kokoro model files for the first launch…")
            download_verified(directory)
        from kokoro_onnx import Kokoro
        self.model = Kokoro(str(directory / "kokoro-v1.0.onnx"), str(directory / "voices-v1.0.bin"))
        return "CPU"

    def synthesize(self, text, request, cancelled):
        voice = request.get("voice", "af_heart")
        return self.model.create(text, voice=voice, lang=kokoro_language(voice))


class PocketBackend:
    name = "Pocket TTS"
    modules = ("torch", "pocket_tts")

    def __init__(self, model_id: str):
        self.model_id = model_id
        self.model = None
        self.voices = {}

    def load(self, progress):
        from pocket_tts import TTSModel
        progress("Speech package loaded. Loading Pocket TTS…")
        # The default English model downloads from Hugging Face once, then loads from its cache.
        self.model = TTSModel.load_model(language="english")
        return "CPU"

    def synthesize(self, text, request, cancelled):
        import torch
        voice = request.get("voice", "alba")
        if voice not in self.voices:
            self.voices[voice] = self.model.get_state_for_audio_prompt(voice)
        frames = list(self.model.generate_audio_stream(self.voices[voice], text, stop=cancelled))
        audio = torch.cat(frames, dim=-1) if frames else torch.zeros(0)
        return audio.reshape(-1).numpy(), self.model.sample_rate


def create(model_id: str):
    if model_id == KOKORO_ID:
        return KokoroBackend(model_id)
    if model_id == POCKET_ID:
        return PocketBackend(model_id)
    return QwenBackend(model_id)


def preload(backend):
    # On Windows, NumPy/PyTorch DLL initialization must finish on the main thread
    # before a background thread imports them while the main thread blocks on stdin.
    with contextlib.redirect_stdout(sys.stderr):
        for module in (*backend.modules, "soundfile"):
            importlib.import_module(module)


def download_verified(directory: Path):
    directory.mkdir(parents=True, exist_ok=True)
    for name, expected in KOKORO_FILES.items():
        target = directory / name
        if target.is_file():
            continue
        partial = target.with_suffix(target.suffix + ".part")
        digest = hashlib.sha256()
        with urllib.request.urlopen(KOKORO_RELEASE + name, timeout=60) as response, partial.open("wb") as output:
            while block := response.read(1 << 20):
                digest.update(block)
                output.write(block)
        if digest.hexdigest() != expected:
            partial.unlink(missing_ok=True)
            raise RuntimeError(f"Downloaded {name} failed its integrity check.")
        partial.replace(target)
