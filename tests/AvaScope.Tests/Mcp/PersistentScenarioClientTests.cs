using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Protocol;
using AvaScope.Tests.Bridge;

namespace AvaScope.Tests.Mcp;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class PersistentScenarioClientTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "avascope-probe-utf8-" + Guid.NewGuid().ToString("N"))).FullName;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RawUtf8EscapedJsonReplayAndFileRequestsPreserveIndependentApplicationText(bool fullResult)
    {
        var requestPath = Path.Combine(_root, "request.json");
        await File.WriteAllTextAsync(requestPath, "{}");
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate(); var runtime = AvaScopeBridge.Activate();
                var editor = new TextBox { Name = "UnicodeEditor", Text = "seed", AcceptsReturn = true };
                var changes = new List<string?>();
                editor.PropertyChanged += (_, change) => { if (change.Property == TextBox.TextProperty) changes.Add(editor.Text); };
                var window = new Window { Width = 400, Height = 200, Content = editor }; window.Show();
                try
                {
                    using var registration = runtime.RegisterTopLevel(window);
                    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    var target = Assert.Single((await runtime.FindNodesAsync(top.Id, TreeKinds.Visual, name: "UnicodeEditor")).Value!.Matches).Target!;
                    var manifestDirectory = Path.GetDirectoryName(runtime.SessionManifestPath)!;
                    using var process = Start(requestPath, manifestDirectory, "--stdio-session", fullResult);
                    var stderr = process.StandardError.ReadToEndAsync();
                    try
                    {
                        var values = new[] { "Native keyboard — Árvíztűrő 😀 日本語", "Második kör — 中文 🚀", "Escaped \"quote\"\nline — ő 😀" };
                        string? lastLine = null;
                        for (var index = 0; index < values.Length; index++)
                        {
                            var arguments = new { request = new RuntimeDesiredStateRequest(target, "text", JsonSerializer.SerializeToElement(values[index]), "utf8-" + index), manifestDirectory };
                            var options = index < 2 ? new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping } : null;
                            var line = JsonSerializer.Serialize(new { tool = "ensure_state", arguments }, options);
                            if (index < 2)
                            {
                                // Force actual four-byte UTF-8 on the wire even if the JSON encoder escapes supplementary characters.
                                line = line.Replace("\\uD83D\\uDE00", "😀", StringComparison.OrdinalIgnoreCase)
                                    .Replace("\\uD83D\\uDE80", "🚀", StringComparison.OrdinalIgnoreCase);
                                Assert.Contains(index == 0 ? "日本語" : "中文", line);
                                Assert.Contains(index == 0 ? "😀" : "🚀", line);
                            }
                            else Assert.All(line, character => Assert.True(character <= 127));
                            await process.StandardInput.BaseStream.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"));
                            await process.StandardInput.BaseStream.FlushAsync();
                            using var response = JsonDocument.Parse((await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)))!);
                            var result = fullResult ? response.RootElement.GetProperty("structuredContent") : response.RootElement;
                            Assert.True(result.GetProperty("success").GetBoolean(), result.ToString());
                            Assert.Equal(values[index], result.GetProperty("value").GetProperty("after").GetProperty("value").GetString());
                            Assert.Equal(values[index], editor.Text);
                            Assert.Equal(index + 1, changes.Count);
                            lastLine = line;
                        }
                        await process.StandardInput.BaseStream.WriteAsync(Encoding.UTF8.GetBytes(lastLine + "\n"));
                        await process.StandardInput.BaseStream.FlushAsync();
                        using var replay = JsonDocument.Parse((await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)))!);
                        var replayResult = fullResult ? replay.RootElement.GetProperty("structuredContent") : replay.RootElement;
                        Assert.True(replayResult.GetProperty("value").GetProperty("replayed").GetBoolean());
                        Assert.Equal(3, changes.Count);
                        process.StandardInput.Close();
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                        Assert.True(process.ExitCode == 0, await stderr);
                        Assert.Equal(string.Empty, await process.StandardOutput.ReadToEndAsync());
                    }
                    finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }

                    const string fromFile = "UTF-8 file — Árvíz 日本語 😀";
                    var fileArguments = new { request = new RuntimeDesiredStateRequest(target, "text", JsonSerializer.SerializeToElement(fromFile), "file-utf8"), manifestDirectory };
                    await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(fileArguments, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                    using var fileProcess = Start(requestPath, manifestDirectory, "ensure_state", fullResult);
                    var fileError = fileProcess.StandardError.ReadToEndAsync();
                    var fileOutput = fileProcess.StandardOutput.ReadToEndAsync();
                    try
                    {
                        fileProcess.StandardInput.Close();
                        await fileProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                        Assert.True(fileProcess.ExitCode == 0, await fileError);
                        using var result = JsonDocument.Parse(await fileOutput);
                        Assert.True((fullResult ? result.RootElement.GetProperty("structuredContent") : result.RootElement).GetProperty("success").GetBoolean());
                        Assert.Equal(fromFile, editor.Text); Assert.Equal(4, changes.Count);
                    }
                    finally { if (!fileProcess.HasExited) fileProcess.Kill(entireProcessTree: true); }
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally
        {
            // A failed dispatch may resume this continuation inline on the headless worker.
            // Dispose joins that worker, so it must execute on another thread.
            await Task.Run(() => BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session));
        }
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("truncated")]
    [InlineData("invalid_utf8")]
    [InlineData("oversized")]
    public async Task InvalidPersistentCommandsFailWithoutAnotherToolResponse(string kind)
    {
        var request = Path.Combine(_root, "request.json"); await File.WriteAllTextAsync(request, "{}");
        using var process = Start(request, _root, "--stdio-session", true);
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.StandardInput.BaseStream.WriteAsync(Encoding.UTF8.GetBytes("{\"tool\":\"health\",\"arguments\":{}}\n"));
            await process.StandardInput.BaseStream.FlushAsync();
            using var first = JsonDocument.Parse((await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)))!);
            Assert.True(first.RootElement.GetProperty("structuredContent").GetProperty("success").GetBoolean());
            var bytes = kind switch
            {
                "malformed" => Encoding.UTF8.GetBytes("{broken}\n"),
                "truncated" => Encoding.UTF8.GetBytes("{\"tool\":\"health\",\"arguments\":{\"note\":\"unterminated"),
                "invalid_utf8" => Encoding.ASCII.GetBytes("{\"tool\":\"health\",\"arguments\":{\"note\":\"").Concat(new byte[] { 0xff }).Concat(Encoding.ASCII.GetBytes("\"}}\n")).ToArray(),
                _ => Encoding.UTF8.GetBytes(new string('x', 1024 * 1024 + 1) + "\n")
            };
            await process.StandardInput.BaseStream.WriteAsync(bytes);
            await process.StandardInput.BaseStream.FlushAsync();
            process.StandardInput.Close();
            var rest = process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.NotEqual(0, process.ExitCode);
            Assert.Equal(string.Empty, await rest);
            var error = await stderr;
            Assert.Contains("probe command", error, StringComparison.OrdinalIgnoreCase);
            Assert.True(error.Length < 4096, "Malformed input must not be echoed into an unbounded error.");
        }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    }

    private static Process Start(string requestPath, string manifestDirectory, string mode, bool fullResult)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "AvaScope.slnx"))) repository = repository.Parent;
        Assert.NotNull(repository);
        var probe = Path.Combine(repository.FullName, "tests", "AvaScope.McpScenarioClient", "bin", configuration, "net10.0", "AvaScope.McpScenarioClient.dll");
        Assert.True(File.Exists(probe), "Build the solution's scenario client before running its subprocess tests.");
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in new[] { probe, Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll"), requestPath, manifestDirectory, mode })
            start.ArgumentList.Add(argument);
        if (fullResult) start.ArgumentList.Add("--full-result");
        return Process.Start(start)!;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
