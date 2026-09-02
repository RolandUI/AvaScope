using Avalonia;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed record CustomActionRegistration
{
    public CustomActionRegistration(
        string name,
        Func<CustomActionContext, CustomActionOutcome> handler,
        string? description = null,
        string safetyClassification = RuntimeCustomActionSafetyClassifications.NonDestructive,
        IReadOnlyList<RuntimeCustomActionParameterDescriptor>? parameters = null,
        IReadOnlyDictionary<string, string>? requiredState = null,
        Func<Visual, CustomActionAvailability>? availability = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Custom action name cannot be empty.", nameof(name));
        }

        if (!RuntimeCustomActionSafetyClassifications.All.Contains(safetyClassification, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Custom action safety classification '{safetyClassification}' is not supported.", nameof(safetyClassification));
        }

        Name = name.Trim();
        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        SafetyClassification = safetyClassification;
        Parameters = (parameters ?? []).ToArray();
        RequiredState = requiredState ?? new Dictionary<string, string>();
        Availability = availability;
    }

    public string Name { get; }
    public Func<CustomActionContext, CustomActionOutcome> Handler { get; }
    public string? Description { get; }
    public string SafetyClassification { get; }
    public IReadOnlyList<RuntimeCustomActionParameterDescriptor> Parameters { get; }
    public IReadOnlyDictionary<string, string> RequiredState { get; }
    public Func<Visual, CustomActionAvailability>? Availability { get; }
}

public sealed record CustomActionContext(
    string RequestId,
    Visual Target,
    IReadOnlyDictionary<string, string> Parameters);

public sealed record CustomActionAvailability(
    bool Executable,
    string? Reason = null,
    IReadOnlyDictionary<string, string>? State = null)
{
    public static CustomActionAvailability Available { get; } = new(true);
}

public sealed record CustomActionOutcome(
    bool Success,
    string Message,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public static CustomActionOutcome Succeeded(
        string message,
        IReadOnlyDictionary<string, string>? metadata = null) => new(true, message, metadata);

    public static CustomActionOutcome Failed(
        string message,
        IReadOnlyDictionary<string, string>? metadata = null) => new(false, message, metadata);
}
