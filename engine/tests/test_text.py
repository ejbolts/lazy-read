import sys
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from text import chunks
from worker import Worker
import tempfile


class ReadingTests(unittest.TestCase):
    def test_unicode_and_code_are_preserved(self):
        text = 'const greeting = "Hello, 世界 👋";\n' * 30
        parts = chunks(text)
        self.assertTrue(all(0 < len(part) <= 220 for part in parts))
        self.assertEqual(''.join(text.split()), ''.join(''.join(parts).split()))

    def test_unbroken_identifier_does_not_hang_or_truncate(self):
        text = 'x' * 800
        self.assertEqual(''.join(chunks(text)), text)

    def test_invalid_text_is_rejected(self):
        for text in (' \x00 ', 'a' * 12001):
            with self.assertRaises(ValueError):
                chunks(text)

    def test_new_reading_cancels_previous_and_empty_does_not(self):
        with tempfile.TemporaryDirectory() as directory:
            worker = Worker(Path(directory), 'test')
            worker.submit({'id': 'first', 'text': 'First reading.'})
            first = worker.current[1]
            with self.assertRaises(ValueError):
                worker.submit({'id': 'empty', 'text': ''})
            self.assertFalse(first.is_set())
            worker.submit({'id': 'second', 'text': 'Second reading.'})
            self.assertTrue(first.is_set())
            self.assertFalse(worker.current[1].is_set())
            worker.cancel('first')
            self.assertFalse(worker.current[1].is_set(), 'Delayed cancel must not stop a newer reading')
            worker.cancel()
            self.assertTrue(worker.current[1].is_set())


if __name__ == '__main__':
    unittest.main()
