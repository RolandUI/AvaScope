#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This artifact smoke test must run on macOS." >&2
  exit 2
fi

if [[ "$#" -ne 3 ]]; then
  echo "Usage: $0 <artifact-zip> <expected-version> <configuration>" >&2
  exit 2
fi

artifact_zip="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
expected_version="$2"
configuration="$3"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
test_root="$(mktemp -d "${TMPDIR:-/tmp}/avascope-macos-artifact.XXXXXX")"

cleanup() {
  rm -rf "$test_root"
}
trap cleanup EXIT

if [[ ! -f "$artifact_zip" ]]; then
  echo "macOS artifact ZIP does not exist: $artifact_zip" >&2
  exit 1
fi

ditto -x -k "$artifact_zip" "$test_root/package"
bash "$test_root/package/prepare-macos.sh"

actual_version="$("$test_root/package/avascope" --version)"
if [[ "$actual_version" != "$expected_version" ]]; then
  echo "Packaged CLI version mismatch. Expected $expected_version, got $actual_version." >&2
  exit 1
fi

"$test_root/package/avascope" doctor \
  --manifest-dir "$test_root/sessions" \
  --preview-session-store "$test_root/preview-sessions" \
  > "$test_root/doctor.json"

python3 - "$test_root/doctor.json" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8-sig") as stream:
    payload = json.load(stream)
if not payload.get("success") or payload["value"]["status"] != "available":
    raise SystemExit(f"packaged doctor failed: {payload}")
PY

AVASCOPE_PACKAGED_MCP_ASSEMBLY="$test_root/package/AvaScope.Mcp.dll" \
  dotnet test "$repo_root/tests/AvaScope.Tests/AvaScope.Tests.csproj" \
  -c "$configuration" \
  --no-build \
  --filter FullyQualifiedName~ServerStartsOverStdioAndListsInitialTools

echo "Packaged macOS CLI version, doctor, and MCP stdio smoke passed."
