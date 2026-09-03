using Avalonia.Controls;
using Avalonia.Input;

namespace AvaScope.ComplexWorkflowApp;

public sealed class WorkflowDragCard : Border
{
    public event EventHandler? DragCompleted;

    protected override void OnPointerPressed(PointerPressedEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        eventArgs.Pointer.Capture(this);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        eventArgs.Pointer.Capture(null);
        DragCompleted?.Invoke(this, EventArgs.Empty);
    }
}
