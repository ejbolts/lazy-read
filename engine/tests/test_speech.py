import io
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import speech


class SpeechTests(unittest.TestCase):
    def test_language_follows_voice_accent(self):
        self.assertEqual(speech.language_for("af_heart"), "en-us")
        self.assertEqual(speech.language_for("bm_george"), "en-gb")

    def test_tampered_download_is_discarded(self):
        with tempfile.TemporaryDirectory() as directory, \
                mock.patch.object(speech, "FILES", {"voices-v1.0.bin": "0" * 64}), \
                mock.patch.object(speech.urllib.request, "urlopen", return_value=io.BytesIO(b"not the model")):
            with self.assertRaises(RuntimeError):
                speech.download_verified(Path(directory))
            self.assertEqual(list(Path(directory).iterdir()), [])


if __name__ == "__main__":
    unittest.main()
