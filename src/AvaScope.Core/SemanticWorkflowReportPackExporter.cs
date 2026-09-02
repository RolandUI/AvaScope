using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class SemanticWorkflowReportPackExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public CoreResult<AgentEvidenceReportPackResponse> Export(
        SemanticWorkflowResponse response,
        string reportDirectory)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (string.IsNullOrWhiteSpace(reportDirectory))
        {
            return Unavailable("Workflow report directory cannot be empty.");
        }

        string directory;
        try
        {
            directory = Path.GetFullPath(reportDirectory);
            Directory.CreateDirectory(directory);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException or PathTooLongException or UnauthorizedAccessException)
        {
            return Unavailable($"Workflow report directory is unavailable: {exception.Message}");
        }

        var syntheticFailure = !string.Equals(response.Status, "passed", StringComparison.Ordinal)
            && response.Steps.All(static step => !string.Equals(step.Status, "failed", StringComparison.Ordinal));
        var failedEntries = response.Steps.Count(static step => string.Equals(step.Status, "failed", StringComparison.Ordinal))
            + (syntheticFailure ? 1 : 0);
        var totalEntries = response.Steps.Count + (syntheticFailure ? 1 : 0);
        var generatedAt = DateTimeOffset.UtcNow;
        var intended = new[]
        {
            new AgentEvidenceReportPackAsset("json", Path.Combine(directory, "workflow-report.json"), "application/json", "Machine-readable workflow, verification, and failure evidence report."),
            new AgentEvidenceReportPackAsset("markdown", Path.Combine(directory, "workflow-report.md"), "text/markdown", "Human-readable workflow execution and evidence summary."),
            new AgentEvidenceReportPackAsset("junit", Path.Combine(directory, "workflow-junit.xml"), "application/xml", "JUnit-compatible workflow and step status summary.")
        };
        var successful = new List<AgentEvidenceReportPackAsset>(intended.Length);
        var unavailable = new List<string>();

        var initialPack = CreatePack(
            directory,
            response.Status,
            generatedAt,
            totalEntries,
            failedEntries,
            intended,
            unavailable);
        TryWrite(intended[0], () => File.WriteAllText(
            intended[0].Path,
            JsonSerializer.Serialize(new { reportPack = initialPack, workflow = response }, JsonOptions),
            Encoding.UTF8));
        TryWrite(intended[1], () => File.WriteAllText(
            intended[1].Path,
            CreateMarkdown(response, initialPack),
            Encoding.UTF8));
        TryWrite(intended[2], () => CreateJUnit(response, initialPack, syntheticFailure).Save(intended[2].Path));

        var finalPack = CreatePack(
            directory,
            successful.Count == intended.Length ? response.Status : "partial",
            generatedAt,
            totalEntries,
            failedEntries,
            successful,
            unavailable);
        if (successful.Contains(intended[0]))
        {
            try
            {
                File.WriteAllText(
                    intended[0].Path,
                    JsonSerializer.Serialize(new { reportPack = finalPack, workflow = response }, JsonOptions),
                    Encoding.UTF8);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                successful.Remove(intended[0]);
                unavailable.Add($"json: {exception.Message}");
                finalPack = CreatePack(
                    directory,
                    "partial",
                    generatedAt,
                    totalEntries,
                    failedEntries,
                    successful,
                    unavailable);
            }
        }

        return CoreResult<AgentEvidenceReportPackResponse>.Ok(finalPack);

        void TryWrite(AgentEvidenceReportPackAsset asset, Action write)
        {
            try
            {
                write();
                successful.Add(asset);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                unavailable.Add($"{asset.Kind}: {exception.Message}");
            }
        }
    }

    private static AgentEvidenceReportPackResponse CreatePack(
        string directory,
        string status,
        DateTimeOffset generatedAt,
        int totalEntries,
        int failedEntries,
        IReadOnlyList<AgentEvidenceReportPackAsset> assets,
        IReadOnlyList<string> unavailable)
    {
        return new AgentEvidenceReportPackResponse(
            directory,
            status,
            generatedAt,
            totalEntries,
            totalEntries - failedEntries,
            failedEntries,
            assets,
            new Dictionary<string, string>
            {
                ["os"] = RuntimeInformation.OSDescription,
                ["framework"] = RuntimeInformation.FrameworkDescription,
                ["processArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString()
            },
            new Dictionary<string, string>
            {
                ["kind"] = "semantic-workflow",
                ["unavailableAssets"] = string.Join(" | ", unavailable),
                ["artifactStatus"] = unavailable.Count == 0 ? "complete" : "partial"
            });
    }

    private static string CreateMarkdown(
        SemanticWorkflowResponse response,
        AgentEvidenceReportPackResponse pack)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# AvaScope Workflow Evidence");
        builder.AppendLine();
        builder.AppendLine($"- Request: `{response.RequestId}`");
        builder.AppendLine($"- Status: `{response.Status}`");
        builder.AppendLine($"- Started: `{response.StartedAt:O}`");
        builder.AppendLine($"- Completed: `{response.CompletedAt:O}`");
        builder.AppendLine($"- Entries: `{pack.TotalEntries}` total, `{pack.FailedEntries}` failed");
        builder.AppendLine();
        builder.AppendLine("## Steps");
        builder.AppendLine();
        builder.AppendLine("| # | Path | Step | Action | Status | Verify | Failure evidence | Message |");
        builder.AppendLine("| - | ---- | ---- | ------ | ------ | ------ | ---------------- | ------- |");
        for (var index = 0; index < response.Steps.Count; index++)
        {
            var step = response.Steps[index];
            builder.AppendLine(
                $"| {(index + 1).ToString(CultureInfo.InvariantCulture)} | {Escape(step.ExecutionPath)} | {Escape(step.StepId)} | {Escape(step.Action)} | {Escape(step.Status)} | {Escape(step.Verification?.Status)} | {Escape(step.FailureEvidence?.Status)} | {Escape(step.Message)} |");
        }

        if (response.Diagnostics.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Diagnostics");
            foreach (var diagnostic in response.Diagnostics)
            {
                builder.AppendLine($"- `{diagnostic.Code}` {diagnostic.Message}");
            }
        }

        var evidence = response.Steps
            .Where(static step => step.FailureEvidence is not null)
            .ToArray();
        if (evidence.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Failure evidence");
            foreach (var step in evidence)
            {
                var item = step.FailureEvidence!;
                builder.AppendLine($"- `{step.StepId}`: `{item.Status}` at `{item.ArtifactDirectory}`");
                foreach (var unavailable in item.UnavailableEvidence)
                {
                    builder.AppendLine($"  - unavailable: {unavailable}");
                }
            }
        }

        return builder.ToString();
    }

    private static XDocument CreateJUnit(
        SemanticWorkflowResponse response,
        AgentEvidenceReportPackResponse pack,
        bool syntheticFailure)
    {
        var suite = new XElement(
            "testsuite",
            new XAttribute("name", "AvaScope semantic workflow"),
            new XAttribute("tests", pack.TotalEntries.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("failures", pack.FailedEntries.ToString(CultureInfo.InvariantCulture)),
            new XAttribute("errors", "0"),
            new XAttribute("time", Math.Max(0, (response.CompletedAt - response.StartedAt).TotalSeconds).ToString("0.###", CultureInfo.InvariantCulture)),
            new XAttribute("timestamp", response.StartedAt.ToString("O", CultureInfo.InvariantCulture)));

        foreach (var step in response.Steps)
        {
            var test = new XElement(
                "testcase",
                new XAttribute("classname", "AvaScope.SemanticWorkflow"),
                new XAttribute("name", step.ExecutionPath ?? step.StepId),
                new XAttribute("time", "0"));
            if (string.Equals(step.Status, "failed", StringComparison.Ordinal))
            {
                test.Add(new XElement(
                    "failure",
                    new XAttribute("message", step.Message),
                    string.Join(Environment.NewLine, step.Diagnostics.Select(static diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"))));
            }
            else if (step.Status is "skipped" or "retried")
            {
                test.Add(new XElement("skipped", new XAttribute("message", step.Message)));
            }

            suite.Add(test);
        }

        if (syntheticFailure)
        {
            var message = response.Diagnostics.FirstOrDefault()?.Message ?? "Workflow failed without a completed failing step.";
            suite.Add(new XElement(
                "testcase",
                new XAttribute("classname", "AvaScope.SemanticWorkflow"),
                new XAttribute("name", "workflow"),
                new XAttribute("time", "0"),
                new XElement("failure", new XAttribute("message", message), message)));
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", null), suite);
    }

    private static string Escape(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("|", "\\|", StringComparison.Ordinal).Replace(Environment.NewLine, " ", StringComparison.Ordinal);

    private static CoreResult<AgentEvidenceReportPackResponse> Unavailable(string message) =>
        CoreResult<AgentEvidenceReportPackResponse>.Fail(new CoreError(
            CoreErrorCodes.AgentEvidenceReportPackUnavailable,
            message));
}
