using System.Text.Json;
using System.Xml.Linq;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class SemanticWorkflowReportPackExporterTests
{
    [Theory]
    [InlineData("passed", "passed", 0)]
    [InlineData("failed", "failed", 1)]
    public void ExportWritesAlignedJsonMarkdownAndJunit(
        string workflowStatus,
        string stepStatus,
        int expectedFailures)
    {
        var directory = NewDirectory();
        try
        {
            var at = DateTimeOffset.UtcNow;
            var response = new SemanticWorkflowResponse(
                "report-workflow",
                new SessionId("report-session"),
                "topLevel:main",
                workflowStatus,
                at,
                at.AddMilliseconds(20),
                [
                    new SemanticWorkflowStepResult(
                        "save",
                        SemanticWorkflowActions.Invoke,
                        stepStatus,
                        stepStatus == "passed" ? "Saved." : "Verification failed.",
                        at,
                        diagnostics: stepStatus == "failed"
                            ? [new ProtocolError("semantic_workflow_wait_timeout", "Verification timed out.")]
                            : [])
                ]);

            var result = new SemanticWorkflowReportPackExporter().Export(response, directory);

            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(workflowStatus, result.Value!.Status);
            Assert.Equal(3, result.Value.Assets.Count);
            Assert.Equal(expectedFailures, result.Value.FailedEntries);
            var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "workflow-report.json")));
            Assert.Equal(workflowStatus, json.RootElement.GetProperty("workflow").GetProperty("status").GetString());
            var markdown = File.ReadAllText(Path.Combine(directory, "workflow-report.md"));
            Assert.Contains($"- Status: `{workflowStatus}`", markdown, StringComparison.Ordinal);
            var junit = XDocument.Load(Path.Combine(directory, "workflow-junit.xml"));
            Assert.Equal(expectedFailures.ToString(), junit.Root!.Attribute("failures")!.Value);
            Assert.Equal("1", junit.Root.Attribute("tests")!.Value);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ExportPreservesSuccessfulAssetsWhenOneArtifactFails()
    {
        var directory = NewDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "workflow-report.md"));
            var at = DateTimeOffset.UtcNow;
            var response = new SemanticWorkflowResponse(
                "partial-report",
                new SessionId("report-session"),
                "topLevel:main",
                "failed",
                at,
                at,
                [new SemanticWorkflowStepResult("save", SemanticWorkflowActions.Invoke, "failed", "Failed.", at)]);

            var result = new SemanticWorkflowReportPackExporter().Export(response, directory);

            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal("partial", result.Value!.Status);
            Assert.Equal("partial", result.Value.Metadata["artifactStatus"]);
            Assert.DoesNotContain(result.Value.Assets, static asset => asset.Kind == "markdown");
            Assert.True(File.Exists(Path.Combine(directory, "workflow-report.json")));
            Assert.True(File.Exists(Path.Combine(directory, "workflow-junit.xml")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string NewDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "AvaScope.Tests", $"workflow-report-{Guid.NewGuid():N}");
    }
}
