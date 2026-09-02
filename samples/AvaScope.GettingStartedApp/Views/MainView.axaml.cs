using Avalonia.Controls;

namespace AvaScope.GettingStartedApp.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    public void Confirm(string? note)
    {
        ActionStatusText.Text = string.IsNullOrWhiteSpace(note)
            ? "Custom action status: confirmed"
            : $"Custom action status: confirmed ({note})";
    }

    public void Reset()
    {
        ActionStatusText.Text = "Custom action status: ready";
    }
}
