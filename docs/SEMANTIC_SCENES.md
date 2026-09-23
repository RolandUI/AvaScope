# Declared canvas and diagram objects

The additive `runtime.scenes` capability exposes CLI/MCP `scene`. It inspects a
host-registered scene on a generation-pinned visual canvas. Drawn graph edges,
chart points and shapes can have semantic identities even without visual-tree
children. No domain meaning is inferred from screenshots, view models or private
framework APIs.

Register an adapter on the UI thread after its canvas belongs to a known top-level:

```csharp
using var scene = runtime.RegisterScene(graph, () => new AvaScopeSceneSnapshot(
    graph.Revision.ToString(),
    [
        new RuntimeSceneObject("a", "a-lifetime", "node", "Source", new(0, 0, 20, 20)),
        new RuntimeSceneObject("b", "b-lifetime", "node", "Destination", new(100, 0, 20, 20)),
        new RuntimeSceneObject("a-b", graph.EdgeGeneration, "connection", "A to B",
            new(20, 10, 80, 2), graph.SelectedId == "a-b",
            [new("connects_from", "a", "a-lifetime"), new("connects_to", "b", "b-lifetime")],
            ["scene.select"])
    ], graph.SceneToCanvas));

using var action = runtime.RegisterCustomAction(graph, new CustomActionRegistration(
    "scene.select", context =>
    {
        graph.Select(context.SceneObject!.Id);
        return CustomActionOutcome.Succeeded("Selection updated.");
    }, requiresSceneObject: true));
```

The action must already be allowed by explicit bridge activation. Set
`requiresSceneObject: true` for object-specific handlers: ordinary custom-action
invocation then fails rather than bypassing the scene guard. Scene invocation uses
the existing action parameters, availability, app allowlist, destructive-action
authorization, operation handles, audit and session-control lease. An action's
declaration is not a claim that it will be executable later. The returned custom
action status is app-reported execution; inspect the resulting scene selection or
other state to verify the intended effect.

For an agent:

1. Resolve the actual canvas with `find_nodes` and retain its full target context.
2. Call `scene` with `canvas`, optionally `objectId`, `objectType` or `relatedTo`, and
   `maxObjects` (default 64, maximum 128). Filters are exact ordinal matches.
   `relatedTo` matches a declared outgoing relationship to that object id.
3. Inspect `snapshot.objects`, `coverage`, transforms and per-item `unavailable`.
   A connection may declare `connects_from` and `connects_to`; these labels are
   application declarations, not inferred relationship rules.
4. Copy an item's complete `target` into `expectedObject` and call `scene` with
   `action: "invoke"`, the same `canvas`, `actionName`, a new `requestId`, optional
   bounded `parameters`, and the evidence policy. Query filters cannot select an
   invocation target implicitly.

Example invocation payload, with values copied from a real inspection:

```json
{
  "canvas": { "sessionId": "session-id", "topLevelId": "top-id", "treeKind": "visual",
    "nodeId": "canvas-id", "topLevelGeneration": "observed-top", "nodeGeneration": "observed-canvas" },
  "action": "invoke",
  "expectedObject": { "sceneGeneration": "observed-adapter-generation",
    "sceneRevision": "copy-the-64-character-revision-from-the-result",
    "objectId": "a-b", "objectGeneration": "observed-edge-generation" },
  "actionName": "scene.select",
  "requestId": "select-connection-1"
}
```

`avascope scene --request scene.json --manifest-dir <dir>` and MCP `scene` use the
same request and response. `sceneRevision` and identity values above are
placeholders. A request id identifies the action; it is not an idempotency ledger.
The client never retries a scene invocation automatically. After a lost response,
inspect actual app state before deciding whether another action is needed.

The host must keep ids stable for an object's lifetime, change its generation
when an id is reused, and update the scene revision whenever semantic state or the
camera changes. Bridge guards include those values, adapter registration identity,
canvas identity/data context, captured objects, transform, canvas geometry and
render scaling. Two bounded captures must agree. Invocation captures again after
custom-action availability callbacks and immediately before calling the handler.
Removing/recreating a connection, panning/zooming, changed redraw revisions,
replacing an adapter or recycling the canvas invalidates the old target. The
adapter contract remains necessary for changes outside the bounded scan and for
remove/recreate cycles which occur entirely between observations.

Object bounds are host-declared scene coordinates. Public Avalonia
[matrix transforms](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Base/Matrix.cs)
produce axis-aligned canvas and top-level bounds in DIPs; render scaling is
reported separately. Perspective/unavailable transforms produce explicit geometry
gaps. These rectangles are not native hit-test points, do not prove visibility or
lack of occlusion, and are never used to synthesize pointer input. Semantic custom
actions remain usable without object geometry.

Bounds: 32 scene adapters per bridge; 256 scanned objects and a 1 MiB capture;
1..128 returned objects with a 96 KiB combined item budget; cooperative two-second
inspection. Each object permits a 1024-character declared label (returned at most
512 after redaction), 16 relationships, eight action names, eight metadata fields
(64-character keys/256-character values), and 128-character identities. The
adapter must be synchronous, fast, UI-thread-safe and side-effect-free. A blocked
host callback cannot be forcibly aborted. Partial adapters and response limits
produce partial coverage; not finding an object there is not proof of absence.
Missing geometry/selection/relationship endpoints and truncated labels are marked.

Evidence policy applies before response truncation. Canvas/ancestor exclusions
block capture; protected object privacy AutomationIds remove those objects and
their incoming references. Protected object identities are withheld, so sanitized
ids cannot silently target another object. Text labels and metadata are redacted.
Query and action paths both require policy `inspect` authorization; invocation also
requires `custom_action` and the named action allowlist. Destructive actions retain
the host, request and policy gates. No scene data is persisted or uploaded by this
tool. Disposing a registration, unregistering the top-level or shutting down the
session clears its registrations; new sessions cannot reuse old targets.

Set `AVASCOPE_SAMPLE_BRIDGE=1` and `AVASCOPE_SAMPLE_SCENE=1` when running the getting
started sample to open the optional graph window. Its `SemanticGraph` visual has
an A-to-B connection with `scene.select`, plus visible zoom/pan and remove/recreate
buttons. This shared public-Avalonia path supports the bridge's Windows, Linux,
macOS and headless backends without claiming native accessibility or pixel picking.
