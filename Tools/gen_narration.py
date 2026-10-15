"""Voice every piece of story text in Riverlight.

  python Tools/gen_narration.py          (skips clips that already exist)

Outputs (Assets/Resources/Audio/Voice):
  <cutscene>_<slide>[_m][_id].mp3     cutscene narration; "_m" = Kai's voice for the player's own lines
                                       (the plain file is Lena's), "_id" = Indonesian
  Lines/<key>[_id].mp3                objectives, Mira, NPCs (text read from Loc.cs)

Backends, in order: ElevenLabs through fal.ai (fal credits), ElevenLabs direct (monthly quota), then Deepgram Aura-2
for English only (Deepgram has no Indonesian voices; the game then plays the English clip).
"""
from __future__ import annotations

import json
import os
import re
import ssl
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

try:
    import certifi
    SSL = ssl.create_default_context(cafile=certifi.where())
except ImportError:
    SSL = ssl.create_default_context()

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Assets/Resources/Audio/Voice"
VOICES = {  # role: (ElevenLabs voice name on fal, ElevenLabs voice id, Deepgram model)
    "grandpa": ("Bill", "pqHfZKP75CvOlQylNhV4", "aura-2-orpheus-en"),
    "laras": ("River", "SAz9YHcvj6GT2YYXdXww", "aura-2-luna-en"),
    "arka": ("Will", "bIHbv24MWmeRgasZH58o", "aura-2-arcas-en"),
    "mira": ("Jessica", "cgSgspJ2msm6clMCkdW9", "aura-2-thalia-en"),
    "bayu": ("George", "JBFqnCBsd6RMkjVDRZzb", "aura-2-zeus-en"),
    "joko": ("Chris", "iP95p4xoKVk53GoZ742B", "aura-2-apollo-en"),
    "wati": ("Matilda", "XrExE9yKIg1WjnnlVkGX", "aura-2-athena-en"),
    "dimas": ("Liam", "TX3LPaxmHKxFdv7VOQHJ", "aura-2-arcas-en"),
    "felix": ("Charlie", "IKne3meq5aSn9XLyUdCD", "aura-2-apollo-en"),
}
PLAYER_SLIDES = {("intro", 0), ("intro", 1), ("intro", 2)} | {("ending", i) for i in range(5)}
LINE_ROLES = {
    **{f"obj_{i}": "grandpa" for i in range(6)}, "obj_done": "grandpa", "got_grandpa_rod": "grandpa",
    **{f"ready_{i}": "grandpa" for i in range(1, 6)},
    **{f"mira_{i}": "mira" for i in range(4)}, "mira_order": "mira", "mira_thanks": "mira",
    **{f"{npc}_{i}": npc for npc in ("bayu", "joko", "wati", "dimas") for i in range(3)},
    **{f"felix_{i}": "felix" for i in range(3)}, "felix_arrive": "felix",
}

eleven_out = False


class QuotaError(Exception):
    pass


def request(url: str, body: dict | None, headers: dict, raw: bool = False):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method="POST" if body is not None else "GET", headers=headers)
    for attempt in range(5):
        try:
            with urllib.request.urlopen(req, timeout=120, context=SSL) as resp:
                content = resp.read()
                return content if raw else json.loads(content)
        except urllib.error.HTTPError as err:
            detail = err.read().decode(errors="replace")
            if err.code == 429 and attempt < 4:
                time.sleep(5 * (attempt + 1))
                continue
            if err.code in (401, 402) and "quota" in detail.lower():
                raise QuotaError(detail) from err
            raise RuntimeError(f"HTTP {err.code}: {detail[:300]}") from err
    raise RuntimeError("gave up after retries")


