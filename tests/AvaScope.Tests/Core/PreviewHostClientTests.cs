using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class PreviewHostClientTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "AvaScope.Tests",
        $"preview-client-{Guid.NewGuid():N}");

    public void Dispose()
    {
        DeleteDirectoryWithRetry(_testRoot);
    }

    private static void DeleteDirectoryWithRetry(string path)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }

    [Fact]
    public async Task RenderAsyncLaunchesPreviewHostChildProcess()
    {
        Directory.CreateDirectory(_testRoot);

        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        var viewPath = Path.Combine(_testRoot, "SmokeView.axaml");
        var outputPath = Path.Combine(_testRoot, "preview.png");

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="Core preview client smoke" />
              </Border>
            </UserControl>
            """);

        var client = new PreviewHostClient(hostAssembly);
        var result = await client.RenderAsync(new PreviewRequest(
            outputPath,
            width: 300,
            height: 180,
            dpi: 96,
            viewPath: viewPath,
            themeVariant: "light"));

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(Path.GetFullPath(outputPath), result.Value!.FilePath);
        Assert.Equal(300, result.Value.PixelWidth);
        Assert.Equal(180, result.Value.PixelHeight);
        Assert.True(File.Exists(result.Value.FilePath));
        Assert.True(new FileInfo(result.Value.FilePath).Length > 0);
    }

    [Fact]
    public async Task RenderAsyncReturnsStructuredErrorWhenPreviewHostIsMissing()
    {
        var client = new PreviewHostClient(Path.Combine(_testRoot, "missing-host.dll"));

        var result = await client.RenderAsync(new PreviewRequest(
            Path.Combine(_testRoot, "preview.png"),
            width: 100,
            height: 100,
            dpi: 96));

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.PreviewHostUnavailable, result.Error!.Code);
        Assert.Equal("host", result.Error.Details!["phase"]);
        Assert.Equal("host_assembly", result.Error.Details["requirement"]);
        Assert.Equal(Path.GetFullPath(Path.Combine(_testRoot, "missing-host.dll")), result.Error.Details["hostAssemblyPath"]);
        Assert.Contains("AvaScope.PreviewHost.dll", result.Error.Details["nextAction"], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RenderAsyncInterruptedRequestCleansChildAndRetainsSafeTimeoutEvidence(bool cancelRequest)
    {
        Directory.CreateDirectory(_testRoot);
        var markerPath = Path.Combine(_testRoot, "blocked.pid");
        var projectPath = Path.Combine(_testRoot, "Blocked.csproj");
        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        var client = new PreviewHostClient(
            Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll"),
            TimeSpan.FromSeconds(cancelRequest ? 30 : 10));
        using var cancellation = new CancellationTokenSource();
        try
        {
            var renderTask = client.RenderAsync(new PreviewRequest(
                Path.Combine(_testRoot, "blocked.png"),
                width: 80,
                height: 60,
                projectPath: projectPath,
                assemblyPath: typeof(PreviewHostClientTests).Assembly.Location,
                noBuild: true,
                designDataType: typeof(BlockingDesignData).FullName,
                stateVariant: markerPath), cancellation.Token);

            if (cancelRequest)
            {
                var deadline = Stopwatch.StartNew();
                while (!File.Exists(markerPath) && !renderTask.IsCompleted && deadline.Elapsed < TimeSpan.FromSeconds(10))
                {
                    await Task.Delay(20);
                }

                Assert.True(File.Exists(markerPath), "Owned child did not reach the controlled block.");
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => renderTask);
                Assert.False(IsProcessRunning(int.Parse(await File.ReadAllTextAsync(markerPath), CultureInfo.InvariantCulture)));
                return;
            }

            var result = await renderTask;

            Assert.True(File.Exists(markerPath), JsonSerializer.Serialize(result));
            var processId = int.Parse(await File.ReadAllTextAsync(markerPath), CultureInfo.InvariantCulture);
            Assert.False(result.Success);
            Assert.Equal(CoreErrorCodes.PreviewHostUnavailable, result.Error!.Code);
            Assert.Equal("host_timeout", result.Error.Details!["requirement"]);
            Assert.Equal("design_data", result.Error.Details["lastHostPhase"]);
            Assert.Equal(processId.ToString(CultureInfo.InvariantCulture), result.Error.Details["hostProcessId"]);
            Assert.Equal("true", result.Error.Details["hostExited"]);
            Assert.Equal("completed", result.Error.Details["stdoutCapture"]);
            Assert.Equal("completed", result.Error.Details["stderrCapture"]);
            Assert.True(long.Parse(result.Error.Details["stdoutCharacters"], CultureInfo.InvariantCulture) > 0);
            Assert.True(long.Parse(result.Error.Details["stderrCharacters"], CultureInfo.InvariantCulture) > 0);
            Assert.DoesNotContain("private-preview-output", JsonSerializer.Serialize(result));
            Assert.False(File.Exists(Path.Combine(_testRoot, "blocked.png")));
            Assert.False(IsProcessRunning(processId));
        }
        finally
        {
            await File.WriteAllTextAsync(markerPath + ".release", "release owned fixture");
            if (File.Exists(markerPath))
            {
                var processId = int.Parse(await File.ReadAllTextAsync(markerPath), CultureInfo.InvariantCulture);
                if (IsProcessRunning(processId))
                {
                    using var process = Process.GetProcessById(processId);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                }
            }
        }
    }

    [Fact]
    public async Task RenderAsyncBuildTimeoutRetainsBuildMarkerAndStopsOwnedBuildProcess()
    {
        Directory.CreateDirectory(_testRoot);
        var projectPath = Path.Combine(_testRoot, "BlockedBuild.csproj");
        var markerPath = Path.Combine(_testRoot, "build.pid");
        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <UsingTask TaskName="ControlledBlock" TaskFactory="RoslynCodeTaskFactory"
                         AssemblyFile="$(MSBuildToolsPath)/Microsoft.Build.Tasks.Core.dll">
                <ParameterGroup><Marker ParameterType="System.String" Required="true" /></ParameterGroup>
                <Task><Code Type="Fragment" Language="cs"><![CDATA[
                  using (var ownedProcess = System.Diagnostics.Process.GetCurrentProcess())
                      System.IO.File.WriteAllText(Marker + ".tmp", ownedProcess.Id.ToString());
                  System.Console.WriteLine("private-build-output");
                  System.Console.Out.Flush();
                  System.IO.File.Move(Marker + ".tmp", Marker);
                  var elapsed = System.Diagnostics.Stopwatch.StartNew();
                  while (!System.IO.File.Exists(Marker + ".release") && elapsed.Elapsed.TotalMinutes < 2)
                      System.Threading.Thread.Sleep(10);
                ]]></Code></Task>
              </UsingTask>
              <Target Name="BlockBuild" BeforeTargets="Restore;Build">
                <ControlledBlock Marker="$(MSBuildProjectDirectory)/build.pid" />
              </Target>
            </Project>
            """);
        var clock = new ControlledDeadline();
        Task<CoreResult<PreviewResponse>>? renderTask = null;
        try
        {
            renderTask = new PreviewHostClient(
                Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll"),
                TimeSpan.FromSeconds(60), clock).RenderAsync(new PreviewRequest(
                    Path.Combine(_testRoot, "build.png"), width: 80, height: 60, projectPath: projectPath));

            var startupDeadline = Stopwatch.StartNew();
            while (!File.Exists(markerPath) && !renderTask.IsCompleted && startupDeadline.Elapsed < TimeSpan.FromSeconds(60))
            {
                await Task.Delay(20);
            }

            clock.Expire();
            var result = await renderTask.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(File.Exists(markerPath), JsonSerializer.Serialize(result));
            Assert.False(result.Success);
            Assert.Equal("host_timeout", result.Error!.Details!["requirement"]);
            Assert.Equal("project_build", result.Error.Details["lastHostPhase"]);
            Assert.Equal("true", result.Error.Details["hostExited"]);
            Assert.Equal("true", result.Error.Details["buildLogAvailable"]);
            var buildProcessId = int.Parse(await File.ReadAllTextAsync(markerPath), CultureInfo.InvariantCulture);
            Assert.Equal(buildProcessId.ToString(CultureInfo.InvariantCulture), result.Error.Details["buildProcessId"]);
            var log = await File.ReadAllTextAsync(result.Error.Details["buildLogPath"]);
            Assert.Contains("In-progress output is withheld", log, StringComparison.Ordinal);
            Assert.DoesNotContain("private-build-output", log);
            Assert.DoesNotContain("private-build-output", JsonSerializer.Serialize(result));
            var exitDeadline = Stopwatch.StartNew();
            while (IsProcessRunning(buildProcessId) && exitDeadline.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(20);
            }

            Assert.False(IsProcessRunning(buildProcessId));
        }
        finally
        {
            clock.Expire();
            await File.WriteAllTextAsync(markerPath + ".release", "release owned build fixture");
            if (renderTask is not null)
            {
                await renderTask.WaitAsync(TimeSpan.FromSeconds(10));
            }
        }
    }

    private sealed class ControlledDeadline : TimeProvider
    {
        private DeadlineTimer? _timer;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new DeadlineTimer(callback, state);
            Volatile.Write(ref _timer, timer);
            return timer;
        }

        public void Expire() => Volatile.Read(ref _timer)?.Expire();

        private sealed class DeadlineTimer(TimerCallback callback, object? state) : ITimer
        {
            private int _completed;
            public bool Change(TimeSpan dueTime, TimeSpan period) => false;
            public void Dispose() => Interlocked.Exchange(ref _completed, 1);
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
            public void Expire()
            {
                if (Interlocked.Exchange(ref _completed, 1) == 0) callback(state);
            }
        }
    }

    [Theory]
    [InlineData("{", "unavailable")]
    [InlineData("null", "unmatched")]
    [InlineData("{\"processId\":\"2\",\"phase\":\"content\"}", "unmatched")]
    [InlineData("{\"processId\":\"1\",\"phase\":\"private-output\",\"buildLogPath\":null}", "observed")]
    [InlineData("oversized", "oversized")]
    public void HostProgressRejectsInvalidForeignAndUnboundedEvidence(string json, string status)
    {
        Directory.CreateDirectory(_testRoot);
        var requestPath = Path.Combine(_testRoot, "request.json");
        File.WriteAllText(requestPath + ".progress.json", json == "oversized" ? new string('x', 9000) : json);
        var details = new Dictionary<string, string>();

        PreviewHostClient.ReadHostProgress(requestPath, 1, details);

        Assert.Equal(status, details["hostProgress"]);
        Assert.Equal("unavailable", details["lastHostPhase"]);
        Assert.DoesNotContain("private-output", JsonSerializer.Serialize(details));
    }

    [Fact]
    public void HostProgressCopiesOnlySafeObservedBuildMetrics()
    {
        Directory.CreateDirectory(_testRoot);
        var requestPath = Path.Combine(_testRoot, "request.json");
        File.WriteAllText(requestPath + ".progress.json", """
            {"processId":"1","phase":"project_build","elapsedMs":"12","buildProcessId":"2",
             "buildStdoutCharacters":"123","buildStderrCharacters":"-1","buildLastOutputElapsedMs":"15",
             "buildOutputMarker":"restore_started","stdout":"private-output","environment":"private-output"}
            """);
        var details = new Dictionary<string, string>();

        PreviewHostClient.ReadHostProgress(requestPath, 1, details);

        Assert.Equal("project_build", details["lastHostPhase"]);
        Assert.Equal("123", details["buildStdoutCharacters"]);
        Assert.Equal("15", details["buildLastOutputElapsedMs"]);
        Assert.Equal("restore_started", details["buildOutputMarker"]);
        Assert.Equal("recognized_dotnet_output", details["buildOutputMarkerProvenance"]);
        Assert.False(details.ContainsKey("buildStderrCharacters"));
        Assert.DoesNotContain("private-output", JsonSerializer.Serialize(details));
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedCleanupBoundsAnIncompleteStreamAfterActualChildExit(bool blockStderr)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        Assert.True(process.Start());
        var actualStdout = process.StandardOutput.ReadToEndAsync();
        var actualStderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await Task.WhenAll(actualStdout, actualStderr).WaitAsync(TimeSpan.FromSeconds(5));
        using var captureCancellation = new CancellationTokenSource();
        var pendingCapture = WaitForCaptureAsync(captureCancellation.Token);
        var details = new Dictionary<string, string>();
        var elapsed = Stopwatch.StartNew();

        await PreviewHostClient.StopPreviewHostAsync(
            process,
            blockStderr ? actualStdout : pendingCapture,
            blockStderr ? pendingCapture : actualStderr,
            captureCancellation,
            details);

        Assert.Equal("already_exited", details["hostTermination"]);
        Assert.Equal("true", details["hostExited"]);
        Assert.Equal("incomplete", details[blockStderr ? "stderrCapture" : "stdoutCapture"]);
        Assert.Equal("completed", details[blockStderr ? "stdoutCapture" : "stderrCapture"]);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(10));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pendingCapture);

        static async Task<string> WaitForCaptureAsync(CancellationToken token)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return string.Empty;
        }
    }

    public sealed class BlockingDesignData
    {
        public static BlockingDesignData ForState(string markerPath)
        {
            File.WriteAllText(markerPath + ".tmp", Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            Console.Out.Write("private-preview-output stdout");
            Console.Out.Flush();
            Console.Error.Write("private-preview-output stderr");
            Console.Error.Flush();
            File.Move(markerPath + ".tmp", markerPath);
            var deadline = Stopwatch.StartNew();
            while (!File.Exists(markerPath + ".release") && deadline.Elapsed < TimeSpan.FromMinutes(2))
            {
                Thread.Sleep(10);
            }

            return new BlockingDesignData();
        }
    }

    [Fact]
    public async Task RenderAnimationAsyncCreatesOffsetFramesStripAndMotionSummary()
    {
        Directory.CreateDirectory(_testRoot);

        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        var viewPath = Path.Combine(_testRoot, "AnimationView.axaml");
        var outputPath = Path.Combine(_testRoot, "animation.png");
        var stripPath = Path.Combine(_testRoot, "animation-strip.png");
        var viewerPath = Path.Combine(_testRoot, "animation.html");

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="Animation frame sample" />
              </Border>
            </UserControl>
            """);

        var client = new PreviewHostClient(hostAssembly);
        var result = await client.RenderAnimationAsync(new PreviewAnimationRequest(
            outputPath,
            [0, 33, 33],
            width: 240,
            height: 120,
            dpi: 96,
            viewPath: viewPath,
            themeVariant: "light",
            frameStripPath: stripPath,
            viewerPath: viewerPath));

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(3, result.Value!.Frames.Count);
        Assert.All(result.Value.Frames, frame => Assert.True(frame.Render.Success, frame.Render.Error?.Message));
        Assert.Equal(0, result.Value.Frames[0].Render.Value!.AnimationTimeOffsetMs);
        Assert.Equal(33, result.Value.Frames[1].Render.Value!.AnimationTimeOffsetMs);
        Assert.Equal(33, result.Value.Frames[2].Render.Value!.AnimationTimeOffsetMs);
        Assert.Contains(
            result.Value.Frames[2].Render.Value!.Diagnostics,
            static diagnostic => diagnostic.Code == "animation_frame_reused");
        Assert.All(result.Value.Frames, frame => Assert.True(File.Exists(frame.OutputPath)));
        Assert.Equal(Path.GetFullPath(stripPath), result.Value.FrameStripPath);
        Assert.True(File.Exists(stripPath));
        Assert.NotNull(result.Value.Viewer);
        Assert.Equal(Path.GetFullPath(viewerPath), result.Value.Viewer!.ViewerPath);
        Assert.Equal(new Uri(viewerPath).AbsoluteUri, result.Value.Viewer.PreviewUrl);
        Assert.True(File.Exists(viewerPath));
        var viewerHtml = await File.ReadAllTextAsync(viewerPath);
        Assert.Contains("data:image/png;base64,", viewerHtml);
        Assert.Contains("animation-json", viewerHtml);
        Assert.Equal("static", result.Value.Motion.Status);
        Assert.Contains(result.Value.Diagnostics, static diagnostic => diagnostic.Code == "animation_static_frames");
        Assert.True(File.Exists(result.Value.DiagnosticsArtifactPath));
        Assert.All(
            result.Value.Diagnostics,
            static diagnostic => Assert.Equal(64, diagnostic.Fingerprint!.Length));
        Assert.Equal("unavailable", result.Value.DiagnosticSummary.ComparisonProvenance);
    }

    [Fact]
    public async Task RenderAsyncKeepsRenderResultWhenDiagnosticsBaselineIsMissing()
    {
        Directory.CreateDirectory(_testRoot);
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        var viewPath = Path.Combine(_testRoot, "MissingBaselineView.axaml");
        var outputPath = Path.Combine(_testRoot, "missing-baseline.png");
        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <TextBlock Text="Missing diagnostics baseline" />
            </UserControl>
            """);

        var result = await new PreviewHostClient(hostAssembly).RenderAsync(new PreviewRequest(
            outputPath,
            width: 240,
            height: 120,
            viewPath: viewPath,
            diagnosticOptions: new PreviewDiagnosticOptions(
                baselinePath: Path.Combine(_testRoot, "missing-diagnostics.json"))));

        Assert.True(result.Success, result.Error?.Message);
        Assert.True(File.Exists(result.Value!.FilePath));
        Assert.Equal("invalid", result.Value.DiagnosticSummary.ComparisonProvenance);
        Assert.Equal(
            CoreErrorCodes.PreviewDiagnosticsBaselineInvalid,
            result.Value.DiagnosticSummary.ComparisonError!.Code);
    }

    [Fact]
    public async Task SeverityFilteringIsConsistentAcrossOneShotBatchAnimationAndSession()
    {
        Directory.CreateDirectory(_testRoot);
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        var viewPath = Path.Combine(_testRoot, "FilterParityView.axaml");
        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <TextBlock Text="Diagnostic filter parity" />
            </UserControl>
            """);
        var options = new PreviewDiagnosticOptions(minimumSeverity: PreviewMinimumSeverities.Error);
        var client = new PreviewHostClient(hostAssembly);

        var oneShot = await client.RenderAsync(new PreviewRequest(
            Path.Combine(_testRoot, "one-shot.png"),
            width: 200,
            height: 100,
            viewPath: viewPath,
            diagnosticOptions: options));
        var batch = await client.RenderBatchAsync(
            new PreviewRequest(
                Path.Combine(_testRoot, "batch.png"),
                viewPath: viewPath,
                diagnosticOptions: options),
            [new PreviewViewport(200, 100)]);
        var animation = await client.RenderAnimationAsync(new PreviewAnimationRequest(
            Path.Combine(_testRoot, "animation-filter.png"),
            [0],
            width: 200,
            height: 100,
            viewPath: viewPath,
            diagnosticOptions: options));
        var sessions = new PreviewSessionRegistry(new SessionRegistry(), client);
        var session = await sessions.CreateAsync(new PreviewRequest(
            Path.Combine(_testRoot, "session.png"),
            width: 200,
            height: 100,
            viewPath: viewPath,
            diagnosticOptions: options));

        Assert.True(oneShot.Success, oneShot.Error?.Message);
        Assert.True(batch.Success, batch.Error?.Message);
        Assert.True(animation.Success, animation.Error?.Message);
        Assert.True(session.Success, session.Error?.Message);
        var responses = new[]
        {
            oneShot.Value!,
            Assert.Single(batch.Value!.Entries).Render.Value!,
            Assert.Single(animation.Value!.Frames).Render.Value!,
            session.Value!.LastRender.Value!
        };
        Assert.All(responses, response =>
        {
            Assert.All(
                response.Diagnostics,
                static diagnostic => Assert.Equal(
                    PreviewDiagnosticSeverities.Error,
                    diagnostic.Severity));
            Assert.True(File.Exists(response.DiagnosticsArtifactPath));
        });
        Assert.Empty(animation.Value.Diagnostics);
        Assert.Equal(0, animation.Value.DiagnosticSummary.TotalCount);
        Assert.True(File.Exists(animation.Value.DiagnosticsArtifactPath));
    }

    [Fact]
    public void GetDiagnosticsReportsAvailablePreviewHost()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        var client = new PreviewHostClient(hostAssembly);

        var diagnostics = client.GetDiagnostics();

        Assert.Equal(DiagnosticStatuses.Available, diagnostics.Status);
        Assert.Equal(Path.GetFullPath(hostAssembly), diagnostics.HostAssemblyPath);
        Assert.Equal(DiagnosticProcessModes.IsolatedChildProcess, diagnostics.ProcessMode);
        Assert.Equal("avascope", diagnostics.Service!.ServiceName);
        Assert.Null(diagnostics.Error);
    }

    [Fact]
    public void GetDiagnosticsReportsMissingPreviewHostAsUnavailable()
    {
        var hostAssembly = Path.Combine(_testRoot, "missing-host.dll");
        var client = new PreviewHostClient(hostAssembly);

        var diagnostics = client.GetDiagnostics();

        Assert.Equal(DiagnosticStatuses.Unavailable, diagnostics.Status);
        Assert.Equal(Path.GetFullPath(hostAssembly), diagnostics.HostAssemblyPath);
        Assert.Equal(DiagnosticProcessModes.IsolatedChildProcess, diagnostics.ProcessMode);
        Assert.Null(diagnostics.Service);
        Assert.Equal(CoreErrorCodes.PreviewHostUnavailable, diagnostics.Error!.Code);
        Assert.Equal("host", diagnostics.Error.Details!["phase"]);
        Assert.Equal("host_assembly", diagnostics.Error.Details["requirement"]);
        Assert.Contains("co-located", diagnostics.Error.Details["nextAction"], StringComparison.Ordinal);
    }
}
