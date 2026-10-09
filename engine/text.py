"""Bounded synthesis chunks without dropping identifiers, punctuation, or Unicode."""
import re

MAX_CHARACTERS = 12000


def chunks(text: str, limit: int = 220) -> list[str]:
    text = text.replace("\x00", "").strip()
    if not text:
        raise ValueError("No text selected.")
    if len(text) > MAX_CHARACTERS:
        raise ValueError("Select at most 12,000 characters.")
    if limit < 2:
        raise ValueError("Chunk size must be at least two characters.")
    result = []
    while text:
        if len(text) <= limit:
            result.append(text)
            break
        window = text[:limit]
        boundaries = list(re.finditer(r"[.!?。！？](?:\s|$)|\n", window))
        split = boundaries[-1].end() if boundaries else window.rfind(" ") + 1
        if split < limit // 3:
            split = limit
        result.append(text[:split].strip())
        text = text[split:].lstrip()
    return [part for part in result if part]
