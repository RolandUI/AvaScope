using Avalonia;
using Avalonia.Headless;

namespace AvaScope.ComplexWorkflowApp;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--native", StringComparer.Ordinal) && args.Contains("--headless", StringComparer.Ordinal))
            throw new ArgumentException("Choose either --native or --headless.");
        var builder = args.Contains("--native", StringComparer.Ordinal)
            ? AppBuilder.Configure<App>().UsePlatformDetect()
            : BuildAvaloniaApp();
        builder.StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            });
    }
}
