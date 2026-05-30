#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET="dotnet"
if command -v godot4 > /dev/null 2>&1; then
    GODOT="godot4"
elif command -v godot > /dev/null 2>&1; then
    GODOT="godot"
else
    echo "Error: neither godot4 nor godot was found in PATH."
    exit 1
fi
BUILD_ROOT="$ROOT_DIR/build"
RELEASE_DIR="$BUILD_ROOT/RemoveMultiplayerPlayerLimit"
DLL_SOURCE="$ROOT_DIR/.godot/mono/temp/bin/Release/RemoveMultiplayerPlayerLimit.dll"
PCK_SOURCE="$BUILD_ROOT/RemoveMultiplayerPlayerLimit.pck"
MANIFEST_PATH_BETA="$ROOT_DIR/RemoveMultiplayerPlayerLimit.json"
REUSE_EXISTING_PCK="${RMP_REUSE_EXISTING_PCK:-false}"

DOTNET_ROLL_FORWARD=Major "$DOTNET" build "$ROOT_DIR/RemoveMultiplayerPlayerLimit.csproj" -c Release
GODOT_VERSION="$("$GODOT" --version | head -n 1)"
if [[ "$GODOT_VERSION" == 4.5.* ]]; then
    "$GODOT" --headless --path "$ROOT_DIR" --script "res://tools/build_pck.gd"
elif [[ "$REUSE_EXISTING_PCK" == true && -f "$PCK_SOURCE" ]]; then
    echo "Warning: local Godot is '$GODOT_VERSION'; reusing existing PCK at $PCK_SOURCE."
    echo "Install Godot 4.5.x to rebuild the PCK for STS2."
else
    echo "Error: local Godot is '$GODOT_VERSION', but STS2 requires a 4.5-compatible PCK."
    echo "Install Godot 4.5.x, or set RMP_REUSE_EXISTING_PCK=true to package the existing $PCK_SOURCE."
    exit 1
fi

mkdir -p "$RELEASE_DIR"
rm -rf "$RELEASE_DIR"/*
rm -f "$BUILD_ROOT"/sts2-RMP-*.zip

cp "$DLL_SOURCE" "$RELEASE_DIR/RemoveMultiplayerPlayerLimit.dll"
cp "$PCK_SOURCE" "$RELEASE_DIR/RemoveMultiplayerPlayerLimit.pck"
if [ -f "$MANIFEST_PATH_BETA" ]; then
    cp "$MANIFEST_PATH_BETA" "$RELEASE_DIR/RemoveMultiplayerPlayerLimit.json"
fi

if ! command -v jq &> /dev/null; then
    echo "Error: jq is required to parse RemoveMultiplayerPlayerLimit.json. Please install it (e.g., sudo apt install jq)."
    exit 1
fi
if ! command -v python3 &> /dev/null; then
    echo "Error: python3 is required to create the release archive."
    exit 1
fi
VERSION=$(jq -r '.version // empty' "$MANIFEST_PATH_BETA")
MOD_FOLDER_NAME=$(jq -r 'if .pck_name and .pck_name != "" then .pck_name else .name end' "$MANIFEST_PATH_BETA")
if [ -z "$VERSION" ]; then
    echo "Error: RemoveMultiplayerPlayerLimit.json missing version field"
    exit 1
fi
if [ -z "$MOD_FOLDER_NAME" ]; then
    echo "Error: RemoveMultiplayerPlayerLimit.json missing name/pck_name field"
    exit 1
fi

ZIP_NAME="sts2-RMP-$VERSION.zip"
ZIP_PATH="$BUILD_ROOT/$ZIP_NAME"
ZIP_STAGE_ROOT="$BUILD_ROOT/_zip_stage"
ZIP_MOD_FOLDER="$ZIP_STAGE_ROOT/$MOD_FOLDER_NAME"

rm -f "$ZIP_PATH"
rm -rf "$ZIP_STAGE_ROOT"

mkdir -p "$ZIP_MOD_FOLDER"
cp -r "$RELEASE_DIR"/* "$ZIP_MOD_FOLDER/"

python3 - "$ZIP_STAGE_ROOT" "$ZIP_PATH" "$MOD_FOLDER_NAME" <<'PY'
from pathlib import Path
import sys
import zipfile

stage_root = Path(sys.argv[1])
zip_path = Path(sys.argv[2])
mod_folder = sys.argv[3]

with zipfile.ZipFile(zip_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
    for path in sorted((stage_root / mod_folder).rglob("*")):
        if path.is_file():
            archive.write(path, path.relative_to(stage_root))
PY
rm -rf "$ZIP_STAGE_ROOT"

echo "Release package created: $ZIP_PATH"
