using AvaScope.Core;

namespace AvaScope.Bridge;

public static class AvaScopeBridge
{
    private static readonly object SyncRoot = new();
    private static AvaScopeBridgeRuntime? _current;

    public static bool IsActive
    {
        get
        {
            lock (SyncRoot)
            {
                return _current is not null;
            }
        }
    }

    public static AvaScopeBridgeRuntime? Current
    {
        get
        {
            lock (SyncRoot)
            {
                return _current;
            }
        }
    }

    public static AvaScopeBridgeRuntime Activate(BridgeActivationOptions? options = null)
    {
        options ??= BridgeActivationOptions.Default;

        lock (SyncRoot)
        {
            if (_current is not null)
            {
                return _current;
            }

            var sessionRegistry = options.SessionRegistry ?? new SessionRegistry();
            var session = sessionRegistry.Create(options.SessionKind, options.DisplayName);

            _current = new AvaScopeBridgeRuntime(
                sessionRegistry,
                session,
                BridgeTransportScope.LocalOnly,
                options);
            _current.StartLocalServer();

            return _current;
        }
    }

    public static CoreResult<SessionSnapshot> Deactivate()
    {
        lock (SyncRoot)
        {
            if (_current is null)
            {
                return CoreResult<SessionSnapshot>.Fail(
                    new CoreError(BridgeErrorCodes.BridgeNotActive, "AvaScope bridge is not active."));
            }

            var result = _current.Close();
            _current = null;
            return result;
        }
    }

    internal static void CompleteRemoteClose(AvaScopeBridgeRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        lock (SyncRoot)
        {
            if (ReferenceEquals(_current, runtime))
            {
                _current = null;
            }
        }

        runtime.StopLocalServer();
    }
}
