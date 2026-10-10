import io
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import backends


class BackendTests(unittest.TestCase):
    def test_model_ids_select_their_backend(self):
        self.assertIsInstance(backends.create(backends.KOKORO_ID), backends.KokoroBackend)
        self.assertIsInstance(backends.create(backends.POCKET_ID), backends.PocketBackend)
        self.assertIsInstance(backends.create("Qwen/Qwen3-TTS-12Hz-0.6B-CustomVoice"), backends.QwenBackend)

    def test_kokoro_skips_pytorch(self):
        self.assertNotIn("torch", backends.KokoroBackend.modules)

    def test_kokoro_language_follows_voice_accent(self):
        self.assertEqual(backends.kokoro_language("af_heart"), "en-us")
        self.assertEqual(backends.kokoro_language("bm_george"), "en-gb")

    def test_tampered_download_is_discarded(self):
        with tempfile.TemporaryDirectory() as directory, \
                mock.patch.object(backends, "KOKORO_FILES", {"voices-v1.0.bin": "0" * 64}), \
                mock.patch.object(backends.urllib.request, "urlopen", return_value=io.BytesIO(b"not the model")):
            with self.assertRaises(RuntimeError):
                backends.download_verified(Path(directory))
            self.assertEqual(list(Path(directory).iterdir()), [])


if __name__ == "__main__":
    unittest.main()
