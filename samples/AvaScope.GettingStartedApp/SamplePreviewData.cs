using System.Globalization;

namespace AvaScope.GettingStartedApp;

public sealed class SamplePreviewData
{
    public string Heading { get; } = "AvaScope getting started";

    public string Summary { get; } = "This sample is small enough to inspect, preview, and run with the opt-in local bridge.";

    public string CultureLabel => $"Preview culture: {CultureInfo.CurrentCulture.Name}";

    public SampleStatus Status { get; } = new(
        "Bridge is opt-in",
        "Set AVASCOPE_SAMPLE_BRIDGE=1 before running the sample to publish a local-only AvaScope session.");
}

public sealed record SampleStatus(string Title, string Detail);
