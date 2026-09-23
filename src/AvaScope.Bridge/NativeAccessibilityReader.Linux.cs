using System.Runtime.InteropServices;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

internal static partial class NativeAccessibilityReader
{
    private const string Accessible = "org.a11y.atspi.Accessible";
    private const string ApplicationRoot = "/org/a11y/atspi/accessible/root";

    private static NativeAccessibilitySnapshot ReadLinux(NodeBounds? bounds, NativeAccessibilityAuditRequest request, CancellationToken token)
    {
        const string adapter = "linux_atspi2_dbus";
        var sessionAddress = Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS");
        if (!LocalBus(sessionAddress)) return Unavailable("unavailable", adapter, "native_accessibility_bus_unavailable", "A local Unix D-Bus session is required. Start an isolated accessibility service in the test environment; no desktop service is started by this query.");
        var cancel = g_cancellable_new(); nint session = 0, bus = 0;
        var nodes = new List<NativeAccessibilityNode>(); var diagnostics = new List<ProtocolError>(); var truncated = false;
        using var cancellation = token.Register(() => g_cancellable_cancel(cancel));
        try
        {
            session = Connect(sessionAddress!);
            using var addressReply = Invoke(session, "org.a11y.Bus", "/org/a11y/bus", "org.a11y.Bus", "GetAddress", "(s)");
            using var addressValue = addressReply.Child(0); var address = addressValue.String();
            if (!LocalBus(address)) return Unavailable("unavailable", adapter, "native_accessibility_bus_denied", "Only a local Unix accessibility bus is accepted.");
            bus = Connect(address);
            // Resolve only this process's authenticated connection. No foreign application objects
            // are queried: ListNames and PID credentials below are bus-daemon metadata only.
            using var namesReply = Invoke(bus, "org.freedesktop.DBus", "/org/freedesktop/DBus", "org.freedesktop.DBus", "ListNames", "(as)");
            using var names = namesReply.Child(0); var owners = new List<string>();
            if (names.Count > 1024) return Unavailable("unavailable", adapter, "native_accessibility_bus_limit", "The accessibility bus exceeds the bounded connection inventory.");
            for (nuint i = 0; i < names.Count; i++)
            {
                using var name = names.Child(i); var unique = name.String(); if (!unique.StartsWith(':')) continue;
                try
                {
                    using var pidReply = Invoke(bus, "org.freedesktop.DBus", "/org/freedesktop/DBus", "org.freedesktop.DBus", "GetConnectionUnixProcessID", "(u)", g_variant_new_string(unique));
                    using var pid = pidReply.Child(0);
                    if (pid.UInt() == Environment.ProcessId && unique != Marshal.PtrToStringUTF8(g_dbus_connection_get_unique_name(bus))) owners.Add(unique);
                }
                catch (NativeAccessibilityUnavailable) { token.ThrowIfCancellationRequested(); }
            }
            var roots = new List<(string Owner, string Path)>();
            foreach (var owner in owners.Take(16))
            {
                try
                {
                    foreach (var path in Children(owner, ApplicationRoot, 32))
                        if (NativeAccessibilityComparer.SameBounds(bounds, Extents(owner, path))) roots.Add((owner, path));
                }
                catch (NativeAccessibilityUnavailable) { token.ThrowIfCancellationRequested(); }
            }
            if (roots.Count != 1 || owners.Count > 16) return Unavailable("unavailable", adapter, "native_accessibility_root_unavailable",
                "The selected window could not be uniquely mapped to this process's AT-SPI root by desktop geometry. Verify the accessibility service, window layout and platform support.");
            var selected = roots[0];
            var pending = new Queue<(string Path, string? Parent, int Depth)>(); pending.Enqueue((selected.Path, null, 0));
            var visited = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                while (pending.Count > 0 && nodes.Count < request.MaxNodes)
                {
                    token.ThrowIfCancellationRequested(); var entry = pending.Dequeue();
                    if (!visited.Add(entry.Path)) { truncated = true; continue; }
                    var id = "atspi-" + nodes.Count;
                    using var roleReply = Invoke(bus, selected.Owner, entry.Path, Accessible, "GetRoleName", "(s)");
                    using var roleValue = roleReply.Child(0); var role = roleValue.String();
                    using var stateReply = Invoke(bus, selected.Owner, entry.Path, Accessible, "GetState", "(au)");
                    using var states = stateReply.Child(0); uint state = 0;
                    if (states.Count > 0) { using var first = states.Child(0); state = first.UInt(); }
                    nodes.Add(new(id, entry.Parent, Property(selected.Owner, entry.Path, "AccessibleId"), role == "password text" ? "[password control]" : Property(selected.Owner, entry.Path, "Name"),
                        role, (state & (1u << 8)) != 0, (state & (1u << 25)) == 0, Extents(selected.Owner, entry.Path)));
                    var remaining = entry.Depth >= request.MaxDepth ? 0 : request.MaxNodes - nodes.Count - pending.Count;
                    foreach (var child in Children(selected.Owner, entry.Path, remaining)) pending.Enqueue((child, id, entry.Depth + 1));
                }
                truncated |= pending.Count > 0;
            }
            catch (Exception e) when (e is NativeAccessibilityUnavailable or OperationCanceledException)
            { truncated = true; diagnostics.Add(new("native_accessibility_partial", "An AT-SPI query failed or exceeded its deadline; missing nodes are not diagnosed as absent.")); }
            return new(nodes.Count == 0 ? "unavailable" : diagnostics.Count > 0 ? "partial" : "observed", adapter, "physical_desktop_pixels", DateTimeOffset.UtcNow, nodes, truncated, diagnostics);
        }
        finally
        {
            cancellation.Dispose();
            // Cancelling a query also releases its two owned client connections; the app's own
            // accessibility connection and the environment's services remain untouched.
            foreach (var connection in new[] { bus, session }) if (connection != 0)
            { g_dbus_connection_close_sync(connection, 0, out var error); if (error != 0) g_error_free(error); g_object_unref(connection); }
            g_object_unref(cancel);
        }

