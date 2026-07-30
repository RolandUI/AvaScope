namespace AvaScope.Tests;

internal static class TestPipeNames
{
    public static string New()
    {
        return $"avt-{Guid.NewGuid():N}";
    }
}
