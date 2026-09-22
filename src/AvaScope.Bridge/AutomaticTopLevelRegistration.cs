using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace AvaScope.Bridge;

internal sealed class AutomaticTopLevelRegistration : IDisposable
{
    private readonly AvaScopeBridgeRuntime _runtime;
    private readonly IApplicationLifetime _lifetime;
    private readonly Dictionary<TopLevel, IDisposable> _registrations = [];
    private readonly List<IDisposable> _subscriptions = [];
    private TopLevel? _singleViewRoot;
    private bool _disposed;

    public AutomaticTopLevelRegistration(AvaScopeBridgeRuntime runtime, IApplicationLifetime lifetime)
    {
        Dispatcher.UIThread.VerifyAccess();
        _runtime = runtime;
        _lifetime = lifetime;
        try
        {
            _subscriptions.Add(Window.WindowOpenedEvent.AddClassHandler<Window>(
                (window, _) => Register(window), handledEventsToo: true));

            if (lifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow is { } mainWindow)
                {
                    Register(mainWindow);
                }

                foreach (var window in desktop.Windows)
                {
                    Register(window);
                }
            }
            else if (lifetime is ISingleViewApplicationLifetime singleView)
            {
                RegisterSingleView(singleView);
                _subscriptions.Add(Control.LoadedEvent.AddClassHandler<Control>((control, _) =>
                {
                    if (ReferenceEquals(control, singleView.MainView))
                    {
                        RegisterSingleView(singleView);
                    }
                }, handledEventsToo: true));
                _subscriptions.Add(Control.UnloadedEvent.AddClassHandler<Control>((control, _) =>
                {
                    if (ReferenceEquals(control, singleView.MainView) && _singleViewRoot is { } root)
                    {
                        Unregister(root);
                        _singleViewRoot = null;
                    }
                }, handledEventsToo: true));
            }

            if (lifetime is IControlledApplicationLifetime controlled)
            {
                controlled.Exit += OnApplicationExit;
            }

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.InvokeAsync(Dispose, DispatcherPriority.Send).GetTask().GetAwaiter().GetResult();
            return;
        }

        if (_disposed)
        {
            return;
        }

        _disposed = true;
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        if (_lifetime is IControlledApplicationLifetime controlled)
        {
            controlled.Exit -= OnApplicationExit;
        }

        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
        foreach (var topLevel in _registrations.Keys.ToArray())
        {
            Unregister(topLevel);
        }

        _singleViewRoot = null;
    }

    private void RegisterSingleView(ISingleViewApplicationLifetime lifetime)
    {
        var root = lifetime.MainView is { } view ? TopLevel.GetTopLevel(view) : null;
        if (_singleViewRoot is { } previous && !ReferenceEquals(previous, root))
        {
            Unregister(previous);
        }

        _singleViewRoot = root;
        if (root is not null)
        {
            Register(root);
        }
    }

    private void Register(TopLevel topLevel)
    {
        if (_disposed || topLevel.PlatformImpl is null || _registrations.ContainsKey(topLevel))
        {
            return;
        }

        _registrations.Add(topLevel, _runtime.RegisterTopLevel(topLevel));
        topLevel.Closed += OnTopLevelClosed;
    }

    private void Unregister(TopLevel topLevel)
    {
        topLevel.Closed -= OnTopLevelClosed;
        if (_registrations.Remove(topLevel, out var registration))
        {
            registration.Dispose();
        }
    }

    private void OnTopLevelClosed(object? sender, EventArgs args)
    {
        if (sender is TopLevel topLevel)
        {
            Unregister(topLevel);
        }
    }

    private void OnApplicationExit(object? sender, ControlledApplicationLifetimeExitEventArgs args)
    {
        if (ReferenceEquals(AvaScopeBridge.Current, _runtime))
        {
            AvaScopeBridge.Deactivate();
        }
    }

    private void OnProcessExit(object? sender, EventArgs args)
    {
        // The dispatcher may have stopped. Transport teardown does not access Avalonia objects.
        _runtime.StopLocalServer();
    }
}