        nint Connect(string address)
        {
            token.ThrowIfCancellationRequested();
            var result = g_dbus_connection_new_for_address_sync(address, 9, 0, cancel, out var error);
            if (error != 0) g_error_free(error);
            if (result == 0) throw new NativeAccessibilityUnavailable();
            return result;
        }
        Variant Invoke(nint connection, string owner, string path, string iface, string method, string signature, params nint[] args)
        {
            // Sink argument ownership before checking cancellation, including pre-built strings.
            using var parameters = new Variant(g_variant_ref_sink(g_variant_new_tuple(args, (nuint)args.Length)));
            token.ThrowIfCancellationRequested();
            var type = g_variant_type_new(signature);
            try
            {
                var result = g_dbus_connection_call_sync(connection, owner, path, iface, method, parameters.Handle, type, 1, 250, cancel, out var error);
                if (error != 0) g_error_free(error);
                if (result == 0) throw new NativeAccessibilityUnavailable();
                if (g_variant_get_size(result) > 1048576) { g_variant_unref(result); throw new NativeAccessibilityUnavailable(); }
                return new(result);
            }
            finally { g_variant_type_free(type); }
        }
        Variant PropertyValue(string owner, string path, string property)
        {
            using var reply = Invoke(bus, owner, path, "org.freedesktop.DBus.Properties", "Get", "(v)", g_variant_new_string(Accessible), g_variant_new_string(property));
            using var wrapped = reply.Child(0); return new(g_variant_get_variant(wrapped.Handle));
        }
        string Property(string owner, string path, string property) { using var value = PropertyValue(owner, path, property); return value.String(truncate: property == "Name"); }
        IReadOnlyList<string> Children(string owner, string path, int limit)
        {
            using var count = PropertyValue(owner, path, "ChildCount"); var total = count.Int();
            var result = new List<string>(); truncated |= total > limit;
            for (var i = 0; i < Math.Min(total, limit); i++)
            {
                using var reply = Invoke(bus, owner, path, Accessible, "GetChildAtIndex", "((so))", g_variant_new_int32(i));
                using var pair = reply.Child(0); using var childOwner = pair.Child(0); using var childPath = pair.Child(1);
                if (childOwner.String() == owner) result.Add(childPath.String());
                else { truncated = true; diagnostics.Add(new("native_accessibility_foreign_child_excluded", "A native child with a different D-Bus owner was excluded.")); }
            }
            return result;
        }
        NodeBounds Extents(string owner, string path)
        {
            using var reply = Invoke(bus, owner, path, "org.a11y.atspi.Component", "GetExtents", "((iiii))", g_variant_new_uint32(0));
            using var rectangle = reply.Child(0); using var x = rectangle.Child(0); using var y = rectangle.Child(1); using var w = rectangle.Child(2); using var h = rectangle.Child(3);
            return new(x.Int(), y.Int(), w.Int(), h.Int());
        }
    }

    private static bool LocalBus(string? address) => address is { Length: > 0 and <= 4096 } && !address.Contains(';')
        && (address.StartsWith("unix:path=", StringComparison.Ordinal) || address.StartsWith("unix:abstract=", StringComparison.Ordinal));
    private sealed class NativeAccessibilityUnavailable : Exception;
    private sealed class Variant(nint handle) : IDisposable
    {
        internal nint Handle { get; } = handle;
        internal nuint Count => g_variant_n_children(Handle);
        internal Variant Child(nuint index) => new(g_variant_get_child_value(Handle, index));
        internal string String(bool truncate = false)
        {
            var type = Marshal.PtrToStringUTF8(g_variant_get_type_string(Handle));
            if (type is not ("s" or "o")) throw new NativeAccessibilityUnavailable();
            var pointer = g_variant_get_string(Handle, out var length);
            if (length > 4096) throw new NativeAccessibilityUnavailable();
            var text = Marshal.PtrToStringUTF8(pointer, (int)length) ?? "";
            if (text.Length <= 256) return text;
            if (!truncate) throw new NativeAccessibilityUnavailable();
            return text[..256];
        }
        internal int Int() { if (Marshal.PtrToStringUTF8(g_variant_get_type_string(Handle)) != "i") throw new NativeAccessibilityUnavailable(); return g_variant_get_int32(Handle); }
        internal uint UInt() { if (Marshal.PtrToStringUTF8(g_variant_get_type_string(Handle)) != "u") throw new NativeAccessibilityUnavailable(); return g_variant_get_uint32(Handle); }
        public void Dispose() => g_variant_unref(Handle);
    }
    private const string Gio = "libgio-2.0.so.0", Glib = "libglib-2.0.so.0", GObject = "libgobject-2.0.so.0";
    [DllImport(Gio)] private static extern nint g_cancellable_new();
    [DllImport(Gio)] private static extern void g_cancellable_cancel(nint cancel);
    [DllImport(Gio)] private static extern nint g_dbus_connection_new_for_address_sync([MarshalAs(UnmanagedType.LPUTF8Str)] string address, uint flags, nint observer, nint cancellable, out nint error);
    [DllImport(Gio)] private static extern nint g_dbus_connection_call_sync(nint connection, [MarshalAs(UnmanagedType.LPUTF8Str)] string owner, [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string iface, [MarshalAs(UnmanagedType.LPUTF8Str)] string method, nint args, nint type, uint flags, int timeout, nint cancel, out nint error);
    [DllImport(Gio)] private static extern nint g_dbus_connection_get_unique_name(nint connection);
    [DllImport(Gio)] private static extern int g_dbus_connection_close_sync(nint connection, nint cancellable, out nint error);
    [DllImport(GObject)] private static extern void g_object_unref(nint value);
    [DllImport(Glib)] private static extern void g_error_free(nint value);
    [DllImport(Glib)] private static extern nint g_variant_new_tuple(nint[] children, nuint length);
    [DllImport(Glib)] private static extern nint g_variant_ref_sink(nint value);
    [DllImport(Glib)] private static extern nint g_variant_new_string([MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport(Glib)] private static extern nint g_variant_new_int32(int value);
    [DllImport(Glib)] private static extern nint g_variant_new_uint32(uint value);
    [DllImport(Glib)] private static extern nint g_variant_get_child_value(nint value, nuint index);
    [DllImport(Glib)] private static extern nint g_variant_get_variant(nint value);
    [DllImport(Glib)] private static extern nuint g_variant_n_children(nint value);
    [DllImport(Glib)] private static extern nuint g_variant_get_size(nint value);
    [DllImport(Glib)] private static extern nint g_variant_get_string(nint value, out nuint length);
    [DllImport(Glib)] private static extern nint g_variant_get_type_string(nint value);
    [DllImport(Glib)] private static extern int g_variant_get_int32(nint value);
    [DllImport(Glib)] private static extern uint g_variant_get_uint32(nint value);
    [DllImport(Glib)] private static extern void g_variant_unref(nint value);
    [DllImport(Glib)] private static extern nint g_variant_type_new([MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport(Glib)] private static extern void g_variant_type_free(nint value);
}
