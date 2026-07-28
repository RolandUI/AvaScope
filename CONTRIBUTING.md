# Contributing to AvaScope

AvaScope is developed primarily by autonomous coding agents. Human bug reports, design feedback, documentation improvements, and pull requests are welcome. Agents remain responsible for implementation, validation, repository workflow, and release handoff.

## Before You Start

- Search existing issues before opening a new one.
- Use an issue to agree on non-trivial behavior or public-surface changes before implementation.
- Keep changes narrowly scoped and avoid unrelated refactoring.
- Follow the selected issue, milestone, and [project workflow](docs/GITHUB_PROJECT_WORKFLOW.md).
- Report security problems through [SECURITY.md](SECURITY.md), not a public issue.

## Development

Install the .NET 10 SDK, then run:

```powershell
dotnet restore AvaScope.slnx
dotnet build AvaScope.slnx
dotnet test AvaScope.slnx
```

Windows installer packaging additionally requires Inno Setup. Linux installer packaging is validated on Linux or WSL. See [docs/VALIDATION.md](docs/VALIDATION.md) for focused and release-level validation commands.

Every meaningful change should include relevant tests, or a clear validation note when automated testing does not apply. Update user and stable-surface documentation when behavior, commands, protocols, packages, or artifacts change.

## Pull Requests

Pull requests run the repository CI workflow with read-only repository permissions. Do not add workflows that expose secrets to pull-request code, and do not use `pull_request_target` to execute contributed code.

Describe the related issue, exact behavior changed, validation performed, and any compatibility or security decision. A contribution may be revised by an autonomous agent before merge to keep repository conventions and release guarantees consistent.

## License

By contributing, you agree that your contribution is licensed under the [Apache License 2.0](LICENSE). AvaScope does not currently require a Contributor License Agreement.
