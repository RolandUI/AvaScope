using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Bridge;

public sealed class WindowsAccessibilityFactAttribute : FactAttribute
{
    public WindowsAccessibilityFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Requires the real Windows COM apartment and HWND ownership APIs.";
    }
}

[Collection(BridgeCollectionDefinition.Name)]
public sealed class NativeAccessibilityAuditTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [WindowsAccessibilityFact]
    public async Task WindowsNativeInitializationFailureRetainsSafeCauseAndStage()
    {
        if (!OperatingSystem.IsWindows()) return;
        // A real STA apartment deterministically refuses the adapter's MTA initialization.
        // This diagnoses a controlled failure; it does not reproduce the original CI cause.
        var completion = new TaskCompletionSource<(NativeAccessibilitySnapshot Snapshot, bool Destroyed)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new Thread(() =>
        {
            nint window = 0; var initialized = false; var destroyed = false;
            NativeAccessibilitySnapshot? snapshot = null; Exception? failure = null;
            try
            {
                var hr = CoInitializeEx(0, 2);
                Assert.True(hr >= 0, $"STA setup failed: 0x{hr:X8}"); initialized = true;
                window = CreateWindowExW(0, "STATIC", "private-document-sentinel", 0, 0, 0, 100, 100, 0, 0, 0, 0);
                Assert.NotEqual(nint.Zero, window);
                GetWindowThreadProcessId(window, out var pid);
                Assert.Equal((uint)Environment.ProcessId, pid);
                var reader = typeof(AvaScopeBridge).Assembly.GetType("AvaScope.Bridge.NativeAccessibilityReader", throwOnError: true)!;
                var read = reader.GetMethod("Read", BindingFlags.NonPublic | BindingFlags.Static)!;
                var request = new NativeAccessibilityAuditRequest(new(new("native-diagnostic"), "owned-window", topLevelGeneration: "current"), timeoutMs: 5000);
                snapshot = (NativeAccessibilitySnapshot)read.Invoke(null, ["win32", window, null, request, CancellationToken.None])!;
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                if (window != 0) destroyed = DestroyWindow(window);
                if (initialized) CoUninitialize();
            }
            if (failure is not null) completion.TrySetException(failure);
            else completion.TrySetResult((snapshot!, destroyed));
        }) { IsBackground = true };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
        var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(worker.Join(TimeSpan.FromSeconds(5)), "The owned COM diagnostic thread did not finish.");
        Assert.True(result.Destroyed, "The owned hidden HWND was not destroyed.");
        var json = JsonSerializer.Serialize(result.Snapshot);
        output.WriteLine(json);
        Assert.Equal("unavailable", result.Snapshot.Status);
        Assert.Empty(result.Snapshot.Nodes);
        Assert.DoesNotContain("private-document-sentinel", json, StringComparison.Ordinal);
        var diagnostic = Assert.Single(result.Snapshot.Diagnostics);
        Assert.NotNull(diagnostic.Details);
        Assert.Equal("initialize_com", diagnostic.Details["stage"]);
        Assert.Equal("0x80010106", diagnostic.Details["hresult"]);
        Assert.Equal("native_return", diagnostic.Details["hresultSource"]);
        Assert.Equal("native_error", diagnostic.Details["failureKind"]);
        Assert.Equal("false", diagnostic.Details["cancellationRequested"]);
        Assert.Equal("5000", diagnostic.Details["queryTimeoutMs"]);
        Assert.Equal("2000", diagnostic.Details["connectionTimeoutMs"]);
        Assert.Equal("250", diagnostic.Details["transactionTimeoutMs"]);
        Assert.Equal("0", diagnostic.Details["observedNodeCount"]);
        Assert.Equal("0", diagnostic.Details["pendingNodeCount"]);
        var elapsed = double.Parse(diagnostic.Details["elapsedMs"], CultureInfo.InvariantCulture);
        Assert.InRange(double.Parse(diagnostic.Details["stageElapsedMs"], CultureInfo.InvariantCulture), 0, elapsed);
        Assert.NotEmpty(diagnostic.Details["nextAction"]);
    }

    [WindowsAccessibilityFact]
    public void WindowsPreCanceledNativeQueryKeepsCancellationDistinctFromComFailure()
    {
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var reader = typeof(AvaScopeBridge).Assembly.GetType("AvaScope.Bridge.NativeAccessibilityReader", throwOnError: true)!;
        var read = reader.GetMethod("Read", BindingFlags.NonPublic | BindingFlags.Static)!;
        var request = new NativeAccessibilityAuditRequest(new(new("native-canceled"), "owned-window", topLevelGeneration: "current"));
        var snapshot = (NativeAccessibilitySnapshot)read.Invoke(null, ["win32", nint.Zero, null, request, canceled.Token])!;
        Assert.Equal("unavailable", snapshot.Status); Assert.Empty(snapshot.Nodes);
        var details = Assert.Single(snapshot.Diagnostics).Details!;
        Assert.Equal("validate_owner", details["stage"]);
        Assert.Equal("canceled", details["failureKind"]);
        Assert.Equal("managed_exception", details["hresultSource"]);
        Assert.Equal("true", details["cancellationRequested"]);
        Assert.Equal("0", details["observedNodeCount"]);
    }

    [Theory]
    [InlineData("native_timeout", false)]
    [InlineData("native_access_denied", false)]
    [InlineData("native_unavailable", false)]
    [InlineData("native_unavailable", true)]
    [InlineData("canceled", true)]
    [InlineData("managed_timeout", false)]
    [InlineData("managed_failure", false)]
    public void WindowsNativeFailureDiagnosticsPreserveRawCodeAndExcludeExceptionContent(string failure, bool cancellationRequested)
    {
        const string secret = "private-document-sentinel: C:\\private\\customer.txt";
        int? returnedHResult = failure switch
        {
            "native_timeout" => unchecked((int)0x80131505),
            "native_access_denied" => unchecked((int)0x80070005),
            "native_unavailable" => unchecked((int)0x80040201),
            _ => null
        };
        Exception exception = failure switch
        {
            "native_timeout" => new COMException(secret, unchecked((int)0x80131505)),
            "native_access_denied" => new UnauthorizedAccessException(secret),
            // A different exception code simulates unrelated thread-local IErrorInfo.
            "native_unavailable" => new COMException(secret, unchecked((int)0x80004005)),
            "canceled" => new OperationCanceledException(secret),
            "managed_timeout" => new TimeoutException(secret),
            _ => new InvalidOperationException(secret, new Exception(secret))
        };
        exception.Data[secret] = secret;
        if (failure is "native_timeout" or "native_access_denied")
            Assert.Equal(exception.GetType(), Marshal.GetExceptionForHR(returnedHResult!.Value, new nint(-1))!.GetType());
        var reader = typeof(AvaScopeBridge).Assembly.GetType("AvaScope.Bridge.NativeAccessibilityReader", throwOnError: true)!;
        var describe = reader.GetMethod("WindowsFailure", BindingFlags.NonPublic | BindingFlags.Static)!;
        var request = new NativeAccessibilityAuditRequest(new(new("native-diagnostic"), "owned-window", topLevelGeneration: "current"), timeoutMs: 5000);
        var culture = CultureInfo.CurrentCulture;
        ProtocolError diagnostic;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            diagnostic = (ProtocolError)describe.Invoke(null, [exception, "read_name", 1250.5, 250.25, returnedHResult, request, cancellationRequested, 3, 2])!;
        }
        finally { CultureInfo.CurrentCulture = culture; }
        var json = JsonSerializer.Serialize(diagnostic);
        Assert.DoesNotContain("private", json, StringComparison.Ordinal);
        Assert.DoesNotContain("customer", json, StringComparison.Ordinal);
        Assert.InRange(json.Length, 1, 2048);
        Assert.Equal("native_accessibility_partial", diagnostic.Code);
        var details = diagnostic.Details!;
        Assert.Equal("read_name", details["stage"]);
        Assert.Equal(returnedHResult.HasValue ? "native_error" : failure == "canceled" ? "canceled" : "managed_error", details["failureKind"]);
        Assert.Equal(returnedHResult.HasValue ? "native_return" : "managed_exception", details["hresultSource"]);
        Assert.Equal($"0x{returnedHResult ?? exception.HResult:X8}", details["hresult"]);
        Assert.Equal("1250.5", details["elapsedMs"]);
        Assert.Equal("250.25", details["stageElapsedMs"]);
        Assert.Equal(cancellationRequested ? "true" : "false", details["cancellationRequested"]);
        Assert.Equal("3", details["observedNodeCount"]);
        Assert.Equal("2", details["pendingNodeCount"]);
        Assert.Equal("5000", details["queryTimeoutMs"]);
        Assert.Equal("2000", details["connectionTimeoutMs"]);
        Assert.Equal("250", details["transactionTimeoutMs"]);
        Assert.NotEmpty(details["nextAction"]);
    }

    [Theory]
    [InlineData(250, "250")]
    [InlineData(750, "750")]
    [InlineData(2000, "2000")]
    [InlineData(5000, "2000")]
    public void WindowsFailureReportsSeparateBoundedConnectionAndTransactionBudgets(int queryTimeoutMs, string connectionTimeoutMs)
    {
        var reader = typeof(AvaScopeBridge).Assembly.GetType("AvaScope.Bridge.NativeAccessibilityReader", throwOnError: true)!;
        var describe = reader.GetMethod("WindowsFailure", BindingFlags.NonPublic | BindingFlags.Static)!;
        var request = new NativeAccessibilityAuditRequest(new(new("native-budget"), "owned-window", topLevelGeneration: "current"), timeoutMs: queryTimeoutMs);
        var diagnostic = (ProtocolError)describe.Invoke(null, [new COMException(), "element_from_handle", 300.0, 290.0,
            unchecked((int)0x80131505), request, false, 0, 0])!;
        Assert.Equal(queryTimeoutMs.ToString(CultureInfo.InvariantCulture), diagnostic.Details!["queryTimeoutMs"]);
        Assert.Equal(connectionTimeoutMs, diagnostic.Details["connectionTimeoutMs"]);
        Assert.Equal("250", diagnostic.Details["transactionTimeoutMs"]);
        Assert.Equal("native_error", diagnostic.Details["failureKind"]);
    }

    [Fact]
    public async Task UnsupportedNativeBackendRetainsBridgeEvidenceAndEnforcesPrivacyOwnershipAndGenerations()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate(); var runtime = AvaScopeBridge.Activate();
                var button = new Button { Name = "AuditButton", Content = "private-label" }; var window = new Window { Width = 250, Height = 180, Content = button };
                try
                {
                    window.Show(); using var registered = runtime.RegisterTopLevel(window); Dispatcher.UIThread.RunJobs();
                    var id = Assert.Single(await runtime.ListTopLevelsAsync()).Id; var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
                    var target = (await client.WindowAsync(new(new(runtime.SessionId, id)))).Value!.After!.Target;
                    var root = Path.Combine(Path.GetTempPath(), "avascope-native-a11y");
                    var policy = new RuntimeEvidencePolicy(root, redactedText: ["private-label"]);
                    var result = await client.AuditNativeAccessibilityAsync(new(target, policy: policy));
                    Assert.True(result.Success, result.Error?.Message); Assert.Equal("unsupported", result.Value!.Status);
                    Assert.NotEmpty(result.Value.BridgeNodes); Assert.Empty(result.Value.Native.Nodes);
                    Assert.All(result.Value.Comparisons, item => Assert.Equal("unavailable", item.Mapping));
                    Assert.False(OperationResultMapper.IsSuccessful(result)); Assert.DoesNotContain("private-label", JsonSerializer.Serialize(result));
                    Assert.Equal("inspection_policy_denied", (await runtime.AuditNativeAccessibilityAsync(new(target, policy: new(root, authorizedProcessIds: [int.MaxValue])))).Error!.Code);
                    Assert.Equal("native_accessibility_policy_unavailable", (await client.AuditNativeAccessibilityAsync(new(target, policy: new(root, excludedControlAutomationIds: ["private"])))).Error!.Code);
                    var stale = new RuntimeTargetContext(runtime.SessionId, id, topLevelGeneration: "stale");
                    Assert.Equal("native_accessibility_stale", (await client.AuditNativeAccessibilityAsync(new(stale))).Error!.Code);
                    var foreign = new RuntimeTargetContext(new("another-session"), id, topLevelGeneration: target.TopLevelGeneration);
                    Assert.Equal("inspection_session_mismatch", (await runtime.AuditNativeAccessibilityAsync(new(foreign))).Error!.Code);
                    var node = Assert.Single((await client.FindNodesAsync(runtime.SessionId, id, TreeKinds.Visual, name: "AuditButton")).Value!.Matches).Node.Target!;
                    window.Content = null;
                    Assert.Equal("native_accessibility_stale", (await client.AuditNativeAccessibilityAsync(new(target, expectations: [new(node)]))).Error!.Code);
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session); }
    }

    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(uint extendedStyle, string className, string title, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(nint window);
}
