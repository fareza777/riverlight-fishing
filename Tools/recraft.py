"""Recraft image generation for Riverlight UI art (fish illustrations, icons...).

  python Tools/recraft.py --out Assets/Art/UI/Fish/SilverBream.png --prompt "..." [--model recraftv4_1] [--size 3:2] [--keep-bg]

Generates one raster image, removes the background (unless --keep-bg) and saves a PNG.
Reads the key from RECRAFT_API_KEY.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.request
from pathlib import Path

BASE = "https://external.api.recraft.ai/v1"


def key() -> str:
    k = os.environ.get("RECRAFT_API_KEY")
    if not k:
        sys.exit("RECRAFT_API_KEY is not set")
    return k


def post(path: str, body: dict) -> dict:
    import time
    for attempt in range(8):
        req = urllib.request.Request(f"{BASE}{path}", data=json.dumps(body).encode(), method="POST")
        req.add_header("Authorization", f"Bearer {key()}")
        req.add_header("Content-Type", "application/json")
        try:
            with urllib.request.urlopen(req, timeout=180) as resp:
                return json.loads(resp.read())
        except urllib.error.HTTPError as err:
            if err.code == 429 and attempt < 7:
                time.sleep(6 * (attempt + 1))   # rate limited: back off and retry
                continue
            sys.exit(f"Recraft error {err.code} on {path}: {err.read().decode(errors='replace')}")
    sys.exit("Recraft: gave up after retries")


def download(url: str, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with urllib.request.urlopen(url, timeout=180) as resp:
        path.write_bytes(resp.read())


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("--out", required=True)
    p.add_argument("--prompt", required=True)
    p.add_argument("--model", default="recraftv4_1")
    p.add_argument("--size", default="3:2")
    p.add_argument("--seed", type=int, default=None)
    p.add_argument("--keep-bg", action="store_true")
    a = p.parse_args()

    body = {"prompt": a.prompt, "model": a.model, "size": a.size, "image_format": "png", "n": 1}
    if a.seed is not None:
        body["random_seed"] = a.seed
    gen = post("/images/generations", body)
    url = gen["data"][0]["url"]
    if not a.keep_bg:
        url = post("/images/removeBackground", {"image_url": url, "image_format": "png"})["image"]["url"]
    out = Path(a.out)
    download(url, out)
    print(f"saved {out} (credits used: {gen.get('credits')})")


if __name__ == "__main__":
    main()
