using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Mcp;

public sealed class AgentRecipeTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "avascope-agent-recipe-" + Guid.NewGuid().ToString("N"))).FullName;

    [Fact]
    public async Task PublishedRecipeUsesCurrentToolsAndCompilesItsMultiWindowDefinition()
    {
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(Repository(), "docs", "agent-recipes.json")))!;
        var recipes = document["recipes"]!.AsArray();
        var tools = AvaScopeCapabilityCatalog.Current().Tools;
        Assert.Equal(recipes.Count, recipes.Select(recipe => recipe!["id"]!.GetValue<string>()).Distinct().Count());
        foreach (var recipe in recipes)
        {
            Assert.Contains(tools, tool => tool.Adapter == "mcp" && tool.Name == recipe!["tool"]!.GetValue<string>());
            Assert.Contains(tools, tool => tool.Adapter == "cli" && tool.Name == recipe!["cli"]!.GetValue<string>());
        }
        var workflow = recipes.Single(recipe => recipe!["id"]!.GetValue<string>() == "multi-window")!["arguments"]!["request"]!;
        workflow["sessionId"] = "recipe-validation";
        workflow["outputDirectory"] = Path.Combine(_root, "workflow");
        workflow["evidence"]!["policy"]!["ownedEvidenceRoot"] = _root;
        workflow["evidence"]!["policy"]!["redactedText"] = new JsonArray("recipe-private-canary");
        workflow["validateOnly"] = true;
        var request = workflow.Deserialize<SemanticWorkflowRequest>()!;
        var validated = await new SemanticWorkflowRunner().RunAsync(new(), request);
        Assert.True(validated.Success, validated.Error?.Message);
        Assert.Equal("validated", validated.Value!.Status);
        Assert.All(request.Steps, step => { Assert.NotNull(step.Verify); Assert.Null(step.Selector?.NodeId); Assert.Null(step.WaitMs); });
    }

    [Fact]
    public async Task ActualMcpAttemptsAreCountedWithoutArgumentsAndUnknownRetriesStayUnknown()
    {
        var request = Path.Combine(_root, "request.json");
        var trace = Path.Combine(_root, "tool-events.jsonl");
        await File.WriteAllTextAsync(request, "{\"unusedPrivateArgument\":\"private-agent-canary\"}");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var probe = Path.Combine(Repository(), "tests", "AvaScope.McpScenarioClient", "bin", configuration, "net10.0", "AvaScope.McpScenarioClient.dll");
        for (var index = 0; index < 2; index++)
        {
            var measured = await Run("dotnet", [probe, Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll"), request, _root, "health"],
                new() { ["AVASCOPE_AGENT_TRACE_FILE"] = trace, ["AVASCOPE_AGENT_TRIAL_ID"] = "trial", ["AVASCOPE_AGENT_CONTEXT"] = "unchanged", ["AVASCOPE_AGENT_RETRY"] = "none" });
            Assert.Equal(0, measured.Exit);
        }
        var lines = await File.ReadAllLinesAsync(trace);
        Assert.Equal(2, lines.Length);
        Assert.DoesNotContain("private-agent-canary", string.Join("\n", lines));
        Assert.DoesNotContain("unusedPrivateArgument", string.Join("\n", lines));
        var entry = JsonNode.Parse(lines[0])!;
        Assert.Equal("agent_tool_capture", entry["mode"]!.GetValue<string>());
        Assert.True(entry["transportCompleted"]!.GetValue<bool>());
        var evidence = Path.Combine(_root, "evidence.json");
        await File.WriteAllTextAsync(evidence, "{\"verified\":true}");
        var outcomes = Path.Combine(_root, "outcomes.json");
        await File.WriteAllTextAsync(outcomes, JsonSerializer.Serialize(new
        {
            schemaVersion = 1, mode = "agent_outcomes", client = "unit-fixture", model = "unreported", configuration = new { backend = "headless" },
            trials = new[] { new { trialId = "trial", expectedClassification = "environment", reportedClassification = "application", taskCompleted = false, evidencePaths = new[] { evidence } } }
        }));
        var report = Path.Combine(_root, "measured.json");
        var measuredReport = await Run("pwsh", ["-NoProfile", "-File", Path.Combine(Repository(), "eng", "measure-agent-evaluation.ps1"),
            "-TracePath", trace, "-OutcomesPath", outcomes, "-OutputPath", report]);
        Assert.True(measuredReport.Exit == 0, measuredReport.Output);
        var result = JsonNode.Parse(await File.ReadAllTextAsync(report))!;
        Assert.Equal(2, result["toolCalls"]!.GetValue<int>());
        Assert.Equal(0, result["completedTrials"]!.GetValue<int>());
        Assert.Equal(0, result["correctlyClassifiedTrials"]!.GetValue<int>());
        Assert.Equal(1, result["trials"]![0]!["unknownRetryJudgments"]!.GetValue<int>());
        Assert.Equal(0, result["trials"]![0]!["unnecessaryRetries"]!.GetValue<int>());
        await File.WriteAllTextAsync(trace, string.Join("\n", lines.Select(line => line.Replace("agent_tool_capture", "deterministic_conformance", StringComparison.Ordinal))));
        var rejected = await Run("pwsh", ["-NoProfile", "-File", Path.Combine(Repository(), "eng", "measure-agent-evaluation.ps1"),
            "-TracePath", trace, "-OutcomesPath", outcomes, "-OutputPath", report]);
        Assert.NotEqual(0, rejected.Exit);
        Assert.Contains("cannot be relabeled", rejected.Output);
    }

    private static async Task<(int Exit, string Output)> Run(string command, string[] arguments, Dictionary<string, string>? environment = null)
    {
        var start = new ProcessStartInfo(command) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in arguments) start.ArgumentList.Add(arg);
        foreach (var pair in environment ?? []) start.Environment[pair.Key] = pair.Value;
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45)); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        return (process.ExitCode, await output + await error);
    }

    private static string Repository()
    {
        for (var path = new DirectoryInfo(AppContext.BaseDirectory); path is not null; path = path.Parent)
            if (File.Exists(Path.Combine(path.FullName, "AvaScope.slnx"))) return path.FullName;
        throw new DirectoryNotFoundException();
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
