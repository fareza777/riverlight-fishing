"""Meshy pipeline CLI for Riverlight: concept image -> 3D -> rig -> animations.

Every successful task is recorded in ArtSource/Meshy/tasks.json under its --name so later steps can chain.

  python Tools/meshy_pipeline.py image   --name FisherM --prompt "..." [--model nano-banana-2] [--aspect 3:4]
  python Tools/meshy_pipeline.py to3d    --name FisherM [--image-task ID | --image FILE] [--polycount 12000] [--pose a-pose]
  python Tools/meshy_pipeline.py rig     --name FisherM [--height 1.7]
  python Tools/meshy_pipeline.py animate --name FisherM --clip Locomotion --actions 0,30,14
  python Tools/meshy_pipeline.py motion  --name FisherM --clip Rowing --prompt "..." --duration 4
  python Tools/meshy_pipeline.py library --search walk

Reads the key from MESHY_API_KEY. Outputs go to ArtSource/Meshy/<name>/.
"""
from __future__ import annotations

import argparse
import base64
import json
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

BASE = "https://api.meshy.ai/openapi"
ROOT = Path(__file__).resolve().parent.parent / "ArtSource" / "Meshy"
TASKS_FILE = ROOT / "tasks.json"
POLL_SECONDS = 8
TIMEOUT_SECONDS = 25 * 60


def api_key() -> str:
    key = os.environ.get("MESHY_API_KEY")
    if not key:
        sys.exit("MESHY_API_KEY is not set")
    return key


def call(method: str, path: str, body: dict | None = None) -> dict:
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(f"{BASE}{path}", data=data, method=method)
    req.add_header("Authorization", f"Bearer {api_key()}")
    req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=90) as resp:
            return json.loads(resp.read())
    except urllib.error.HTTPError as err:
        sys.exit(f"Meshy API error {err.code} on {path}: {err.read().decode(errors='replace')}")


def wait(path: str, task_id: str, label: str) -> dict:
    deadline = time.time() + TIMEOUT_SECONDS
    last = None
    while time.time() < deadline:
        task = call("GET", f"{path}/{task_id}")
        status, progress = task.get("status"), task.get("progress", 0)
        if (status, progress) != last:
            print(f"[{label}] {status} {progress}%", flush=True)
            last = (status, progress)
        if status == "SUCCEEDED":
            return task
        if status in ("FAILED", "CANCELED"):
            sys.exit(f"[{label}] {status}: {task.get('task_error')}")
        time.sleep(POLL_SECONDS)
    sys.exit(f"[{label}] timed out (task {task_id})")


