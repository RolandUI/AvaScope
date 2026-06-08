namespace AvaScope.Core;

public sealed record PreviewSessionWatchOptions
{
    public PreviewSessionWatchOptions(
        TimeSpan timeout,
        TimeSpan settleDelay,
        int maxReloads,
        IReadOnlyList<string>? watchPaths = null)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Watch timeout must be positive.");
        }

        if (settleDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(settleDelay), settleDelay, "Watch settle delay cannot be negative.");
        }

        if (maxReloads < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxReloads), maxReloads, "Max reloads must be positive.");
        }

        Timeout = timeout;
        SettleDelay = settleDelay;
        MaxReloads = maxReloads;
        WatchPaths = (watchPaths ?? [])
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public TimeSpan Timeout { get; }

    public TimeSpan SettleDelay { get; }

    public int MaxReloads { get; }

    public IReadOnlyList<string> WatchPaths { get; }
}
