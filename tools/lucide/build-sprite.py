"""Build Content/vendor/lucide/lucide-sprite.svg from a lucide-static package.

Usage:
    npm pack lucide-static@<version>   (in an empty temp folder)
    tar xzf lucide-static-<version>.tgz
    python -I build-sprite.py <path to the extracted "package" folder>

The sprite holds only the icons listed in icons.txt. Each <symbol> carries the stroke
attributes, so <use> picks up currentColor without extra CSS. LICENSE is copied next to it.
"""
import json
import re
import shutil
import sys
from pathlib import Path

here = Path(__file__).resolve().parent
package = Path(sys.argv[1]).resolve()
target = here.parent.parent / "src" / "ToolboxWeb.Web" / "Content" / "vendor" / "lucide"
version = json.loads((package / "package.json").read_text(encoding="utf-8"))["version"]

names = [line.strip() for line in (here / "icons.txt").read_text(encoding="utf-8").splitlines()
         if line.strip() and not line.startswith("#")]

symbols = []
for name in names:
    svg = (package / "icons" / f"{name}.svg").read_text(encoding="utf-8")
    body = re.search(r"<svg[^>]*>(.*)</svg>", svg, re.S).group(1)
    body = "".join(part.strip() for part in body.splitlines())
    symbols.append(
        f'<symbol id="{name}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" '
        f'stroke-linecap="round" stroke-linejoin="round">{body}</symbol>')

target.mkdir(parents=True, exist_ok=True)
(target / "lucide-sprite.svg").write_text(
    f"<!-- Lucide icons (lucide-static v{version}), ISC License; see LICENSE in this folder. -->\n"
    '<svg xmlns="http://www.w3.org/2000/svg">\n'
    + "\n".join(symbols) + "\n</svg>\n", encoding="utf-8", newline="\n")
shutil.copyfile(package / "LICENSE", target / "LICENSE")
print(f"{len(symbols)} icons from lucide-static v{version} -> {target / 'lucide-sprite.svg'}")
