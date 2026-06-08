# Feature Request Tickets

This directory contains sanitized feature request tickets. It is a backlog intake area, not an implementation queue.

Before committing a ticket:

1. Replace absolute local paths with placeholders.
2. Remove local usernames, emails, tokens, secrets, and machine-specific identifiers.
3. Keep enough product detail to evaluate scope and acceptance criteria later.
4. Set `Implementation Status` to `Not started` unless the feature is already covered by validated repository work.
5. Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1`.

Use file names in the form `FEAT-####-short-title.md`.
