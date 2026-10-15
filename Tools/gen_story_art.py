"""Generate cutscene slides, title/location art and item icons from ArtSource/story.json with Recraft.

  python Tools/gen_story_art.py            (skips files that already exist)
"""
from __future__ import annotations

import json
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ICON_STYLE = ("cute stylized mobile game inventory icon, hand-painted, soft shading, warm cozy colours, "
              "single object centered on plain white background, no text")


def jobs() -> list[tuple[str, str, str, bool]]:
    story = json.loads((ROOT / "ArtSource/story.json").read_text(encoding="utf-8"))
    style, grandpa, child = story["style"], story["grandpa"], story["child"]
    out: list[tuple[str, str, str, bool]] = []
    for cs in story["cutscenes"]:
        for i, slide in enumerate(cs["slides"]):
            art = slide["art"].replace("GRANDPA", grandpa).replace("CHILD", child)
            out.append((f"Assets/Art/Story/{cs['id']}_{i}.png", f"{art}. {style}", "9:16", True))
    for extra in story["extra_art"]:
        prompt = extra["art"] if extra.get("plain") else f"{extra['art']}. {style}"
        out.append((extra["out"], prompt, extra["size"], extra["keep_bg"]))
    for icon in story["icons"]:
        out.append((f"Assets/Art/UI/Items/{icon['id']}.png", f"{icon['art']}, {ICON_STYLE}", "1:1", False))
    return out


def run(job: tuple[str, str, str, bool]) -> str:
    path, prompt, size, keep_bg = job
    if (ROOT / path).exists():
        return f"skip {path}"
    cmd = [sys.executable, str(ROOT / "Tools/recraft.py"), "--out", str(ROOT / path), "--prompt", prompt, "--size", size]
    if keep_bg:
        cmd.append("--keep-bg")
    res = subprocess.run(cmd, capture_output=True, text=True)
    return (res.stdout or res.stderr).strip()


if __name__ == "__main__":
    with ThreadPoolExecutor(3) as pool:
        for line in pool.map(run, jobs()):
            print(line, flush=True)
