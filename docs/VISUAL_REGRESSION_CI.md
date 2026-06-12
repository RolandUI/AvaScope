# Visual Regression CI

Use `baseline-check --report-pack` for CI and upload the generated review pack with the current and diff image directories. A complete copy-paste starting point is available at [docs/examples/github-actions/avascope-visual-regression.yml](examples/github-actions/avascope-visual-regression.yml).

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

The sample workflow is intentionally not installed under `.github/workflows/` so it does not change this repository's CI behavior. Copy it into a consuming repository as `.github/workflows/avascope-visual-regression.yml`, then set `baseline_manifest` to a committed AvaScope baseline manifest. If the project uses suite manifests, generate and commit the expanded baseline manifest with `baseline-create --suite <suite.json> --manifest <baseline.json>` before enabling the check.

Minimal job shape:

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

    if (-not (Test-Path -LiteralPath (Join-Path $reportPack "baseline-report.json"))) {
      throw "AvaScope did not produce baseline-report.json."
    }

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

The upload step uses `if: always()` so changed baselines still publish report assets and images for review. The baseline step preserves the original `baseline-check` exit code, so changed variants fail the job while still leaving reviewable artifacts.

## Review And Failure Semantics

- A passing baseline check exits `0` and uploads the report pack, current images, and diff images for auditability.
- A changed baseline exits non-zero, but the upload step still runs; review `report-pack/baseline-report.html` first, then inspect `report-pack/baseline-report.json` or `baseline-junit.xml` for machine-readable failure details.
- Missing or invalid manifests fail before meaningful artifacts exist; the workflow should treat that as setup failure, not a visual-regression failure.
- Do not update committed baselines from this CI job. Refreshing baselines should be an explicit local or reviewed workflow, not an automatic pull-request side effect.

## Release Workflow Separation

This visual-regression workflow is separate from AvaScope release publishing:

- It uses `permissions: contents: read`.
- It does not require `NUGET_API_KEY`, `packages: write`, or `contents: write`.
- It does not call `eng\publish-nuget.ps1` or `eng\publish-github-release.ps1`.
- It may build a local AvaScope CLI package with `eng\create-local-release.ps1 -SkipTests`, but that only creates local artifacts for the job.
