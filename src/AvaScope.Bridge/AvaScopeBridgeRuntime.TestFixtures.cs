using Avalonia;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    public IDisposable RegisterTestFixture(Visual target, RuntimeTestFixtureDescriptor fixture,
        Func<CustomActionContext, CustomActionOutcome> prepare,
        Func<CustomActionContext, CustomActionOutcome>? cleanup = null,
        IReadOnlyList<RuntimeCustomActionParameterDescriptor>? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        if (fixture.HasCleanup != (cleanup is not null))
            throw new ArgumentException("The fixture cleanup declaration must match its cleanup handler.", nameof(cleanup));
        var schema = (parameters ?? []).ToArray();
        if (schema.Length > 16 || schema.Any(parameter => parameter.Name == "testResource"
            || parameter.Type == RuntimeCustomActionParameterTypes.String && parameter.AllowedValues.Count == 0))
            throw new ArgumentException("Fixture string parameters require an explicit value allowlist; testResource is reserved.", nameof(parameters));
        var resource = new RuntimeCustomActionParameterDescriptor("testResource", required: true, allowedValues: fixture.ResourceIds);
        var prepareRegistration = RegisterCustomAction(target, new CustomActionRegistration(fixture.PrepareAction,
            prepare, "Prepare explicitly selected host-owned test state.", parameters: [resource, .. schema], testFixture: fixture));
        try
        {
            var cleanupRegistration = cleanup is null ? null : RegisterCustomAction(target, new CustomActionRegistration(fixture.CleanupAction!,
                cleanup, "Clean up explicitly selected host-owned test state.", parameters: [resource], testFixture: fixture));
            return new TopLevelRegistration(() => { prepareRegistration.Dispose(); cleanupRegistration?.Dispose(); });
        }
        catch
        {
            prepareRegistration.Dispose();
            throw;
        }
    }
}
