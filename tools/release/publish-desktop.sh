#!/usr/bin/env bash
# Builds the Toolkit desktop installer and update packages from Linux or macOS (Velopack can
# package for Windows from there, without signing). Same steps as publish-desktop.ps1; see
# docs/RELEASE.md.
#
#   tools/release/publish-desktop.sh 1.1.0 https://license.example.com "<one-line public key>" [output-dir]
set -euo pipefail

VERSION=${1:?version, e.g. 1.1.0}
SERVER_URL=${2:?license server address}
PUBLIC_KEY=${3:?one-line public key from the Signing key page}
OUTPUT_DIR=${4:-artifacts/releases}
VPK_VERSION=${VPK_VERSION:-1.2.161}

ROOT=$(cd "$(dirname "$0")/../.." && pwd)
cd "$ROOT"
[[ $VERSION =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "Version must look like 1.2.0" >&2; exit 1; }

VPK=${VPK:-vpk}
if ! command -v "$VPK" >/dev/null 2>&1; then
  dotnet tool install -g vpk --version "$VPK_VERSION"
  VPK="$HOME/.dotnet/tools/vpk"
fi

PUBLISH=${PUBLISH_DIR:-artifacts/publish/$VERSION}
rm -rf "$PUBLISH"
dotnet publish src/ToolboxWeb.Web/ToolboxWeb.Web.csproj -c Release -r win-x64 --self-contained true -p:Version="$VERSION" -o "$PUBLISH"

python3 - "$PUBLISH/appsettings.Desktop.json" "$SERVER_URL" "$PUBLIC_KEY" <<'PY'
import json, sys
path, url, key = sys.argv[1:4]
settings = json.load(open(path, encoding="utf-8-sig"))
settings.setdefault("License", {})
settings["License"]["ServerUrl"] = url.rstrip("/")
settings["License"]["PublicKey"] = key
json.dump(settings, open(path, "w", encoding="utf-8"), indent=2)
PY
echo "This copy starts as the Toolkit desktop app (DesktopHost.FlagFileName)." > "$PUBLISH/toolkit.desktop"

# packId ToolkitApp: installed to %LOCALAPPDATA%\ToolkitApp, apart from the data folder %LOCALAPPDATA%\Toolkit.
"$VPK" "[win]" pack --packId ToolkitApp --packVersion "$VERSION" --packDir "$PUBLISH" --mainExe ToolboxWeb.Web.exe \
  --packTitle Toolkit --packAuthors Toolkit --icon docs/brand/toolkit.ico --outputDir "$OUTPUT_DIR" ${NOTES_FILE:+--releaseNotes "$NOTES_FILE"}

echo
echo "Done: $OUTPUT_DIR. Upload these files on the license server (Releases), then publish version $VERSION there."
