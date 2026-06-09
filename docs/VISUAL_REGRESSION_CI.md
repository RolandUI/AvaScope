# Visual Regression CI

Use `baseline-check --report` for CI and collect the generated report, current images, and diff images as one uploadable artifact directory.

The baseline commands keep their local behavior:

- `baseline-create` writes the baseline manifest and baseline images.
- `baseline-check` renders current images, writes diff images, prints the structured result to stdout, and exits non-zero when a variant changed.
- `--report <report.json>` adds a stable JSON report file without changing the check result or exit code.

## Artifact Collection

Run the helper after `baseline-check --report`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\collect-baseline-artifacts.ps1 `
  -Report .\artifacts\visual-regression\report\baseline-check.json `
  -OutDir .\artifacts\visual-regression\upload
```

The helper copies:

- `report\baseline-check.json`
- `current\*.png`
- `diff\*.png`
- `artifact-manifest.json`

The helper fails if the report references an image that does not exist.

## GitHub Actions Example

```yaml
- name: Run AvaScope visual baseline check
  shell: pwsh
  run: |
    $avascope = ".\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe"
    $report = ".\artifacts\visual-regression\report\baseline-check.json"

    & $avascope baseline-check `
      --manifest .\baselines\main.json `
      --out-dir .\artifacts\visual-regression\current `
      --diff-dir .\artifacts\visual-regression\diff `
      --report $report `
      --tolerance 2

    $baselineExitCode = $LASTEXITCODE

    if (Test-Path -LiteralPath $report) {
      powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\collect-baseline-artifacts.ps1 `
        -Report $report `
        -OutDir .\artifacts\visual-regression\upload
    }

    exit $baselineExitCode

- name: Upload AvaScope visual regression artifacts
  if: always()
  uses: actions/upload-artifact@v4
  with:
    name: avascope-visual-regression
    path: artifacts/visual-regression/upload
```

The upload step uses `if: always()` so changed baselines still publish the report and images for review. The baseline step preserves the original `baseline-check` exit code after collecting artifacts.
