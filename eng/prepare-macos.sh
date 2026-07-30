#!/usr/bin/env bash
set -euo pipefail

artifact_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

for executable in avascope AvaScope.Mcp AvaScope.PreviewHost; do
  executable_path="$artifact_dir/$executable"
  if [[ ! -f "$executable_path" ]]; then
    echo "Missing AvaScope macOS executable: $executable_path" >&2
    exit 1
  fi

  chmod u+x "$executable_path"
done

echo "Prepared AvaScope macOS executables in: $artifact_dir"
