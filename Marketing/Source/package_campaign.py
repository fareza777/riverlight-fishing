"""Package final assets and editable Remotion source, without local dependencies or private saves."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import json

root = Path(__file__).resolve().parents[1]
report_file = root / "Deliverables" / "validation-report.json"
report = json.loads(report_file.read_text(encoding="utf-8"))
assert report["passed"] and len(report["videos"]) == 2 and len(report["images"]) == 14

files = []
for p in (root / "Deliverables").rglob("*"):
    if p.is_file() and "Previews/Video/" not in p.relative_to(root).as_posix():
        files.append(p)
for subfolder in ("src", "public", "scripts"):
    for p in (root / "Remotion" / subfolder).rglob("*"):
        if p.is_file() and p.name not in ("progress.txt", "previous-graphics.txt"):
            files.append(p)
for name in ("package.json", "package-lock.json", "tsconfig.json", "remotion.config.ts", "eslint.config.mjs", ".prettierrc", ".gitignore", "README.md"):
    p = root / "Remotion" / name
    if p.is_file():
        files.append(p)
files.extend([root / "CREATIVE-DIRECTION.md", root / "Source" / "IMAGE-PROMPTS.md", root / "Source" / "CaptureCampaign.cs"])

destination = root / "Riverlight-English-Launch-Kit.zip"
with ZipFile(destination, "w", ZIP_DEFLATED, compresslevel=5) as archive:
    for p in sorted(set(files)):
        archive.write(p, "Riverlight-English-Launch-Kit/" + p.relative_to(root).as_posix())
    archive.comment = b"Riverlight English launch assets, two 30-second trailers and editable Remotion source."
with ZipFile(destination) as archive:
    bad = archive.testzip()
    assert bad is None, f"ZIP integrity check failed: {bad}"
    names = archive.namelist()
    assert not any("node_modules/" in n or ".render-bundle/" in n or "riverlight_save" in n for n in names)
    assert sum("Deliverables/PlayStore/Screenshots/" in n for n in names) == 8
    assert sum(n.endswith(".mp4") and "/Deliverables/Video/" in n for n in names) == 2
print(json.dumps({"archive": str(destination), "files": len(files), "bytes": destination.stat().st_size, "integrity": "passed"}, indent=2))
