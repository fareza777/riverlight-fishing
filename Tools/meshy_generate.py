"""Generate a textured 3D model with the Meshy Text-to-3D API and download it.

Usage:
    python Tools/meshy_generate.py --name Fisher --prompt "..." [--texture-prompt "..."]

Reads the API key from the MESHY_API_KEY environment variable.
Output: ArtSource/Meshy/<name>.glb (+ <name>_thumb.png)
"""
import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

API_URL = "https://api.meshy.ai/openapi/v2/text-to-3d"
OUT_DIR = Path(__file__).resolve().parent.parent / "ArtSource" / "Meshy"
POLL_SECONDS = 10
TIMEOUT_SECONDS = 15 * 60


def request(method: str, url: str, key: str, body: dict | None = None) -> dict:
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Authorization", f"Bearer {key}")
    req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            return json.loads(resp.read())
    except urllib.error.HTTPError as err:
        sys.exit(f"Meshy API error {err.code}: {err.read().decode(errors='replace')}")


def wait_for(task_id: str, key: str, label: str) -> dict:
    deadline = time.time() + TIMEOUT_SECONDS
    while time.time() < deadline:
        task = request("GET", f"{API_URL}/{task_id}", key)
        status = task["status"]
        print(f"[{label}] {status} {task.get('progress', 0)}%", flush=True)
        if status == "SUCCEEDED":
            return task
        if status in ("FAILED", "CANCELED"):
            sys.exit(f"[{label}] {status}: {task.get('task_error')}")
        time.sleep(POLL_SECONDS)
    sys.exit(f"[{label}] timed out after {TIMEOUT_SECONDS}s (task {task_id})")


def download(url: str, path: Path) -> None:
    with urllib.request.urlopen(url, timeout=120) as resp:
        path.write_bytes(resp.read())
    print(f"saved {path}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--name", required=True)
    parser.add_argument("--prompt", required=True)
    parser.add_argument("--texture-prompt", default="")
    parser.add_argument("--polycount", type=int, default=6000)
    args = parser.parse_args()

    key = os.environ.get("MESHY_API_KEY")
    if not key:
        sys.exit("MESHY_API_KEY is not set")

    preview_id = request("POST", API_URL, key, {
        "mode": "preview",
        "prompt": args.prompt,
        "ai_model": "latest",
        "should_remesh": True,
        "topology": "triangle",
        "target_polycount": args.polycount,
        "target_formats": ["glb"],
    })["result"]
    print(f"preview task {preview_id}")
    wait_for(preview_id, key, "preview")

    refine_body = {
        "mode": "refine",
        "preview_task_id": preview_id,
        "enable_pbr": False,
        "target_formats": ["glb"],
    }
    if args.texture_prompt:
        refine_body["texture_prompt"] = args.texture_prompt
    refine_id = request("POST", API_URL, key, refine_body)["result"]
    print(f"refine task {refine_id}")
    task = wait_for(refine_id, key, "refine")

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    download(task["model_urls"]["glb"], OUT_DIR / f"{args.name}.glb")
    if task.get("thumbnail_url"):
        download(task["thumbnail_url"], OUT_DIR / f"{args.name}_thumb.png")
    print(f"credits used (refine): {task.get('consumed_credits')}")


if __name__ == "__main__":
    main()
