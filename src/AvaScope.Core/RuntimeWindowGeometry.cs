using AvaScope.Protocol;

namespace AvaScope.Core;

public static class RuntimeWindowGeometry
{
    public static CoreResult<RuntimeWindowPosition> ResolvePosition(RuntimeWindowPosition position, IReadOnlyList<RuntimeMonitorInfo> monitors)
    {
        ArgumentNullException.ThrowIfNull(position); ArgumentNullException.ThrowIfNull(monitors);
        var x = position.X; var y = position.Y;
        RuntimeMonitorInfo? selected = null;
        if (position.CoordinateSpace == "monitor_dip")
        {
            selected = monitors.SingleOrDefault(monitor => monitor.Id == position.MonitorId);
            if (selected is null) return Fail("window_monitor_stale", "Select a monitor from the current window inspection.");
            if (!double.IsFinite(selected.DesktopScale) || selected.DesktopScale <= 0) return Fail("window_monitor_scale_unavailable", "This monitor has no usable desktop scale.");
            x = selected.WorkArea.X + position.X * selected.DesktopScale;
            y = selected.WorkArea.Y + position.Y * selected.DesktopScale;
        }
        x = Math.Round(x, MidpointRounding.AwayFromZero); y = Math.Round(y, MidpointRounding.AwayFromZero);
        if (!double.IsFinite(x) || !double.IsFinite(y) || Math.Abs(x) > 1000000 || Math.Abs(y) > 1000000)
            return Fail("window_position_range", "Resolved desktop position exceeds supported bounds.");
        bool Contains(RuntimeMonitorInfo monitor) => x >= monitor.WorkArea.X && y >= monitor.WorkArea.Y
            && x < monitor.WorkArea.X + monitor.WorkArea.Width && y < monitor.WorkArea.Y + monitor.WorkArea.Height;
        if (selected is not null ? !Contains(selected) : !monitors.Any(Contains))
            return Fail("window_position_offscreen", "Choose a position inside a current monitor work area; hidden off-screen placement is not supported.");
        return CoreResult<RuntimeWindowPosition>.Ok(new(x, y));

        static CoreResult<RuntimeWindowPosition> Fail(string code, string message) => CoreResult<RuntimeWindowPosition>.Fail(new(code, message));
    }
}
