# Opt-in deterministic host test fixtures

A fixture is a host-declared convention over existing allowlisted custom actions. It prepares a named test resource, waits for a declared public readiness/postcondition, then cleans it up before scenario process/environment shutdown. It never accepts a database connection, filesystem reset path, source expression or arbitrary method name as a resource selector.

## Host declaration

Keep registration behind the host's own test/diagnostics compile flag. Enable fixtures separately from ordinary custom actions, declare test-resource identities, and allow both preparation and cleanup action names:

```csharp
#if ENABLE_UI_TEST_FIXTURES
var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions(
    enableCustomActions: true,
    allowedCustomActions: ["fixture.prepare.seeded", "fixture.cleanup.seeded"],
    enableTestFixtures: true,
    allowedTestResources: ["test-memory"]));

var fixture = new RuntimeTestFixtureDescriptor(
    "seeded", "1", ["test-memory"],
    new SemanticWaitCondition(SemanticWaitConditionKinds.ApplicationReady));

// fixtureTarget must be in a registered top-level and have a unique AutomationId.
AutomationProperties.SetAutomationId(fixtureTarget, "TestFixtures");
using var registration = runtime.RegisterTestFixture(
    fixtureTarget, fixture,
    prepare: context =>
    {
        // Resolve only the pre-created test resource. Do not interpret resource ids as paths.
        runtime.SetReadiness("busy", "Preparing test data");
        testRows.Clear();
        var seed = int.Parse(context.Parameters["seed"]);
        testRows.AddRange(CreateDeterministicRows(seed));
        runtime.SetReadiness("ready", "Test data ready");
        return CustomActionOutcome.Succeeded("Prepared");
    },
    cleanup: context =>
    {
        CancelPendingFixtureWork();
        testRows.Clear();
        return CustomActionOutcome.Succeeded("Cleaned");
    },
    parameters: [new RuntimeCustomActionParameterDescriptor(
        "seed", RuntimeCustomActionParameterTypes.Integer, required: true)]);
#endif
```

Keep the returned registration alive for the intended test lifetime. The illustration's data/resource helpers belong to the application. The bridge does not create or reset application databases. Resource identities are 1–64 ASCII letters/digits/dots/dashes/underscores and map to test resources already chosen by the host. The host remains responsible for mapping them exclusively to test data. Activation and registration are disabled by default; ordinary bridge activation alone does not enable fixtures.

The helper adds a required `testResource` parameter constrained to the declared resource ids. Callers cannot override it through fixture parameters. Additional parameters use the existing typed custom-action schema; string parameters must have an explicit value allowlist. Unknown actions/resources and invalid parameter values are rejected before fixture handlers run. Required cleanup must be registered and allowlisted before preparation. Registration failure removes any partially registered preparation action.

## Agent discovery and lifecycle

Discover through `custom-actions` / `custom_actions`. Preparation and cleanup descriptors expose `testFixture` metadata with name, version, resource identities and readiness condition/selector. A fixture can use `application_ready`, a typed text/value condition or another supported public wait. Set readiness to busy before asynchronous work and only report ready after its postcondition holds. Do not use a stale readiness flag from the previous run.

Add this to a normal `run-scenario` / `run_scenario` request (or the scenario inside a test profile):

```json
{
  "testFixture": {
    "name": "seeded",
    "resourceId": "test-memory",
    "targetAutomationId": "TestFixtures",
    "parameters": { "seed": "42" },
    "timeoutMs": 5000,
    "cleanupTimeoutMs": 3000
  }
}
```

The target must resolve uniquely inside the selected top-level and scenario depth. Fixture preparation runs after attach/window discovery and before optional application readiness, tree capture and workflow actions. Descriptor/resource/cleanup/readiness validation precedes dispatch. Existing policy authorization applies to the selected session, `custom_actions`, `custom_action`, `wait_for_state` and each preparation/cleanup action name. Cleanup authorization is checked before preparation; destructive host actions also require both host and explicit scenario/policy authorization.

Scenario calls pin the discovered fixture version for preparation and cleanup. The bridge rejects a changed declaration before executing the handler; changing fixture behavior requires the host to change its declared version.

Preparation plus readiness share a 50–60000 ms deadline (default 5000). Cleanup has an independent 50–10000 ms deadline (default 3000) and runs after success, preparation failure, readiness failure, workflow failure or cancellation whenever preparation may have executed. It runs at most once, before closing an owned process or desktop. A fixture can explicitly declare `hasCleanup: false` when no cleanup is required. Required cleanup failure makes the scenario fail with `fixture_cleanup`; it does not prevent owned process/environment cleanup.

Handlers run on the Avalonia UI thread using the existing synchronous custom-action contract. Asynchronous preparation may acknowledge scheduling and use the readiness condition; cleanup must cancel/finish its owned pending work before acknowledging success. AvaScope cannot forcibly interrupt a blocked in-process callback, roll back host changes, or claim that an expired transport deadline prevented execution. Timeout/unknown outcomes remain explicit. Fixtures should be deterministic, safe to re-prepare, and scoped to a test resource owned for that run; process exit is not a substitute for acknowledged application cleanup.

## Evidence and validation

Scenario JSON and the Markdown timeline contain fixture identity/version, resource identity, parameter **names**, preparation/readiness/cleanup status and public readiness observation. Parameter values and arbitrary handler metadata are omitted from fixture evidence. Existing evidence-policy redaction applies before artifacts/transport output. Public readiness may itself contain application state, so configure redaction for sensitive values. Unknown or disallowed fixtures produce a failed preparation stage and do not run workflow actions.

When policy action auditing is enabled, preparation intent/outcome and cleanup outcome join the existing redacted local action audit. Failed intent auditing prevents preparation. Required cleanup is still attempted after an executed preparation; an audit failure is reported rather than claiming a complete audit trail.

Headless integration fixtures exercise seeded data, empty data and offline mode twice, verify the same seed produces the same rows, and clean their in-memory resources after each scenario. Regressions cover disabled registration, resource/action allowlists, direct invocation parameter enforcement, policy-denied cleanup, readiness timeout, preparation failure, cancellation, cleanup failure and non-sensitive evidence. These use shared Avalonia APIs and the existing local transport on supported platforms.

Real CLI and MCP processes also run the same fixture scenario and verify preparation, readiness and cleanup evidence against the live host.
