# Contributing to AvaScope

AvaScope is developed primarily by autonomous coding agents. Human bug reports, design feedback and suggested improvements are welcome through issues. Development uses direct commits and pushes to `master`; task branches, worktrees and pull requests are not part of the workflow. Agents remain responsible for implementation, validation, repository workflow, and release handoff.

## Before You Start

- Search existing issues before opening a new one.
- Use an issue to agree on non-trivial behavior or public-surface changes before implementation.
- Keep changes narrowly scoped and avoid unrelated refactoring.
- Follow the selected issue, milestone, and [project workflow](docs/GITHUB_PROJECT_WORKFLOW.md).
- Report security problems through [SECURITY.md](SECURITY.md), not a public issue.

## Development

Install the .NET 10 SDK. For normal development, build and test the affected components and dependents using the [risk-based validation rules](docs/VALIDATION.md#choose-validation-by-risk). A full local solution check, when justified, uses:

```powershell
dotnet restore AvaScope.slnx
dotnet build AvaScope.slnx
dotnet test AvaScope.slnx
```

Windows installer packaging additionally requires Inno Setup. Linux installer packaging is validated on Linux or WSL. See [docs/VALIDATION.md](docs/VALIDATION.md) for focused and release-level validation commands.

Every meaningful change should include relevant tests, or a clear validation note when automated testing does not apply. Update user and stable-surface documentation when behavior, commands, protocols, packages, or artifacts change.

## Published Information

Follow the [GitHub information policy](AGENTS.md#github-information-policy) for all outgoing text and evidence, and the [information ownership rules](docs/GITHUB_PROJECT_WORKFLOW.md#information-ownership) to avoid duplicate records.

Small documentation or policy corrections explicitly requested by the owner do not need a new tracking issue when there is no product behavior change. Record the result and focused validation with the commit; see the [project workflow](docs/GITHUB_PROJECT_WORKFLOW.md) for when issue updates are needed.

## Direct Commits

Use the [development loop](docs/GITHUB_PROJECT_WORKFLOW.md#development-loop) for direct `master` commits, validation, acceptance and concise issue updates.

## License

By contributing, you agree that your contribution is licensed under the [Apache License 2.0](LICENSE). AvaScope does not currently require a Contributor License Agreement.
