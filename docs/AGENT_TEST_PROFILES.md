# Agent test profiles

Store shared build/launch recipes in a JSON file with `schemaVersion: 1` and a `profiles` object. A profile resolves into the existing `RuntimeScenarioRequest`; it has no separate execution engine. Existing explicit `run-scenario --request` and MCP `run_scenario(request)` calls remain unchanged.

```json
{
  "schemaVersion": 1,
  "profiles": {
    "smoke": {
      "scenario": {
        "build": {"projectPath": "../Host/Host.csproj", "configuration": "Release", "arguments": ["-p:EnableUiInspection=true"]},
        "launch": {"command": "dotnet", "argumentList": ["{profileDir}/../Host/bin/Release/net10.0/Host.dll"], "timeoutMs": 20000},
        "outputDirectory": "../artifacts/ui/{runId}",
        "steps": [
          {"action": "wait_for_node", "selector": {"name": "ReadyIndicator"}, "timeoutMs": 10000},
          {"action": "screenshot"}
        ]
      },
      "provider": {"directory": "../../external/avascope-provider", "version": "1.5.0", "manifestSha256": "<pinned-manifest-sha256>"},
      "environmentReferences": {"launch": {"TEST_TOKEN": {"name": "CI_TEST_TOKEN"}}},
      "platforms": {
        "linux": {"environmentReferences": {"launch": {
          "DISPLAY": {"name": "DISPLAY", "secret": false},
          "XAUTHORITY": {"name": "XAUTHORITY", "secret": false, "required": false}
        }}},
        "windows": {"scenario": {"launch": {"timeoutMs": 30000}}},
        "macos": {"scenario": {"launch": {"timeoutMs": 30000}}}
      }
    }
  }
}
```

Adjust the selected project, built assembly, readiness selector and provider pin for the host. The application still owns compile-time permission and explicit bootstrap. Omit `provider` for ordinary package integration. Profiles only launch explicitly selected owned applications; use an explicit scenario request when attaching an existing session.

Inspect without building, editing, launching or activating:

```sh
avascope resolve-test-profile --profile-file /absolute/agent-tests.json --profile smoke
avascope resolve-test-profile --profile-file /absolute/agent-tests.json --profile smoke --platform linux
avascope run-scenario --profile-file /absolute/agent-tests.json --profile smoke
```

MCP uses `resolve_test_profile(profileFile, profileName, platform?)` and `run_scenario(profileFile, profileName)`. Execution always chooses the current platform; `platform` is a read-only preview override. Select either a profile or an explicit request, never both. CI invokes the same CLI command against the same checked-in file.

Resolution rules are deterministic: the base profile is merged with `platforms.windows`, `.linux` or `.macos`; objects merge, arrays replace, and null removes an inherited field. Names are 1–64 ASCII letters/digits/periods/underscores/hyphens. Unknown fields and schema versions fail before launch. Limits are 1 MiB, 64 profiles, and 64 environment references per launch/build section; existing workflow limits still apply.

Project paths, working directories, provider directory, manifest/output/evidence paths and screenshot paths are relative to the profile file, regardless of the caller's working directory. Bare commands such as `dotnet` remain PATH lookups. Use `{profileDir}` inside arguments; argument strings are otherwise kept tokenized and unchanged. `{runId}` is a unique execution value and remains a placeholder in read-only output. An output directory without this token receives a unique child directory. The default manifest directory is inside that run, and owned process termination defaults to true. Readiness, timeouts, isolation, workflow composition and evidence settings use their existing scenario contracts.

Use `environmentReferences.launch` or `.build` instead of literal environment dictionaries. Each target variable names an existing environment variable through `{ "name": "SOURCE_NAME", "secret": true, "required": true }`; the booleans default to true. Missing required references fail clearly. Mark display/locale values non-secret only when appropriate. Referenced secrets are injected only for execution, replaced by `[REDACTED]` in resolved output, and added to the existing evidence policy so build/launch logs, timelines and structured evidence are redacted before persistence. Configure screenshot masks for secrets shown by the application; text redaction cannot infer pixels. Profiles must not embed secrets in arguments, variables, source paths or checked-in data.

Provider files and optional exact pins are verified at resolution without activation. The resolved identity is injected into the loader environment; simultaneous provider environment references are rejected. A read-only response includes the file SHA-256, selected platform, normalized redacted scenario, provider identity and environment variable names so an agent can review exactly what will run.

Validation: `eng/test-integration-onboarding.ps1` executes the same generated named profile through CLI and MCP for package and external-provider hosts, including log redaction and real screenshots. Platform/schema/path/secret failure fixtures are covered by `AgentTestProfileTests`.
