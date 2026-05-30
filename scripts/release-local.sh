#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage:
  scripts/release-local.sh [--package-only] vX.Y.Z

Examples:
  scripts/release-local.sh --package-only v0.1.8
  scripts/release-local.sh v0.1.8
USAGE
}

die() {
  printf 'release-local: %s\n' "$*" >&2
  exit 1
}

info() {
  printf 'release-local: %s\n' "$*"
}

need_command() {
  command -v "$1" >/dev/null 2>&1 || die "missing required command: $1"
}

json_field() {
  local path="$1"
  local field="$2"
  jq -r --arg field "$field" '.[$field] // empty' "$path"
}

verify_main_synced() {
  git fetch origin main:refs/remotes/origin/main --tags

  local local_main
  local remote_main
  local_main="$(git rev-parse main)"
  remote_main="$(git rev-parse origin/main)"
  [[ "$local_main" == "$remote_main" ]] || die "local main is not synced with origin/main"
}

package_only=false

if [[ $# -eq 2 && "$1" == "--package-only" ]]; then
  package_only=true
  tag="$2"
elif [[ $# -eq 1 ]]; then
  tag="$1"
else
  usage >&2
  exit 2
fi

if [[ ! "$tag" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  die "tag must match v<major>.<minor>.<patch>, got '$tag'"
fi

version="${tag#v}"

need_command git
need_command dotnet
need_command godot
need_command jq
need_command python3

repo_root="$(git rev-parse --show-toplevel 2>/dev/null)" || die "not inside a git repository"
cd "$repo_root"

manifest_path="RemoveMultiplayerPlayerLimit.json"
zip_path="build/sts2-RMP-$version.zip"

[[ -f "$manifest_path" ]] || die "missing $manifest_path"
[[ -x tools/build_release.sh ]] || die "tools/build_release.sh must be executable"

manifest_version="$(json_field "$manifest_path" version)"
if [[ "$manifest_version" != "$version" ]]; then
  die "$manifest_path version '$manifest_version' does not match tag '$tag'"
fi

if [[ "$package_only" == false ]]; then
  need_command gh

  if ! gh auth status >/dev/null 2>&1; then
    die "GitHub CLI is not authenticated; run 'gh auth login'"
  fi

  current_branch="$(git rev-parse --abbrev-ref HEAD)"
  [[ "$current_branch" == "main" ]] || die "full release must run from main, currently on '$current_branch'"

  if [[ -n "$(git status --porcelain)" ]]; then
    die "full release requires a clean worktree"
  fi

  verify_main_synced

  if git rev-parse -q --verify "refs/tags/$tag" >/dev/null; then
    die "local tag already exists: $tag"
  fi

  if git ls-remote --exit-code --tags origin "refs/tags/$tag" >/dev/null 2>&1; then
    die "remote tag already exists: $tag"
  fi

  if gh release view "$tag" >/dev/null 2>&1; then
    die "GitHub Release already exists for $tag; inspect it with 'gh release view $tag'"
  fi
fi

info "building release package"
tools/build_release.sh

[[ -f "$zip_path" ]] || die "expected package at $zip_path"

info "verifying zip contents"
python3 - "$zip_path" <<'PY'
from pathlib import Path
import sys
import zipfile

zip_path = Path(sys.argv[1])
expected = {
    "RemoveMultiplayerPlayerLimit/RemoveMultiplayerPlayerLimit.json",
    "RemoveMultiplayerPlayerLimit/RemoveMultiplayerPlayerLimit.dll",
    "RemoveMultiplayerPlayerLimit/RemoveMultiplayerPlayerLimit.pck",
}

with zipfile.ZipFile(zip_path, "r") as archive:
    actual = {item.filename for item in archive.infolist() if not item.is_dir()}

if actual != expected:
    print(f"unexpected zip contents in {zip_path}", file=sys.stderr)
    print("expected:", file=sys.stderr)
    for name in sorted(expected):
        print(f"  {name}", file=sys.stderr)
    print("actual:", file=sys.stderr)
    for name in sorted(actual):
        print(f"  {name}", file=sys.stderr)
    sys.exit(1)
PY

if [[ "$package_only" == true ]]; then
  info "package-only release artifact ready: $zip_path"
  exit 0
fi

if [[ -n "$(git status --porcelain)" ]]; then
  die "full release requires a clean worktree before tagging"
fi

verify_main_synced

info "creating annotated tag $tag"
git tag -a "$tag" -m "Release $tag"

info "pushing tag $tag"
git push origin "$tag"

info "creating GitHub Release $tag"
if ! gh release create "$tag" "$zip_path" --title "Remove Multiplayer Player Limit $tag" --generate-notes; then
  cat >&2 <<RECOVERY
release-local: tag '$tag' was pushed, but GitHub Release creation failed.
release-local: inspect the tag with:
release-local:   git ls-remote --tags origin refs/tags/$tag
release-local: retry release creation with:
release-local:   gh release create $tag $zip_path --title "Remove Multiplayer Player Limit $tag" --generate-notes
RECOVERY
  exit 1
fi

release_url="$(gh release view "$tag" --json url --jq .url)"
info "GitHub Release published: $release_url"
info "uploaded asset: $zip_path"
