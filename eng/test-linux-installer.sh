#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "Usage: test-linux-installer.sh <installer-path> <expected-version>" >&2
  exit 2
fi

source_installer="$(realpath "$1")"
expected_version="$2"
test_root="$(mktemp -d "${TMPDIR:-/tmp}/avascope-installer-test.XXXXXX")"
unowned_root="$(mktemp -d "${TMPDIR:-/tmp}/avascope-unowned-test.XXXXXX")"
installer="$test_root/avascope-linux-x64-installer"
install_root="$test_root/install"
bin_directory="$test_root/bin"

cleanup() {
  rm -rf "$test_root" "$unowned_root"
}
trap cleanup EXIT

cp "$source_installer" "$installer"
chmod +x "$installer"

"$installer" --verify >/dev/null

printf 'not owned by AvaScope\n' >"$unowned_root/keep.txt"
if "$installer" \
  --uninstall \
  --install-root "$unowned_root" \
  --bin-dir "$unowned_root/bin" \
  --no-path-update \
  --no-registration >/dev/null 2>&1; then
  echo "Installer accepted an unowned uninstall root." >&2
  exit 1
fi
test -f "$unowned_root/keep.txt"

installer_arguments=(
  --install-root "$install_root"
  --bin-dir "$bin_directory"
  --no-path-update
  --no-registration
)

"$installer" "${installer_arguments[@]}" >/dev/null
test "$("$bin_directory/avascope" --version)" = "$expected_version"
test -f "$install_root/current/LICENSE"
test -f "$install_root/current/NOTICE"
test -f "$install_root/current/LICENSE-SCOPE.md"
test -f "$install_root/current/THIRD-PARTY-NOTICES.md"

"$bin_directory/avascope" doctor \
  --manifest-dir "$test_root/sessions" \
  --preview-session-store "$test_root/preview-sessions" >"$test_root/doctor.json"
grep -q '"status":"available"' "$test_root/doctor.json"

"$install_root/current/AvaScope.Mcp" </dev/null >"$test_root/mcp.stdout" 2>"$test_root/mcp.stderr"

touch "$install_root/current/stale-upgrade-sentinel.txt"
"$installer" "${installer_arguments[@]}" >/dev/null
test ! -e "$install_root/current/stale-upgrade-sentinel.txt"

"$installer" --uninstall "${installer_arguments[@]}" >/dev/null
test ! -e "$install_root"

echo "Linux installer smoke passed."
