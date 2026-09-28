# AvaScope Development Plan

This is a publishable checkpoint, not an execution log. GitHub issues and milestones own scope, acceptance and detailed results. Follow the [project workflow](GITHUB_PROJECT_WORKFLOW.md) and [GitHub information policy](../AGENTS.md#github-information-policy); keep machine-specific working state outside tracked files.

## Current release

Stable [v1.5.1](https://github.com/RolandUI/AvaScope/releases/tag/v1.5.1) is published at `5c9cad38161607d64290073293597634319412f7`. [Release #165](https://github.com/RolandUI/AvaScope/issues/165) and [milestone v1.5.1](https://github.com/RolandUI/AvaScope/milestone/20) are closed.

The [stable release gate](https://github.com/RolandUI/AvaScope/actions/runs/36415197323) passed: 1189 tests passed, eight skipped, zero failed; build, installer, package and publication checks passed. This is the existing release validation, not a new test run for later documentation changes.

## Remaining work

No implementation issue is currently in progress. Follow-up scope remains in the unversioned backlog:

- [#166: stabilization campaign](https://github.com/RolandUI/AvaScope/issues/166).
- [#174: comprehensive capability coverage](https://github.com/RolandUI/AvaScope/issues/174).
- [#164: packaged MCP and native Retina validation](https://github.com/RolandUI/AvaScope/issues/164).

Eleven investigations still have unresolved original timing, IPC, build or file-lock causes. Later passing checks do not establish those causes or justify closure. #157/#161 were accepted by the owner after Mac RC testing; no new independent agent Retina measurement is claimed. Publication does not establish complete coverage or a bug-free product.

Next implementation work should select one actionable issue from GitHub and follow its current acceptance criteria. Reuse the local QA environment, perform focused validation, and run one complete gate per coherent implementation batch; do not repeat unchanged passing suites merely to close an unexplained failure.
