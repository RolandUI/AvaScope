using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using AvaScope.Bridge;
using AvaScope.Protocol;

namespace AvaScope.ComplexWorkflowApp;

// Only the Direct host declares domain identity. The pure standalone host shares
// the ordinary view/journal, but cannot claim this declaration through injection.
public partial class QaNavigationView : IAvaScopeDebugStateProvider
{
    public IReadOnlyDictionary<string, string?> GetAvaScopeDebugState() =>
        new Dictionary<string, string?>
        {
            ["navigation.surface"] = Surface,
            ["navigation.context"] = Context,
            ["navigation.revision"] = Revision
        };
}

// Host-owned declarations stay outside the pure fixture shared with the standalone host.
public static class QaRuntimeRegistration
{
    public const string SelectAction = "qa.scene.select";
    public const string WorkAction = "qa.work";

    public static IReadOnlyList<IDisposable> Register(AvaScopeBridgeRuntime runtime, QaWindow window)
    {
        var scene = window.FindControl<QaSceneControl>("RuntimeScene")!;
        var work = window.FindControl<Button>("StartOperationButton")!;
        var registrations = new IDisposable[]
        {
            new DeferredSceneRegistration(runtime, scene, () => new AvaScopeSceneSnapshot(
                scene.Revision.ToString(CultureInfo.InvariantCulture),
                scene.Items.Select(item => new RuntimeSceneObject(item.Id,
                    scene.Generation.ToString(CultureInfo.InvariantCulture), "record", item.Label,
                    new NodeBounds(item.Bounds.X, item.Bounds.Y, item.Bounds.Width, item.Bounds.Height),
                    scene.SelectedId == item.Id, Actions: [SelectAction])).ToArray(), scene.SceneToCanvas)),
            runtime.RegisterCustomAction(scene, new CustomActionRegistration(SelectAction, context =>
                scene.Select(context.SceneObject!.Id)
                    ? CustomActionOutcome.Succeeded("Scene record selected.")
                    : CustomActionOutcome.Failed("The declared record is absent."),
                description: "Select one currently observed drawn record.", requiresSceneObject: true)),
            runtime.RegisterCustomAction(work, new CustomActionRegistration(WorkAction, context =>
            {
                if (!int.TryParse(context.Parameters["steps"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var steps)
                    || steps is < 1 or > 10) return CustomActionOutcome.Failed("Work requires 1–10 steps.");
                var handle = context.BeginOperation();
                _ = CompleteWorkAsync(handle, context.Parameters["mode"], steps);
                return CustomActionOutcome.Succeeded("Fixture work accepted.");
            }, description: "Run bounded fixture work with observable progress and cancellation.",
                parameters:
                [new("mode", required: true, allowedValues: ["complete", "fail"]),
                 new("steps", RuntimeCustomActionParameterTypes.Integer, required: true)],
                supportsOperations: true, supportsCancellation: true))
        };
        window.EnableDeclaredRuntime();
        return registrations;

        async Task CompleteWorkAsync(RuntimeOperationHandle handle, string mode, int steps)
        {
            try
            {
                var result = await window.RunFixtureOperationAsync(mode, steps, handle.CancellationToken,
                    (progress, message) => handle.ReportProgress(progress, message));
                if (result == "cancelled") handle.ConfirmCancelled();
                else if (result == "failed") handle.Fail("qa_deliberate_failure", "Deliberate fixture failure.");
                else handle.Complete(new Dictionary<string, string> { ["steps"] = steps.ToString(CultureInfo.InvariantCulture) });
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
            {
                handle.Fail("qa_operation_error", "Fixture work failed unexpectedly.");
            }
        }
    }

    // Tab content is not attached at startup. Register only after its first real attachment,
    // without selecting a different tab or forcing a layout as a side effect of registration.
    private sealed class DeferredSceneRegistration : IDisposable
    {
        private readonly QaSceneControl _scene;
        private readonly AvaScopeBridgeRuntime _runtime;
        private readonly Func<AvaScopeSceneSnapshot> _capture;
        private IDisposable? _registration;

        public DeferredSceneRegistration(AvaScopeBridgeRuntime runtime, QaSceneControl scene, Func<AvaScopeSceneSnapshot> capture)
        {
            _runtime = runtime; _scene = scene; _capture = capture;
            if (TopLevel.GetTopLevel(scene) is not null) _registration = runtime.RegisterScene(scene, capture);
            else scene.AttachedToVisualTree += OnAttached;
        }

        private void OnAttached(object? sender, VisualTreeAttachmentEventArgs args)
        {
            _registration = _runtime.RegisterScene(_scene, _capture);
            _scene.AttachedToVisualTree -= OnAttached;
        }

        public void Dispose()
        {
            _scene.AttachedToVisualTree -= OnAttached;
            _registration?.Dispose();
        }
    }
}
