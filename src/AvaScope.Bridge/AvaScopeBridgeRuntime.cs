using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using AvaScope.Core;
using AvaScope.Protocol;
using System.Collections.Concurrent;

namespace AvaScope.Bridge;

public sealed class AvaScopeBridgeRuntime
{
    private readonly ConcurrentDictionary<int, WeakReference<TopLevel>> _registeredTopLevels = new();
    private readonly SessionRegistry _sessionRegistry;

    internal AvaScopeBridgeRuntime(
        SessionRegistry sessionRegistry,
        SessionSnapshot session,
        BridgeTransportScope transportScope)
    {
        _sessionRegistry = sessionRegistry ?? throw new ArgumentNullException(nameof(sessionRegistry));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        TransportScope = transportScope;
    }

    public SessionSnapshot Session { get; }

    public SessionId SessionId => Session.Id;

    public BridgeTransportScope TransportScope { get; }

    public IDisposable RegisterTopLevel(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        Dispatcher.UIThread.VerifyAccess();

        var key = InspectableTopLevel.GetRuntimeId(topLevel);
        _registeredTopLevels[key] = new WeakReference<TopLevel>(topLevel);

        return new TopLevelRegistration(_registeredTopLevels, key);
    }

    public Task<IReadOnlyList<InspectableTopLevel>> ListTopLevelsAsync(CancellationToken cancellationToken = default)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return Task.FromResult(DiscoverTopLevels());
        }

        return Dispatcher.UIThread
            .InvokeAsync(DiscoverTopLevels, DispatcherPriority.Background, cancellationToken)
            .GetTask();
    }

    internal CoreResult<SessionSnapshot> Close()
    {
        return _sessionRegistry.Close(SessionId);
    }

    private IReadOnlyList<InspectableTopLevel> DiscoverTopLevels()
    {
        Dispatcher.UIThread.VerifyAccess();

        var discovered = new List<InspectableTopLevel>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var topLevel in DiscoverLifetimeTopLevels())
        {
            if (seen.Add(topLevel.Id))
            {
                discovered.Add(topLevel);
            }
        }

        foreach (var topLevel in DiscoverRegisteredTopLevels())
        {
            if (seen.Add(topLevel.Id))
            {
                discovered.Add(topLevel);
            }
        }

        return discovered;
    }

    private static IReadOnlyList<InspectableTopLevel> DiscoverLifetimeTopLevels()
    {
        return Application.Current?.ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktop => desktop.Windows
                .Select(static window => InspectableTopLevel.FromWindow(window))
                .ToArray(),
            ISingleViewApplicationLifetime { MainView: { } mainView } => DiscoverSingleViewTopLevel(mainView),
            _ => Array.Empty<InspectableTopLevel>()
        };
    }

    private IReadOnlyList<InspectableTopLevel> DiscoverRegisteredTopLevels()
    {
        var topLevels = new List<InspectableTopLevel>();

        foreach (var (key, weakReference) in _registeredTopLevels)
        {
            if (!weakReference.TryGetTarget(out var topLevel))
            {
                _registeredTopLevels.TryRemove(key, out _);
                continue;
            }

            topLevels.Add(topLevel is Window window
                ? InspectableTopLevel.FromWindow(window)
                : InspectableTopLevel.FromTopLevel(topLevel, "topLevel"));
        }

        return topLevels;
    }

    private static IReadOnlyList<InspectableTopLevel> DiscoverSingleViewTopLevel(Control mainView)
    {
        var topLevel = TopLevel.GetTopLevel(mainView);

        return topLevel is null
            ? Array.Empty<InspectableTopLevel>()
            : [InspectableTopLevel.FromTopLevel(topLevel, "singleView")];
    }

    private sealed class TopLevelRegistration(
        ConcurrentDictionary<int, WeakReference<TopLevel>> registeredTopLevels,
        int key) : IDisposable
    {
        public void Dispose()
        {
            registeredTopLevels.TryRemove(key, out _);
        }
    }
}