def via_fal(text: str, voice: str) -> bytes:
    key = os.environ.get("FAL_KEY")
    if not key:
        raise RuntimeError("FAL_KEY not set")
    h = {"Authorization": f"Key {key}", "Content-Type": "application/json"}
    sub = request("https://queue.fal.run/fal-ai/elevenlabs/tts/multilingual-v2",
                  {"text": text, "voice": voice, "stability": 0.55, "similarity_boost": 0.8, "style": 0.25}, h)
    for _ in range(120):
        if request(sub["status_url"], None, h).get("status") == "COMPLETED":
            url = request(sub["response_url"], None, h)["audio"]["url"]
            with urllib.request.urlopen(url, timeout=120, context=SSL) as resp:
                return resp.read()
        time.sleep(2)
    raise TimeoutError("fal tts")


def via_eleven(text: str, voice_id: str) -> bytes:
    return request(f"https://api.elevenlabs.io/v1/text-to-speech/{voice_id}?output_format=mp3_44100_128",
                   {"text": text, "model_id": "eleven_multilingual_v2",
                    "voice_settings": {"stability": 0.55, "similarity_boost": 0.8, "style": 0.25, "use_speaker_boost": True}},
                   {"xi-api-key": os.environ["ELEVENLABS_API_KEY"], "Content-Type": "application/json", "Accept": "audio/mpeg"}, raw=True)


def via_deepgram(text: str, model: str) -> bytes:
    return request(f"https://api.deepgram.com/v1/speak?model={model}&encoding=mp3", {"text": text},
                   {"Authorization": f"Token {os.environ['DEEPGRAM_API_KEY']}", "Content-Type": "application/json"}, raw=True)


def speak(text: str, role: str, indonesian: bool, path: Path) -> str:
    global eleven_out
    if path.exists():
        return ""
    text = text.replace('"', "").replace("—", ",")
    fal_name, eleven_id, deepgram_model = VOICES[role]
    try:
        path.write_bytes(via_fal(text, fal_name))
        return f"fal {path.name} ({role})"
    except Exception as err:  # fall through to the next backend
        print(f"  fal failed for {path.name}: {err}", flush=True)
    if not eleven_out and os.environ.get("ELEVENLABS_API_KEY"):
        try:
            path.write_bytes(via_eleven(text, eleven_id))
            return f"eleven {path.name} ({role})"
        except QuotaError:
            eleven_out = True
    if indonesian:
        return f"missing {path.name} (no Indonesian fallback)"
    path.write_bytes(via_deepgram(text, deepgram_model))
    return f"deepgram {path.name}"


def loc_table() -> dict[str, tuple[str, str]]:
    src = (ROOT / "Assets/Scripts/Core/Loc.cs").read_text(encoding="utf-8")
    rx = re.compile(r'\["(\w+)"\]\s*=\s*\("((?:[^"\\]|\\.)*)",\s*"((?:[^"\\]|\\.)*)"\)')
    return {m.group(1): (m.group(2).replace('\\"', '"'), m.group(3).replace('\\"', '"')) for m in rx.finditer(src)}


def main() -> None:
    (OUT / "Lines").mkdir(parents=True, exist_ok=True)
    story = json.loads((ROOT / "ArtSource/story.json").read_text(encoding="utf-8"))
    loc = loc_table()
    jobs = []
    for indonesian in (True, False):
        suffix = "_id" if indonesian else ""
        for cs in story["cutscenes"]:
            for i, slide in enumerate(cs["slides"]):
                text = slide["id" if indonesian else "en"]
                if (cs["id"], i) in PLAYER_SLIDES:
                    jobs.append((text, "laras", indonesian, OUT / f"{cs['id']}_{i}{suffix}.mp3"))
                    jobs.append((text, "arka", indonesian, OUT / f"{cs['id']}_{i}_m{suffix}.mp3"))
                else:
                    jobs.append((text, "grandpa", indonesian, OUT / f"{cs['id']}_{i}{suffix}.mp3"))
        for key, role in LINE_ROLES.items():
            id_text, en_text = loc[key]
            jobs.append((id_text if indonesian else en_text, role, indonesian, OUT / "Lines" / f"{key}{suffix}.mp3"))
    for job in jobs:
        msg = speak(*job)
        if msg:
            print(msg, flush=True)


if __name__ == "__main__":
    main()
