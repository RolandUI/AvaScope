using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Protocol;
using AvaScope.Tests.Bridge;

namespace AvaScope.Tests.Mcp;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class PersistentScenarioClientTests(Xunit.Abstractions.ITestOutputHelper output) : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "avascope-probe-utf8-" + Guid.NewGuid().ToString("N"))).FullName;

    [Fact]
    public async Task LifecycleTraceTracksReadyResponsesAndServerExitWithoutPayloads()
    {
        var request = Path.Combine(_root, "request.json");
        await File.WriteAllTextAsync(request, "{\"unusedPrivateArgument\":\"private-lifecycle-canary\"}");
        using var process = Start(request, _root, "health", false);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Exception? processFailure = null;
        try
        {
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(process.ExitCode == 0, await stderr);
            using var response = JsonDocument.Parse(await stdout);
            Assert.True(response.RootElement.GetProperty("success").GetBoolean());
            var trace = ReadLifecycle(process);
            Assert.Contains(trace, row => row.GetProperty("stage").GetString() == "ready");
            Assert.Contains(trace, row => row.GetProperty("stage").GetString() == "response_received"
                && row.GetProperty("disposition").GetString() == "response_received_output_pending");
            var closed = Assert.Single(trace, row => row.GetProperty("stage").GetString() == "closed");
            Assert.Equal(1, closed.GetProperty("requestsStarted").GetInt32());
            Assert.Equal(1, closed.GetProperty("outputsWritten").GetInt32());
            Assert.Equal("responses_written", closed.GetProperty("disposition").GetString());
            Assert.True(closed.GetProperty("serverProcessId").GetInt32() > 0);
            Assert.Equal(JsonValueKind.Number, closed.GetProperty("serverExitCode").ValueKind);
            Assert.DoesNotContain("private-lifecycle-canary", JsonSerializer.Serialize(trace));
            Assert.DoesNotContain("unusedPrivateArgument", JsonSerializer.Serialize(trace));
        }
        catch (Exception exception) { processFailure = exception; throw; }
        finally { await FinishProcessAsync(process, processFailure); }
    }

    [Fact]
    public async Task UnavailableLifecycleSinkPreservesTheActualResponseAndExistingEvidence()
    {
        var request = Path.Combine(_root, "request.json");
        var trace = Path.Combine(_root, "occupied.jsonl");
        await File.WriteAllTextAsync(request, "{}");
        await File.WriteAllTextAsync(trace, "{\"existing\":true}\n");
        using var process = Start(request, _root, "health", false, lifecyclePath: trace);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Exception? processFailure = null;
        try
        {
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(0, process.ExitCode);
            using var result = JsonDocument.Parse(await stdout);
            Assert.True(result.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("{\"existing\":true}\n", await File.ReadAllTextAsync(trace));
            Assert.Equal("Probe lifecycle evidence unavailable; do not replay requests to recover diagnostics.", (await stderr).Trim());
        }
        catch (Exception exception) { processFailure = exception; throw; }
        finally { await FinishProcessAsync(process, processFailure); }
    }

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
                var elapsed = new Stopwatch();
                var stages = new List<object>();
                var inputEvents = 0;
                void Record(string stage)
                {
                    if (elapsed.IsRunning && stages.Count < 32) stages.Add(new { stage, elapsedMs = elapsed.Elapsed.TotalMilliseconds });
                }
                editor.PropertyChanged += (_, change) =>
                {
                    if (change.Property == TextBox.TextProperty) { changes.Add(editor.Text); Record("text_changed"); }
                    else if (change.Property == TextBox.SelectionStartProperty) Record("selection_start_changed");
                    else if (change.Property == TextBox.SelectionEndProperty) Record("selection_end_changed");
                    else if (change.Property == TextBox.CaretIndexProperty) Record("caret_changed");
                    else if (change.Property == InputElement.IsFocusedProperty) Record("focus_changed");
                };
                var window = new Window { Width = 400, Height = 200, Content = editor }; window.Show();
                window.AddHandler(InputElement.TextInputEvent, (_, _) => { inputEvents++; Record("input_enter"); }, RoutingStrategies.Tunnel, handledEventsToo: true);
                window.AddHandler(InputElement.TextInputEvent, (_, _) => Record("input_exit"), RoutingStrategies.Bubble, handledEventsToo: true);
                string Observe(string requestId, string expected)
                {
                    using var frame = window.GetLastRenderedFrame();
                    return JsonSerializer.Serialize(new
                    {
                        requestId, fullResult, elapsedMs = elapsed.Elapsed.TotalMilliseconds, stages,
                        inputEvents, textChanges = changes.Count, textLength = editor.Text?.Length,
                        expectedTextObserved = editor.Text == expected, seedTextObserved = editor.Text == "seed",
                        editor.IsFocused, editor.CaretIndex, editor.SelectionStart, editor.SelectionEnd,
                        editor.IsMeasureValid, editor.IsArrangeValid, frameAvailable = frame is not null
                    });
                }
                try
                {
                    using var registration = runtime.RegisterTopLevel(window);
                    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    var target = Assert.Single((await runtime.FindNodesAsync(top.Id, TreeKinds.Visual, name: "UnicodeEditor")).Value!.Matches).Target!;
                    var manifestDirectory = Path.GetDirectoryName(runtime.SessionManifestPath)!;
                    using var process = Start(requestPath, manifestDirectory, "--stdio-session", fullResult);
                    var stderr = process.StandardError.ReadToEndAsync();
                    Exception? processFailure = null;
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
                            stages.Clear(); elapsed.Restart();
                            string? responseLine;
                            try
                            {
                                await process.StandardInput.BaseStream.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"));
                                await process.StandardInput.BaseStream.FlushAsync();
                                responseLine = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
                            }
                            finally { output.WriteLine(Observe("utf8-" + index, values[index])); }
                            using var response = JsonDocument.Parse(responseLine!);
                            var result = fullResult ? response.RootElement.GetProperty("structuredContent") : response.RootElement;
                            Assert.True(result.GetProperty("success").GetBoolean(), result + "\n" + Observe("utf8-" + index, values[index]));
                            Assert.Equal(values[index], result.GetProperty("value").GetProperty("after").GetProperty("value").GetString());
                            Assert.Equal(values[index], editor.Text);
                            Assert.Equal(index + 1, changes.Count);
                            Assert.Equal(index + 1, inputEvents);
                            elapsed.Stop();
                            lastLine = line;
                        }
                        await process.StandardInput.BaseStream.WriteAsync(Encoding.UTF8.GetBytes(lastLine + "\n"));
                        await process.StandardInput.BaseStream.FlushAsync();
                        using var replay = JsonDocument.Parse((await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)))!);
                        var replayResult = fullResult ? replay.RootElement.GetProperty("structuredContent") : replay.RootElement;
                        Assert.True(replayResult.GetProperty("value").GetProperty("replayed").GetBoolean());
                        Assert.Equal(3, changes.Count); Assert.Equal(3, inputEvents);
                        process.StandardInput.Close();
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                        Assert.True(process.ExitCode == 0, await stderr);
                        Assert.Equal(string.Empty, await process.StandardOutput.ReadToEndAsync());
                    }
                    catch (Exception exception) { processFailure = exception; throw; }
                    finally { await FinishProcessAsync(process, processFailure); }

                    const string fromFile = "UTF-8 file — Árvíz 日本語 😀";
                    var fileArguments = new { request = new RuntimeDesiredStateRequest(target, "text", JsonSerializer.SerializeToElement(fromFile), "file-utf8"), manifestDirectory };
                    await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(fileArguments, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                    stages.Clear(); elapsed.Restart();
                    using var fileProcess = Start(requestPath, manifestDirectory, "ensure_state", fullResult);
                    var fileError = fileProcess.StandardError.ReadToEndAsync();
                    var fileOutput = fileProcess.StandardOutput.ReadToEndAsync();
                    Exception? fileFailure = null;
                    try
                    {
                        fileProcess.StandardInput.Close();
                        await fileProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                        Assert.True(fileProcess.ExitCode == 0, await fileError);
                        using var result = JsonDocument.Parse(await fileOutput);
                        Assert.True((fullResult ? result.RootElement.GetProperty("structuredContent") : result.RootElement).GetProperty("success").GetBoolean());
                        Assert.Equal(fromFile, editor.Text); Assert.Equal(4, changes.Count); Assert.Equal(4, inputEvents);
                    }
                    catch (Exception exception) { fileFailure = exception; throw; }
                    finally
                    {
                        output.WriteLine(Observe("file-utf8", fromFile));
                        await FinishProcessAsync(fileProcess, fileFailure);
                    }
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
    [InlineData("connecting", "no_tool_request_started", 0)]
    [InlineData("request_started", "request_outcome_unknown", 1)]
    [InlineData("response_received", "response_received_output_pending", 1)]
    [InlineData("closing", "responses_written", 1)]
    public async Task ControlledLifecycleDelayDistinguishesStartupFromAnObservedEdit(string stage, string disposition, int expectedEdits)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate(); var runtime = AvaScopeBridge.Activate();
                var editor = new TextBox { Name = "LifecycleEditor", Text = "seed" };
                var edits = 0;
                editor.PropertyChanged += (_, change) => { if (change.Property == TextBox.TextProperty) edits++; };
                var window = new Window { Width = 400, Height = 200, Content = editor }; window.Show();
                try
                {
                    using var registration = runtime.RegisterTopLevel(window);
                    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    var target = Assert.Single((await runtime.FindNodesAsync(top.Id, TreeKinds.Visual, name: "LifecycleEditor")).Value!.Matches).Target!;
                    var request = Path.Combine(_root, "request.json");
                    const string desired = "private-lifecycle-edit — 日本語 😀";
                    await File.WriteAllTextAsync(request, JsonSerializer.Serialize(new
                    {
                        request = new RuntimeDesiredStateRequest(target, "text", JsonSerializer.SerializeToElement(desired), "controlled-lifecycle"),
                        manifestDirectory = Path.GetDirectoryName(runtime.SessionManifestPath)!
                    }));
                    using var process = Start(request, _root, "ensure_state", false, stage);
                    var stdout = process.StandardOutput.ReadToEndAsync();
                    var stderr = process.StandardError.ReadToEndAsync();
                    Exception? processFailure = null;
                    try
                    {
                        process.StandardInput.Close();
                        var elapsed = Stopwatch.StartNew();
                        JsonElement[] trace = [];
                        while (elapsed.Elapsed < TimeSpan.FromSeconds(30))
                        {
                            if (File.Exists(process.StartInfo.Environment["AVASCOPE_PROBE_LIFECYCLE_FILE"])) trace = ReadLifecycle(process);
                            if (trace.Any(row => row.GetProperty("stage").GetString() == "controlled_delay_started")) break;
                            await Task.Delay(20);
                        }
                        var held = Assert.Single(trace, row => row.GetProperty("stage").GetString() == "controlled_delay_started");
                        Assert.Equal(disposition, held.GetProperty("disposition").GetString());
                        if (stage == "request_started")
                        {
                            var editWait = Stopwatch.StartNew();
                            while (edits == 0 && editWait.Elapsed < TimeSpan.FromSeconds(3)) await Task.Delay(20);
                        }
                        await Assert.ThrowsAsync<TimeoutException>(() => process.WaitForExitAsync().WaitAsync(TimeSpan.FromMilliseconds(100)));
                        // Independent application oracle: a missing client response does not imply no edit.
                        Assert.Equal(expectedEdits, edits);
                        Assert.Equal(expectedEdits == 0 ? "seed" : desired, editor.Text);
                        output.WriteLine(JsonSerializer.Serialize(new { stage, expectedEdits, observedEdits = edits, expectedTextObserved = editor.Text == (expectedEdits == 0 ? "seed" : desired) }));
                        await File.WriteAllTextAsync(process.StartInfo.Environment["AVASCOPE_PROBE_HOLD_RELEASE"]!, "release");
                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                        Assert.True(process.ExitCode == 0, await stderr);
                        using var result = JsonDocument.Parse(await stdout);
                        Assert.True(result.RootElement.GetProperty("success").GetBoolean());
                        Assert.Equal(desired, editor.Text); Assert.Equal(1, edits);
                        var complete = ReadLifecycle(process);
                        Assert.Single(complete, row => row.GetProperty("stage").GetString() == "request_started");
                        Assert.Single(complete, row => row.GetProperty("stage").GetString() == "closed");
                        Assert.DoesNotContain(desired, JsonSerializer.Serialize(complete));
                    }
                    catch (Exception exception) { processFailure = exception; throw; }
                    finally { await FinishProcessAsync(process, processFailure); }
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { await Task.Run(() => BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session)); }
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
        Exception? processFailure = null;
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
        catch (Exception exception) { processFailure = exception; throw; }
        finally { await FinishProcessAsync(process, processFailure); }
    }

    private static Process Start(string requestPath, string manifestDirectory, string mode, bool fullResult, string? holdStage = null, string? lifecyclePath = null)
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
        start.Environment["AVASCOPE_PROBE_LIFECYCLE_FILE"] = lifecyclePath ?? Path.Combine(Path.GetDirectoryName(requestPath)!, Guid.NewGuid().ToString("N") + ".lifecycle.jsonl");
        start.Environment.Remove("AVASCOPE_PROBE_HOLD_STAGE");
        start.Environment.Remove("AVASCOPE_PROBE_HOLD_RELEASE");
        if (holdStage is not null)
        {
            start.Environment["AVASCOPE_PROBE_HOLD_STAGE"] = holdStage;
            start.Environment["AVASCOPE_PROBE_HOLD_RELEASE"] = Path.Combine(Path.GetDirectoryName(requestPath)!, "release");
        }
        return Process.Start(start)!;
    }

    private static JsonElement[] ReadLifecycle(Process process)
    {
        var path = process.StartInfo.Environment["AVASCOPE_PROBE_LIFECYCLE_FILE"]!;
        Assert.True(File.Exists(path), "The owned probe did not retain lifecycle evidence.");
        Assert.InRange(new FileInfo(path).Length, 0, 256 * 1024);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        // A live reader can observe a final partial write. Parse only complete records.
        return text[..(text.LastIndexOf('\n') + 1)].Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonSerializer.Deserialize<JsonElement>(line)).ToArray();
    }

    private async Task FinishProcessAsync(Process process, Exception? primaryFailure)
    {
        // Snapshot before cleanup: a terminated child must not masquerade as a natural exit.
        output.WriteLine(JsonSerializer.Serialize(new { mode = "probe_parent", stage = "before_cleanup", processId = process.Id, exited = process.HasExited }));
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception exception)
        {
            if (primaryFailure is not null) throw new AggregateException(primaryFailure, exception);
            throw;
        }
        finally
        {
            output.WriteLine(JsonSerializer.Serialize(new { mode = "probe_parent", stage = "after_cleanup", processId = process.Id, exited = process.HasExited, exitCode = process.HasExited ? (int?)process.ExitCode : null }));
            var path = process.StartInfo.Environment["AVASCOPE_PROBE_LIFECYCLE_FILE"]!;
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length <= 256 * 1024)
                    foreach (var row in ReadLifecycle(process)) output.WriteLine(row.GetRawText());
                else output.WriteLine("Probe lifecycle file unavailable; no dispatch or cleanup outcome is inferred from missing evidence.");
            }
            catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
            { output.WriteLine("Probe lifecycle file unreadable; original failure and process outcome remain separate."); }
        }
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
