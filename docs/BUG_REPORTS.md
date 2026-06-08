# AvaScope Bug Reports

This ledger stores user-reported defects before they are selected for implementation.
Reports in this area are intake records only; do not start a fix unless the user explicitly asks for that bug to be fixed.

## Storage Rules

- Store each report under `docs/bug-reports/BUG-####-short-title.md`.
- Remove local usernames, machine names, personal directories, absolute paths, emails, tokens, and secrets before commit.
- Replace sensitive paths with placeholders such as `<avascope-root>`, `<target-app-root>`, and `<output-root>`.
- Preserve the technical symptom, command shape, error code, exception type, affected view kind, and expected behavior.
- Keep `Fix Status` explicit. New reports default to `Not started`.
- Run `eng/validate-bug-reports.ps1` before committing bug report changes.

## Reports

| ID | Title | Status | Fix Status | Stored |
| --- | --- | --- | --- | --- |
| [BUG-0001](bug-reports/BUG-0001-previewhost-window-root.md) | PreviewHost fails for Window-rooted AXAML previews | Stored | Not started | 2026-06-08 |
| [BUG-0002](bug-reports/BUG-0002-previewhost-design-time-datacontext.md) | PreviewHost ignores Avalonia design-time DataContext metadata | Stored | Not started | 2026-06-08 |
