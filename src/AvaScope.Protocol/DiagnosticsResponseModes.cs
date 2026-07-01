namespace AvaScope.Protocol;

public static class DiagnosticsResponseModes
{
    public const string All = "all";
    public const string ActiveOnly = "active-only";
    public const string Minimal = "minimal";
    public const string JsonMinimal = "json-minimal";

    public static bool TryNormalize(string? mode, out string normalized)
    {
        normalized = All;
        if (string.IsNullOrWhiteSpace(mode))
        {
            return true;
        }

        normalized = mode.Trim().ToLowerInvariant();
        if (normalized is "active")
        {
            normalized = ActiveOnly;
        }

        return normalized is All or ActiveOnly or Minimal or JsonMinimal;
    }
}
