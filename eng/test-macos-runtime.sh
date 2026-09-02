#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This smoke test must run on macOS." >&2
  exit 2
fi

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${1:-Release}"
cli_dll="$repo_root/src/AvaScope.Cli/bin/$configuration/net10.0/avascope.dll"
sample_dll="$repo_root/samples/AvaScope.GettingStartedApp/bin/$configuration/net10.0/AvaScope.GettingStartedApp.dll"
test_root="$(mktemp -d "${TMPDIR:-/tmp}/avascope-macos-runtime.XXXXXX")"
manifest_dir="$test_root/sessions"
preview_store="$test_root/preview-sessions"
sample_log="$test_root/sample.log"
sample_pid=""

cleanup() {
  if [[ -n "$sample_pid" ]] && kill -0 "$sample_pid" 2>/dev/null; then
    kill "$sample_pid" 2>/dev/null || true
    wait "$sample_pid" 2>/dev/null || true
  fi

  rm -rf "$test_root"
}
trap cleanup EXIT

if [[ ! -f "$cli_dll" ]]; then
  echo "CLI assembly does not exist: $cli_dll" >&2
  exit 1
fi

if [[ ! -f "$sample_dll" ]]; then
  echo "Sample assembly does not exist: $sample_dll" >&2
  exit 1
fi

mkdir -p "$manifest_dir" "$preview_store"

expected_version="$(
  sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$repo_root/Directory.Build.props"
)"
actual_version="$(dotnet "$cli_dll" --version)"
if [[ "$actual_version" != "$expected_version" ]]; then
  echo "CLI version mismatch. Expected $expected_version, got $actual_version." >&2
  exit 1
fi

dotnet "$cli_dll" doctor \
  --manifest-dir "$manifest_dir" \
  --preview-session-store "$preview_store" \
  > "$test_root/doctor.json"

python3 - "$test_root/doctor.json" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8-sig") as stream:
    payload = json.load(stream)
if not payload.get("success"):
    raise SystemExit(f"doctor failed: {payload}")
if payload["value"]["status"] != "available":
    raise SystemExit(f"doctor status was not available: {payload}")
PY

AVASCOPE_SAMPLE_BRIDGE=1 \
AVASCOPE_BRIDGE_MANIFEST_DIR="$manifest_dir" \
dotnet "$sample_dll" > "$sample_log" 2>&1 &
sample_pid=$!

manifest_path=""
for _ in $(seq 1 150); do
  manifest_path="$(find "$manifest_dir" -maxdepth 1 -type f -name '*.json' -print -quit)"
  if [[ -n "$manifest_path" ]]; then
    break
  fi

  if ! kill -0 "$sample_pid" 2>/dev/null; then
    echo "Bridged sample exited before publishing a session manifest." >&2
    cat "$sample_log" >&2
    exit 1
  fi

  sleep 0.2
done

if [[ -z "$manifest_path" ]]; then
  echo "Timed out waiting for the bridged sample session manifest." >&2
  cat "$sample_log" >&2
  exit 1
fi

session_id="$(
  python3 - "$manifest_path" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8-sig") as stream:
    print(json.load(stream)["sessionId"])
PY
)"

dotnet "$cli_dll" attach \
  --session "$session_id" \
  --manifest-dir "$manifest_dir" \
  > "$test_root/attach.json"

dotnet "$cli_dll" list-top-levels \
  --session "$session_id" \
  --manifest-dir "$manifest_dir" \
  > "$test_root/top-levels.json"

python3 - "$test_root/top-levels.json" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8-sig") as stream:
    payload = json.load(stream)
items = payload.get("value", {}).get("topLevels", [])
if items:
    print(f"macOS runtime RenderScaling: {items[0].get('renderScaling')}")
PY

top_level_id="$(
  python3 - "$test_root/attach.json" "$test_root/top-levels.json" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8-sig") as stream:
    attach = json.load(stream)
if not attach.get("success"):
    raise SystemExit(f"attach failed: {attach}")

with open(sys.argv[2], encoding="utf-8-sig") as stream:
    top_levels = json.load(stream)
if not top_levels.get("success"):
    raise SystemExit(f"list-top-levels failed: {top_levels}")
items = top_levels["value"]["topLevels"]
if len(items) != 1:
    raise SystemExit(f"expected one sample top-level, got: {items}")
if items[0]["title"] != "AvaScope Getting Started":
    raise SystemExit(f"unexpected sample top-level: {items[0]}")
print(items[0]["id"])
PY
)"

dotnet "$cli_dll" visual-tree \
  --session "$session_id" \
  --top-level "$top_level_id" \
  --max-depth 4 \
  --manifest-dir "$manifest_dir" \
  > "$test_root/visual-tree.json"

dotnet "$cli_dll" screenshot \
  --session "$session_id" \
  --top-level "$top_level_id" \
  --out "$test_root/runtime.png" \
  --manifest-dir "$manifest_dir" \
  > "$test_root/screenshot.json"

dotnet "$cli_dll" preview \
  "$repo_root/samples/AvaScope.GettingStartedApp/AvaScope.GettingStartedApp.csproj" \
  --view "Views/MainView.axaml" \
  --out "$test_root/preview.png" \
  --width 720 \
  --height 420 \
  --theme light \
  --design-data-type "AvaScope.GettingStartedApp.SamplePreviewData" \
  > "$test_root/preview.json"

set +e
dotnet "$cli_dll" native-picker \
  --session "$session_id" \
  --operation detect \
  --manifest-dir "$manifest_dir" \
  > "$test_root/native-picker.json"
native_picker_exit=$?
set -e

if [[ "$native_picker_exit" -eq 0 ]]; then
  echo "Windows-only native picker unexpectedly succeeded on macOS." >&2
  exit 1
fi

python3 - \
  "$test_root/visual-tree.json" \
  "$test_root/screenshot.json" \
  "$test_root/preview.json" \
  "$test_root/native-picker.json" \
  "$test_root/runtime.png" \
  "$test_root/preview.png" <<'PY'
import json
import os
import sys

for path in sys.argv[1:4]:
    with open(path, encoding="utf-8-sig") as stream:
        payload = json.load(stream)
    if not payload.get("success"):
        raise SystemExit(f"command failed: {path}: {payload}")

with open(sys.argv[4], encoding="utf-8-sig") as stream:
    picker = json.load(stream)
if picker.get("success"):
    raise SystemExit(f"native picker unexpectedly succeeded: {picker}")
details = picker["error"].get("details") or {}
if not details.get("platform"):
    raise SystemExit(f"native picker did not report the current platform: {picker}")
if "only on Windows" not in picker["error"]["message"]:
    raise SystemExit(f"native picker did not report its Windows-only boundary: {picker}")

for path in sys.argv[5:]:
    if not os.path.isfile(path) or os.path.getsize(path) == 0:
        raise SystemExit(f"expected non-empty PNG artifact: {path}")
PY

echo "macOS runtime, bridge, screenshot, preview, and platform-boundary smoke passed."
