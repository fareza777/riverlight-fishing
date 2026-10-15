"""Generates missing fish illustrations from ArtSource/fish_catalog.json (3 in flight, rate-limit safe)."""
import json, os, subprocess, sys
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
root = Path(__file__).resolve().parent.parent
cat = json.loads((root / "ArtSource/fish_catalog.json").read_text(encoding="utf-8"))["fish"]
STYLE = ("full body side view facing right, children's storybook hand-painted illustration with soft watercolor and gouache "
         "shading, gentle warm natural colours, clean readable silhouette, single fish centered and isolated on a plain white "
         "background, no text, no frame, no water")
def run(f):
    out = root / "Assets/Art/UI/Fish" / f"{f['id']}.png"
    if out.exists(): return f"skip {f['id']}"
    r = subprocess.run([sys.executable, str(root / "Tools/recraft.py"), "--out", str(out), "--seed", "4242",
                        "--prompt", f"A {f['art']}, {STYLE}"], capture_output=True, text=True)
    return (r.stdout + r.stderr).strip().splitlines()[-1] if (r.stdout + r.stderr).strip() else f"? {f['id']}"
with ThreadPoolExecutor(3) as ex:
    for line in ex.map(run, cat): print(line, flush=True)
