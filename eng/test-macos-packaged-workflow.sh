#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This packaged workflow smoke test must run on macOS." >&2
  exit 2
fi

if [[ "$#" -ne 3 ]]; then
  echo "Usage: $0 <installer-path> <release-manifest> <configuration>" >&2
  exit 2
fi

installer="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
manifest="$(cd "$(dirname "$2")" && pwd)/$(basename "$2")"
configuration="$3"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
sample_dll="$repo_root/samples/AvaScope.GettingStartedApp/bin/$configuration/net10.0/AvaScope.GettingStartedApp.dll"
test_root="$(mktemp -d "${TMPDIR:-/tmp}/avascope-macos-packaged.XXXXXX")"
install_root="$test_root/install"
bin_directory="$test_root/bin"
manifest_directory="$test_root/sessions"
preview_store="$test_root/preview-sessions"
sample_log="$test_root/sample.log"
sample_pid=""

cleanup() {
  if [[ -n "$sample_pid" ]] && kill -0 "$sample_pid" 2>/dev/null; then
    kill "$sample_pid" 2>/dev/null || true
    wait "$sample_pid" 2>/dev/null || true
  fi

  if [[ -d "$install_root" ]]; then
    "$installer" \
      --uninstall \
      --install-root "$install_root" \
      --bin-dir "$bin_directory" \
      --no-path-update \
      --no-registration >/dev/null 2>&1 || true
  fi

  rm -rf "$test_root"
}
trap cleanup EXIT

if [[ ! -f "$installer" || ! -f "$manifest" || ! -f "$sample_dll" ]]; then
  echo "Packaged workflow input is missing." >&2
  exit 1
fi

python3 - "$installer" "$manifest" <<'PY'
import hashlib
import json
import os
import sys

installer_path, manifest_path = sys.argv[1:]
with open(manifest_path, encoding="utf-8-sig") as stream:
    manifest = json.load(stream)
name = os.path.basename(installer_path)
matches = [item for item in manifest["artifacts"] if item["name"] == name]
if len(matches) != 1:
    raise SystemExit(f"manifest does not contain exactly one {name}: {matches}")
with open(installer_path, "rb") as stream:
    digest = hashlib.file_digest(stream, "sha256").hexdigest()
if digest != matches[0]["sha256"]:
    raise SystemExit(f"installer SHA-256 mismatch: expected {matches[0]['sha256']}, got {digest}")
PY

chmod u+x "$installer"
"$installer" \
  --install-root "$install_root" \
  --bin-dir "$bin_directory" \
  --no-path-update \
  --no-registration > "$test_root/install.log"

avascope="$bin_directory/avascope"
test -x "$avascope"

mkdir -p "$manifest_directory" "$preview_store"
AVASCOPE_SAMPLE_BRIDGE=1 \
AVASCOPE_BRIDGE_MANIFEST_DIR="$manifest_directory" \
dotnet "$sample_dll" > "$sample_log" 2>&1 &
sample_pid=$!

session_manifest=""
for _ in $(seq 1 150); do
  session_manifest="$(find "$manifest_directory" -maxdepth 1 -type f -name '*.json' -print -quit)"
  if [[ -n "$session_manifest" ]]; then
    break
  fi

  if ! kill -0 "$sample_pid" 2>/dev/null; then
    echo "Bridged sample exited before publishing a session manifest." >&2
    cat "$sample_log" >&2
    exit 1
  fi
  sleep 0.2
done

if [[ -z "$session_manifest" ]]; then
  echo "Timed out waiting for the packaged workflow session manifest." >&2
  exit 1
fi

session_id="$(
  python3 - "$session_manifest" <<'PY'
import json
import sys
with open(sys.argv[1], encoding="utf-8-sig") as stream:
    print(json.load(stream)["sessionId"])
PY
)"

"$avascope" attach --session "$session_id" --manifest-dir "$manifest_directory" > "$test_root/attach.json"
"$avascope" list-top-levels --session "$session_id" --manifest-dir "$manifest_directory" > "$test_root/top-levels.json"

top_level_id="$(
  python3 - "$test_root/attach.json" "$test_root/top-levels.json" <<'PY'
import json
import sys
with open(sys.argv[1], encoding="utf-8-sig") as stream:
    attach = json.load(stream)
with open(sys.argv[2], encoding="utf-8-sig") as stream:
    top_levels = json.load(stream)
if not attach.get("success") or not top_levels.get("success"):
    raise SystemExit(f"attach/top-level failed: {attach} / {top_levels}")
items = top_levels["value"]["topLevels"]
if len(items) != 1:
    raise SystemExit(f"expected one top-level: {items}")
print(items[0]["id"])
PY
)"

"$avascope" visual-tree \
  --session "$session_id" \
  --top-level "$top_level_id" \
  --max-depth 4 \
  --manifest-dir "$manifest_directory" > "$test_root/visual-tree.json"

"$avascope" screenshot \
  --session "$session_id" \
  --top-level "$top_level_id" \
  --out "$test_root/runtime-evidence.png" \
  --manifest-dir "$manifest_directory" > "$test_root/screenshot.json"

"$avascope" preview \
  "$repo_root/samples/AvaScope.GettingStartedApp/AvaScope.GettingStartedApp.csproj" \
  --view "Views/MainView.axaml" \
  --out "$test_root/preview-evidence.png" \
  --width 720 \
  --height 420 \
  --theme light \
  --design-data-type "AvaScope.GettingStartedApp.SamplePreviewData" > "$test_root/preview.json"

python3 - \
  "$test_root/visual-tree.json" \
  "$test_root/screenshot.json" \
  "$test_root/preview.json" \
  "$test_root/runtime-evidence.png" \
  "$test_root/preview-evidence.png" <<'PY'
import json
import os
import sys
for path in sys.argv[1:4]:
    with open(path, encoding="utf-8-sig") as stream:
        payload = json.load(stream)
    if not payload.get("success"):
        raise SystemExit(f"packaged command failed: {path}: {payload}")
for path in sys.argv[4:]:
    if not os.path.isfile(path) or os.path.getsize(path) == 0:
        raise SystemExit(f"missing packaged evidence artifact: {path}")
PY

"$installer" \
  --uninstall \
  --install-root "$install_root" \
  --bin-dir "$bin_directory" \
  --no-path-update \
  --no-registration >/dev/null

test ! -e "$install_root"
echo "Installed macOS attach, inspection, screenshot, preview, and uninstall workflow passed."
