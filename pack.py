"""Build the Hexium package zip from package/ and the Release DLL, checking Hexium's packaging rules
(https://hexium.gg/packaging). Usage: python pack.py  ->  dist/<name>-<version>.zip"""
import json, re, struct, sys, zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent
PKG = ROOT / "package"
DLL = ROOT.parent / "dist" / "PoVPlus.Compat.dll"


def check(manifest):
    errors = []
    for field in ("name", "description", "version_number", "website_url", "dependencies"):
        if field not in manifest:
            errors.append("manifest missing " + field)
    if not re.fullmatch(r"[A-Za-z0-9_]{1,128}", manifest.get("name", "")):
        errors.append("name: 1-128 letters, digits, underscores")
    if len(manifest.get("description", "")) > 256:
        errors.append("description over 256 characters")
    if not re.fullmatch(r"\d+\.\d+\.\d+(-(alpha|beta|rc)\.\d+)?", manifest.get("version_number", "")):
        errors.append("version_number must be Major.Minor.Patch")
    for dep in manifest.get("dependencies", []):
        if not re.fullmatch(r"[A-Za-z0-9_]+-[A-Za-z0-9_]+-\d+\.\d+\.\d+", dep):
            errors.append("bad dependency string: " + dep)
    with open(PKG / "icon.png", "rb") as icon:
        head = icon.read(24)
    if head[:8] != b"\x89PNG\r\n\x1a\n" or struct.unpack(">II", head[16:24]) != (256, 256):
        errors.append("icon.png must be a 256x256 PNG")
    for required in ("README.md", "icon.png"):
        if not (PKG / required).is_file():
            errors.append("missing " + required)
    if not DLL.is_file():
        errors.append("build first: missing " + str(DLL))
    return errors


def main():
    manifest = json.loads((PKG / "manifest.json").read_text(encoding="utf-8"))
    errors = check(manifest)
    if errors:
        sys.exit("Package invalid:\n  " + "\n  ".join(errors))
    out = ROOT.parent / "dist" / "{}-{}.zip".format(manifest["name"], manifest["version_number"])
    out.parent.mkdir(exist_ok=True)
    files = [PKG / n for n in ("manifest.json", "icon.png", "README.md", "CHANGELOG.md", "LICENSE")] + [DLL]
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as zf:
        for f in files:
            zf.write(f, f.name)  # Hexium wants every file at the archive root
    print(out)


if __name__ == "__main__":
    main()
