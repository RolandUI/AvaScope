using Avalonia;
using Avalonia.Themes.Fluent;

namespace AvaScope.PreviewHost;

internal sealed class PreviewHostApplication : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }
}