def download(url: str, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with urllib.request.urlopen(url, timeout=300) as resp:
        path.write_bytes(resp.read())
    print(f"saved {path.relative_to(ROOT.parent.parent)}")


def tasks_file(name: str, step: str) -> Path:
    # One file per (name, step) so parallel runs never race on the same JSON.
    return ROOT / name / "tasks" / f"{step}.txt"


def record(name: str, step: str, task_id: str) -> None:
    path = tasks_file(name, step)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(task_id)


def task_of(name: str, step: str) -> str:
    path = tasks_file(name, step)
    tid = path.read_text().strip() if path.exists() else (json.loads(TASKS_FILE.read_text()).get(name, {}).get(step) if TASKS_FILE.exists() else None)
    if not tid:
        sys.exit(f"No '{step}' task recorded for {name}; run that step first")
    return tid


# ----------------------------------------------------------------------------- steps
def cmd_image(a: argparse.Namespace) -> None:
    body = {"ai_model": a.model, "prompt": a.prompt, "aspect_ratio": a.aspect}
    if a.remove_bg:
        body["remove_background"] = True
    tid = call("POST", "/v1/text-to-image", body)["result"]
    task = wait("/v1/text-to-image", tid, "image")
    record(a.name, "image", tid)
    for i, url in enumerate(task["image_urls"]):
        download(url, ROOT / a.name / f"concept_{tid[-6:]}_{i}.png")


def cmd_to3d(a: argparse.Namespace) -> None:
    body = {
        "ai_model": "latest",
        "should_remesh": True,
        "topology": "triangle",
        "target_polycount": a.polycount,
        "should_texture": True,
        "enable_pbr": False,
        "texture_resolution": "2k",
        "target_formats": ["glb", "fbx"],
        "multi_view_thumbnails": True,
    }
    if a.pose:
        body["pose_mode"] = a.pose
    if a.texture_prompt:
        body["texture_prompt"] = a.texture_prompt
    if a.image:
        mime = "image/png" if a.image.lower().endswith(".png") else "image/jpeg"
        body["image_url"] = f"data:{mime};base64," + base64.b64encode(Path(a.image).read_bytes()).decode()
    else:
        body["input_task_id"] = a.image_task or task_of(a.name, "image")
    tid = call("POST", "/v1/image-to-3d", body)["result"]
    record(a.name, "to3d_pending", tid)
    task = wait("/v1/image-to-3d", tid, "image-to-3d")
    record(a.name, "to3d", tid)
    out = ROOT / a.name
    download(task["model_urls"]["glb"], out / f"{a.name}.glb")
    for view, url in (task.get("thumbnail_urls") or {}).items():
        download(url, out / f"thumb_{view}.png")
    print(f"credits: {task.get('consumed_credits')}")


def cmd_rig(a: argparse.Namespace) -> None:
    body = {"input_task_id": task_of(a.name, "to3d"), "height_meters": a.height}
    tid = call("POST", "/v1/rigging", body)["result"]
    task = wait("/v1/rigging", tid, "rigging")
    record(a.name, "rig", tid)
    res = task.get("result", task)
    out = ROOT / a.name
    download(res["rigged_character_fbx_url"], out / f"{a.name}_Rigged.fbx")
    download(res["rigged_character_glb_url"], out / f"{a.name}_Rigged.glb")
    for clip, urls in (res.get("basic_animations") or {}).items():
        if isinstance(urls, str) and urls.startswith("http") and "fbx" in clip and "armature" not in clip:
            download(urls, out / f"{a.name}_Basic_{clip}.fbx")


def cmd_animate(a: argparse.Namespace) -> None:
    body: dict = {"rig_task_id": task_of(a.name, "rig"), "post_process": {"operation_type": "change_fps", "fps": 30}}
    if a.motion:
        body["motion_task_id"] = a.motion
        body.pop("post_process")
    else:
        ids = [int(x) for x in a.actions.split(",")]
        if len(ids) == 1:
            body["action_id"] = ids[0]
        else:
            body["action_ids"] = ids
    tid = call("POST", "/v1/animations", body)["result"]
    task = wait("/v1/animations", tid, f"animate {a.clip}")
    record(a.name, f"anim_{a.clip}", tid)
    res = task.get("result", task)
    url = res.get("processed_animation_fps_fbx_url") or res.get("animation_fbx_url")
    if url:
        download(url, ROOT / a.name / "Animations" / f"{a.name}@{a.clip}.fbx")
    elif res.get("animation_glb_url"):
        download(res["animation_glb_url"], ROOT / a.name / "Animations" / f"{a.name}@{a.clip}.glb")
    else:
        print(json.dumps(res, indent=1)[:1500])


def cmd_motion(a: argparse.Namespace) -> None:
    tid = call("POST", "/v1/text-to-motion", {"prompt": a.prompt, "mode": "prime", "duration": a.duration})["result"]
    wait("/v1/text-to-motion", tid, f"motion {a.clip}")
    record(a.name, f"motion_{a.clip}", tid)
    a.motion = tid
    cmd_animate(a)


def cmd_fetch(a: argparse.Namespace) -> None:
    """Re-download an existing image-to-3D result as FBX (+ textures) without spending credits."""
    task = call("GET", f"/v1/image-to-3d/{task_of(a.name, 'to3d')}")
    out = ROOT / a.name
    download(task["model_urls"]["fbx"], out / f"{a.name}.fbx")
    for tex in task.get("texture_urls") or []:
        if tex.get("base_color"):
            download(tex["base_color"], out / f"{a.name}_BaseColor.png")


def cmd_library(a: argparse.Namespace) -> None:
    items = call("GET", f"/v1/animations/library?search={a.search}")
    items = items if isinstance(items, list) else items.get("result", [])
    for it in items[:40]:
        print(it.get("action_id"), "|", it.get("name"), "|", it.get("category"), "/", it.get("sub_category"))


def main() -> None:
    p = argparse.ArgumentParser()
    sub = p.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("image"); s.add_argument("--name", required=True); s.add_argument("--prompt", required=True)
    s.add_argument("--model", default="nano-banana-2"); s.add_argument("--aspect", default="3:4"); s.add_argument("--remove-bg", action="store_true")
    s = sub.add_parser("to3d"); s.add_argument("--name", required=True); s.add_argument("--image-task"); s.add_argument("--image")
    s.add_argument("--polycount", type=int, default=12000); s.add_argument("--pose", default="a-pose"); s.add_argument("--texture-prompt", default="")
    s = sub.add_parser("rig"); s.add_argument("--name", required=True); s.add_argument("--height", type=float, default=1.7)
    s = sub.add_parser("animate"); s.add_argument("--name", required=True); s.add_argument("--clip", required=True)
    s.add_argument("--actions", default=""); s.add_argument("--motion", default="")
    s = sub.add_parser("motion"); s.add_argument("--name", required=True); s.add_argument("--clip", required=True)
    s.add_argument("--prompt", required=True); s.add_argument("--duration", type=float, default=4.0)
    s = sub.add_parser("library"); s.add_argument("--search", default="")
    s = sub.add_parser("fetch"); s.add_argument("--name", required=True)
    a = p.parse_args()
    {"image": cmd_image, "to3d": cmd_to3d, "rig": cmd_rig, "animate": cmd_animate, "motion": cmd_motion, "library": cmd_library, "fetch": cmd_fetch}[a.cmd](a)


if __name__ == "__main__":
    main()
