using Avalonia;
using Avalonia.Controls;

namespace AvaScope.Bridge;

/// <summary>Optional application-owned, fast, read-only UI-thread evidence. May be implemented by a control or its automation peer.</summary>
public interface IAvaScopeActionContextProvider
{
    AvaScopeActionContext GetActionContext(string action);
}

/// <summary>The activation point is in the target control's local DIPs. Reasons are app declarations, never inferred business causes.</summary>
public sealed record AvaScopeActionContext(
    Point? ActivationPoint = null,
    bool? CanExecute = null,
    IReadOnlyList<string>? BusinessReasons = null,
    IReadOnlyList<Control>? RelatedValidationTargets = null);
