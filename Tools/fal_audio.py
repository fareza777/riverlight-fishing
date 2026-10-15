"""Generate music and sound effects with fal.ai and save them under Assets/Resources/Audio.

  python Tools/fal_audio.py probe <model>            check a model id answers
  python Tools/fal_audio.py batch                    generate everything in AUDIO (skips existing files)

Reads FAL_KEY from the environment. Dev-time only: nothing here ships in the game.
"""
from __future__ import annotations

import json
import os
import ssl
import sys
import time
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

try:
    import certifi
    SSL = ssl.create_default_context(cafile=certifi.where())   # fal CDN chain is missing from the Windows store
except ImportError:
    SSL = ssl.create_default_context()

ROOT = Path(__file__).resolve().parent.parent
RES = ROOT / "Assets/Resources/Audio"
MUSIC_MODEL = "fal-ai/stable-audio-25/text-to-audio"
SFX_MODEL = "fal-ai/elevenlabs/sound-effects/v2"

MUSIC_STYLE = "cozy acoustic instrumental, gentle, warm, high quality studio recording, loopable, no vocals"
AUDIO = [
    # (folder, name, kind, prompt, seconds)
    ("Music", "title", "music", "hopeful nostalgic main theme, fingerpicked acoustic guitar, soft piano, light strings, gentle flute melody, 80 bpm", 95),
    ("Music", "river_day", "music", "peaceful riverside afternoon, plucked acoustic guitar and kalimba, soft shaker, warm and sunny, 90 bpm", 120),
    ("Music", "lake_day", "music", "dreamy calm lake, soft piano and harp arpeggios, airy pads, gentle and reflective, 75 bpm", 120),
    ("Music", "gorge_day", "music", "adventurous mountain gorge, acoustic guitar strumming, warm cello, light hand drums, uplifting, 100 bpm", 120),
    ("Music", "estuary_day", "music", "breezy seaside estuary, ukulele and marimba, soft accordion, light and cheerful, 95 bpm", 120),
    ("Music", "night", "music", "quiet starry night, slow music box and soft felt piano, warm pads, lullaby, 60 bpm", 120),
    ("Music", "memory", "music", "tender bittersweet memory, solo felt piano with soft strings, emotional and nostalgic, 65 bpm", 90),
    ("Ambience", "river", "sfx", "gentle flowing river water with soft birdsong in a forest, continuous ambience", 22),
    ("Ambience", "lake", "sfx", "calm lake shore ambience, tiny lapping water, distant loons and soft wind in willow leaves", 22),
    ("Ambience", "gorge", "sfx", "mountain gorge ambience with rushing rapids and a distant waterfall, wind in pines", 22),
    ("Ambience", "estuary", "sfx", "seaside estuary ambience, soft waves on sand, seagulls, wind in reeds", 22),
    ("Ambience", "night", "sfx", "peaceful summer night ambience, crickets and frogs by a quiet river", 22),
    ("Sfx", "ui_click", "sfx", "soft wooden UI button click, short", 0.5),
    ("Sfx", "toast", "sfx", "gentle soft notification chime, two warm bell notes", 1.2),
    ("Sfx", "cast", "sfx", "fishing rod cast whoosh with reel line zipping out", 1.2),
    ("Sfx", "splash_small", "sfx", "small fishing bobber plop into calm water", 0.8),
    ("Sfx", "splash_big", "sfx", "big fish splashing out of water", 1.5),
    ("Sfx", "bite", "sfx", "quick bobber dip bloop in water with a tiny tug", 0.7),
    ("Sfx", "hook", "sfx", "fishing rod sharp yank whip with line snap tight", 0.6),
    ("Sfx", "reel_tick", "sfx", "single fishing reel ratchet click, very short", 0.3),
    ("Sfx", "snap", "sfx", "fishing line snapping, sharp twang", 0.7),
    ("Sfx", "catch", "sfx", "happy short magical success jingle with sparkle, acoustic, cozy game", 2.0),
    ("Sfx", "catch_legend", "sfx", "grand triumphant short fanfare with harp glissando and warm brass, cozy game legendary reward", 3.5),
    ("Sfx", "rod_pickup", "sfx", "picking up a wooden fishing rod, light wood knock and rustle", 0.8),
    ("Sfx", "coins", "sfx", "handful of coins jingling into a pouch", 1.0),
    ("Sfx", "shop_bell", "sfx", "small shop door bell ring", 1.2),
    ("Sfx", "book_open", "sfx", "old leather journal opening, pages rustling", 1.2),
    ("Sfx", "chapter", "sfx", "warm magical chapter reveal chime with soft harp and bells", 2.5),
    ("Sfx", "start", "sfx", "gentle welcoming start game whoosh with soft chime", 1.5),
    ("Sfx", "oar_stroke", "sfx", "wooden oar paddle stroke in calm water", 1.0),
    ("Sfx", "footstep_wood", "sfx", "single footstep on a wooden dock plank", 0.4),
    ("Sfx", "door", "sfx", "old wooden cabin door opening creak", 1.2),
]


def key() -> str:
    return os.environ.get("FAL_KEY") or sys.exit("FAL_KEY is not set")


def request(url: str, body: dict | None = None) -> dict:
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method="POST" if body is not None else "GET",
                                 headers={"Authorization": f"Key {key()}", "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=120, context=SSL) as resp:
        return json.loads(resp.read())


def run(model: str, args: dict) -> dict:
    sub = request(f"https://queue.fal.run/{model}", args)
    for _ in range(240):
        st = request(sub["status_url"])
        if st.get("status") == "COMPLETED":
            return request(sub["response_url"])
        time.sleep(3)
    raise TimeoutError(model)


def audio_url(res: dict) -> str:
    for k in ("audio", "audio_file", "audio_url"):
        v = res.get(k)
        if isinstance(v, dict) and "url" in v:
            return v["url"]
        if isinstance(v, str):
            return v
    raise KeyError(f"no audio in {list(res)}")


def make(item: tuple) -> str:
    folder, name, kind, prompt, seconds = item
    out_dir = RES / folder
    out_dir.mkdir(parents=True, exist_ok=True)
    existing = list(out_dir.glob(f"{name}.*"))
    if existing:
        return f"skip {folder}/{name}"
    try:
        if kind == "music":
            res = run(MUSIC_MODEL, {"prompt": f"{prompt}, {MUSIC_STYLE}", "seconds_total": seconds, "num_inference_steps": 8})
        else:
            res = run(SFX_MODEL, {"text": prompt, "duration_seconds": max(0.5, seconds), "prompt_influence": 0.5})
        url = audio_url(res)
        ext = ".wav" if ".wav" in url else ".mp3"
        with urllib.request.urlopen(url, timeout=120, context=SSL) as resp:
            (out_dir / f"{name}{ext}").write_bytes(resp.read())
        return f"saved {folder}/{name}{ext}"
    except Exception as err:  # report and continue with the rest of the batch
        detail = err.read().decode(errors="replace")[:300] if isinstance(err, urllib.error.HTTPError) else str(err)
        return f"FAILED {folder}/{name}: {detail}"


if __name__ == "__main__":
    if sys.argv[1] == "probe":
        try:
            print(json.dumps(request(f"https://queue.fal.run/{sys.argv[2]}", json.loads(sys.argv[3]) if len(sys.argv) > 3 else {}))[:400])
        except urllib.error.HTTPError as e:
            print(e.code, e.read().decode(errors="replace")[:400])
    else:
        with ThreadPoolExecutor(4) as pool:
            for line in pool.map(make, AUDIO):
                print(line, flush=True)
