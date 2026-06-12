# Visual Regression CI

Use `baseline-check --report-pack` for CI and upload the generated review pack with the current and diff image directories.

The baseline commands keep their local behavior:

- `baseline-create` writes the baseline manifest and baseline images.
- `baseline-check` renders current images, writes diff images, prints the structured result to stdout, and exits non-zero when a variant changed.
- `--report <report.json>` adds a stable JSON report file without changing the check result or exit code.
- `--report-pack <dir>` writes JSON, HTML, JUnit XML, and SARIF-style summaries for agent and pull-request review.

## Artifact Collection

Prefer direct report-pack upload:

```powershell
$report = ".\artifacts\visual-regression\report\baseline-check.json"
$reportPack = ".\artifacts\visual-regression\report-pack"

& $avascope baseline-check `
  --manifest .\baselines\main.json `
  --out-dir .\artifacts\visual-regression\current `
  --diff-dir .\artifacts\visual-regression\diff `
  --report $report `
  --report-pack $reportPack `
  --tolerance 2
```

The report pack writes:

- `baseline-report.json`
- `baseline-report.html`
- `baseline-junit.xml`
- `baseline.sarif.json`

The CLI response includes `reportPack.status`, pass/fail counts, environment metadata, and asset paths. It does not inline screenshots or large report payloads. For legacy JSON-only workflows, `eng\collect-baseline-artifacts.ps1` can still collect `--report`, current images, and diff images into one helper output directory.

## GitHub Actions Example

```yaml
- name: Run AvaScope visual baseline check
  shell: pwsh
  run: |
    $avascope = ".\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe"
    $report = ".\artifacts\visual-regression\report\baseline-check.json"
    $reportPack = ".\artifacts\visual-regression\report-pack"

    & $avascope baseline-check `
      --manifest .\baselines\main.json `
      --out-dir .\artifacts\visual-regression\current `
      --diff-dir .\artifacts\visual-regression\diff `
      --report $report `
      --report-pack $reportPack `
      --tolerance 2

    $baselineExitCode = $LASTEXITCODE

    exit $baselineExitCode

- name: Upload AvaScope visual regression artifacts
  if: always()
  uses: actions/upload-artifact@v4
  with:
    name: avascope-visual-regression
    path: |
      artifacts/visual-regression/report
      artifacts/visual-regression/report-pack
      artifacts/visual-regression/current
      artifacts/visual-regression/diff
```

The upload step uses `if: always()` so changed baselines still publish report assets and images for review. The baseline step preserves the original `baseline-check` exit code.
