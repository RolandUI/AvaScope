using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using AvaScope.Bridge;
using AvaScope.Protocol;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class BootstrapTests : IDisposable
{
    public BootstrapTests() => AvaScopeBridge.Deactivate();

    public void Dispose() => AvaScopeBridge.Deactivate();

    [Fact]
    public void ResolvingBootstrapDoesNotActivateBridge()
    {
        var bootstrap = typeof(AvaScopeBridge).Assembly.GetType("AvaScope.Bridge.Bootstrap", throwOnError: true)!;
        var start = bootstrap.GetMethod("Start", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);
        Assert.NotNull(start);
        Assert.Equal(typeof(string), start.ReturnType);
        Assert.False(AvaScopeBridge.IsActive);
    }

    [Fact]
    public async Task BootstrapRejectsMissingLifetimeBeforeCreatingSession()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BootstrapWithoutLifetimeTestApplication));
        await session.Dispatch(() =>
        {
            Assert.Null(Application.Current!.ApplicationLifetime);
            var exception = Assert.Throws<NotSupportedException>(() => Bootstrap.Start());
            Assert.Contains("AVASCOPE_LIFETIME_UNAVAILABLE", exception.Message);
            Assert.False(AvaScopeBridge.IsActive);
        }, CancellationToken.None);
    }

    [Fact]
    public async Task BootstrapTracksExistingNewClosedWindowsAndReactivation()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BootstrapTestApplication));
        await session.Dispatch(async () =>
        {
            using var lifetime = (ClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            var existing = new Window { Width = 200, Height = 100, Content = new TextBox { Text = "before" } };
            var added = new Window { Width = 200, Height = 100, Content = new Button { Content = "Added" } };
            existing.Show();
            lifetime.MainWindow = existing;
            try
            {
                var firstId = Bootstrap.Start();
                var runtime = AvaScopeBridge.Current!;
                var manifest = runtime.SessionManifestPath!;
                Assert.Equal(firstId, Bootstrap.Start());
                Assert.True(File.Exists(manifest));
                var existingTopLevel = Assert.Single(await runtime.ListTopLevelsAsync());

                // No host registration call: the global public WindowOpened event owns this entry.
                added.Show();
                var topLevels = await runtime.ListTopLevelsAsync();
                Assert.Equal(2, topLevels.Count);
                var addedTopLevel = Assert.Single(topLevels, topLevel => topLevel.Id != existingTopLevel.Id);
                Assert.True((await runtime.GetVisualTreeAsync(addedTopLevel.Id)).Success);
                added.Close();
                Assert.Single(await runtime.ListTopLevelsAsync());

                Bootstrap.Stop();
                Assert.False(File.Exists(manifest));
                Assert.False(AvaScopeBridge.IsActive);
                Assert.NotEqual(firstId, Bootstrap.Start());
                var secondManifest = AvaScopeBridge.Current!.SessionManifestPath!;
                Bootstrap.Stop();
                Assert.False(AvaScopeBridge.IsActive);
                Assert.False(File.Exists(secondManifest));
            }
            finally
            {
                Bootstrap.Stop();
                added.Close();
                existing.Close();
            }

            return true;
        }, CancellationToken.None);
    }

    [Fact]
    public void FailedManifestCreationLeavesBridgeInactiveAndAllowsRetry()
    {
        var previousDirectory = Environment.GetEnvironmentVariable(BridgeSessionManifest.DirectoryEnvironmentVariable);
        var invalidDirectory = Path.GetTempFileName();
        try
        {
            Environment.SetEnvironmentVariable(BridgeSessionManifest.DirectoryEnvironmentVariable, invalidDirectory);
            Assert.ThrowsAny<IOException>(() => AvaScopeBridge.Activate());
            Assert.False(AvaScopeBridge.IsActive);
            Environment.SetEnvironmentVariable(BridgeSessionManifest.DirectoryEnvironmentVariable, previousDirectory);
            var runtime = AvaScopeBridge.Activate();
            Assert.True(File.Exists(runtime.SessionManifestPath));
            Assert.True(AvaScopeBridge.Deactivate().Success);
        }
        finally
        {
            Environment.SetEnvironmentVariable(BridgeSessionManifest.DirectoryEnvironmentVariable, previousDirectory);
            File.Delete(invalidDirectory);
        }
    }

    private sealed class BootstrapTestApplication : Application
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<BootstrapTestApplication>()
            .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

        public override void Initialize() => Styles.Add(new FluentTheme());

        public override void RegisterServices()
        {
            ApplicationLifetime = new ClassicDesktopStyleApplicationLifetime { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            base.RegisterServices();
        }
    }

    private sealed class BootstrapWithoutLifetimeTestApplication : Application
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<BootstrapWithoutLifetimeTestApplication>()
            .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
