# Bug Report Intake

This directory contains sanitized user bug reports. It is a holding area, not an implementation queue.

Before committing a report:

1. Replace absolute local paths with placeholders.
2. Remove local usernames, emails, tokens, secrets, and machine-specific identifiers.
3. Keep enough technical detail to reproduce the failure later.
4. Set `Fix Status` to `Not started` unless the user has explicitly requested a fix.
5. Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1`.

Use file names in the form `BUG-####-short-title.md`.
