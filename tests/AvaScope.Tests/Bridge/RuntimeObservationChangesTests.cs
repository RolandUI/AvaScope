using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeObservationChangesTests
{
    [Fact]
    public async Task CursorsTrackOrderedStateLifecycleValidationAndBoundedPolling()
    {
        await WithApp(async (runtime, window, panel, client) =>
        {
            var status = new TextBlock { Name = "Status", Text = "Initial", Width = 120, Height = 25 };
            panel.Children.Add(status);
            var observation = new RuntimeObservationRequest(runtime.SessionId, maxDepth: 8, maxNodes: 128, maxInlineBytes: 131072);
            var observer = new RuntimeObserver();
            async Task<RuntimeObservationChangesResponse> Read(string? cursor = null, int waitMs = 0, int maximum = 128)
            {
                var result = await observer.ObserveChangesAsync(client,
                    new RuntimeObservationChangesRequest(observation, cursor, waitMs, pollIntervalMs: 25, maxEvents: maximum));
                Assert.True(result.Success, result.Error?.Message);
                Assert.False(result.Value!.RequiresArtifactRead);
                return result.Value;
            }
            var baseline = await Read();
            Assert.Equal("baseline", baseline.Status);
            var unchanged = await Read(baseline.Cursor);
            Assert.Equal("unchanged", unchanged.Status);
            Assert.Empty(unchanged.Events);
            Assert.Null(unchanged.Baseline);
            Assert.True(JsonSerializer.SerializeToUtf8Bytes(unchanged).Length < JsonSerializer.SerializeToUtf8Bytes(baseline).Length);
            status.Text = "Transient intermediate value";
            status.Text = "Initial";
            var coalesced = await Read(unchanged.Cursor);
            Assert.Equal("unchanged", coalesced.Status);
            Assert.Contains("intermediate_states_may_be_coalesced", coalesced.Coverage, StringComparison.Ordinal);
            status.Text = "Updated";
            status.IsEnabled = false;
            DataValidationErrors.SetErrors(status, ["Invalid local value"]);
            var changed = await Read(unchanged.Cursor, maximum: 1);
            Assert.Equal("changed", changed.Status);
            var events = changed.Events.ToList();
            while (changed.HasMore)
            {
                changed = await Read(changed.Cursor, maximum: 8);
                events.AddRange(changed.Events);
            }
            var node = Assert.Single(events, value => value.Node?.Name == "Status");
            Assert.Contains("text", node.ChangedFields!);
            Assert.Contains("state", node.ChangedFields!);
            Assert.Contains("validation", node.ChangedFields!);
            Assert.True(node.Node!.Validation!.HasErrors);
            Assert.Equal(events.Select(value => value.Sequence).Order(), events.Select(value => value.Sequence));
            Assert.Equal(events.Count, events.Select(value => value.Sequence).Distinct().Count());

            var added = new TextBlock { Name = "Added", Text = "New", Height = 20 };
            panel.Children.Add(added);
            var enter = await Read(changed.Cursor);
            Assert.Contains(enter.Events, item => item.Kind == "node_entered_sample" && item.Node?.Name == "Added");
            panel.Children.Remove(added);
            var leave = await Read(enter.Cursor);
            Assert.Contains(leave.Events, item => item.Kind == "node_left_sample");
            var child = new Window { Title = "Child", Width = 100, Height = 80 };
            child.Show();
            using var childRegistration = runtime.RegisterTopLevel(child);
            var open = await Read(leave.Cursor);
            Assert.Contains(open.Events, item => item.Kind == "window_entered_scope" && item.Window!.Window!.Title == "Child");
            child.Close();
            childRegistration.Dispose();
            var close = await Read(open.Cursor);
            Assert.Contains(close.Events, item => item.Kind == "window_left_scope");

            Dispatcher.UIThread.RunJobs();
            var current = await Read(close.Cursor);
            var pending = Read(current.Cursor, waitMs: 1000);
            DispatcherTimer.RunOnce(() => status.Text = "Asynchronous update", TimeSpan.FromMilliseconds(70));
            var asynchronous = await pending;
            Assert.Contains(asynchronous.Events, item => item.Node?.Text == "Asynchronous update");
            Assert.True(asynchronous.SamplesTaken >= 2);
            Assert.InRange(asynchronous.SamplesTaken, 2, 42);
            var latest = await Read(asynchronous.Cursor);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(60));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runtime.ObserveChangesAsync(
                new RuntimeObservationChangesRequest(observation, latest.Cursor, waitMs: 3000, pollIntervalMs: 25), cancellation.Token));
        });
    }

    [Fact]
    public async Task OverflowExpiryInvalidScopesAndRestartRequireAnExplicitBaseline()
    {
        await WithApp(async (runtime, window, panel, _) =>
        {
            for (var index = 0; index < 210; index++) panel.Children.Add(new TextBlock { Text = $"Before {index}", Width = 100, Height = 2 });
            var observation = new RuntimeObservationRequest(runtime.SessionId, maxDepth: 8, maxNodes: 256, maxInlineBytes: 131072);
            var initial = (await runtime.ObserveChangesAsync(new RuntimeObservationChangesRequest(observation))).Value!;
            foreach (var row in panel.Children.Cast<TextBlock>()) row.Text = "Rapid updated value";
            var overflow = (await runtime.ObserveChangesAsync(new RuntimeObservationChangesRequest(observation, initial.Cursor))).Value!;
            Assert.True(overflow.ResyncRequired);
            Assert.Equal("buffer_overflow", overflow.ResyncReason);
            Assert.NotNull(overflow.Baseline);
            Assert.Empty(overflow.Events);
            Assert.True(overflow.DroppedEvents > 0);
            Assert.InRange(overflow.RetainedEvents, 0, 128);
            Assert.InRange(overflow.RetainedBytes, 0, 384 * 1024);
            var resynced = (await runtime.ObserveChangesAsync(new RuntimeObservationChangesRequest(observation, overflow.Cursor))).Value!;
            Assert.Equal("unchanged", resynced.Status);

            var invalid = (await runtime.ObserveChangesAsync(new RuntimeObservationChangesRequest(observation, "invalid"))).Value!;
            Assert.Equal("invalid_cursor", invalid.ResyncReason);
            var future = (await runtime.ObserveChangesAsync(new RuntimeObservationChangesRequest(observation, invalid.Cursor.Split(':')[0] + ":999999"))).Value!;
            Assert.Equal("invalid_future_cursor", future.ResyncReason);
            var otherFilter = new RuntimeObservationRequest(runtime.SessionId, maxDepth: 2, maxNodes: 256);
            var mismatch = (await runtime.ObserveChangesAsync(new RuntimeObservationChangesRequest(otherFilter, future.Cursor))).Value!;
            Assert.Equal("cursor_scope_mismatch", mismatch.ResyncReason);
            var shortLived = (await runtime.ObserveChangesAsync(new RuntimeObservationChangesRequest(observation, cursorTtlMs: 100))).Value!;
            await Task.Delay(140);
            var expired = (await runtime.ObserveChangesAsync(new RuntimeObservationChangesRequest(observation, shortLived.Cursor, cursorTtlMs: 100))).Value!;
            Assert.Equal("expired_evicted_or_restarted", expired.ResyncReason);
            var evicted = invalid.Cursor;
            for (var index = 0; index < 9; index++)
                await runtime.ObserveChangesAsync(new RuntimeObservationChangesRequest(observation));
            Assert.Equal("expired_evicted_or_restarted", (await runtime.ObserveChangesAsync(new RuntimeObservationChangesRequest(observation, evicted))).Value!.ResyncReason);

            AvaScopeBridge.Deactivate();
            Assert.False((await runtime.ObserveChangesAsync(new RuntimeObservationChangesRequest(observation, overflow.Cursor))).Success);
            var restarted = AvaScopeBridge.Activate(new BridgeActivationOptions("Restarted changes"));
            using var registration = restarted.RegisterTopLevel(window);
            var restart = await restarted.ObserveChangesAsync(new RuntimeObservationChangesRequest(new RuntimeObservationRequest(restarted.SessionId), overflow.Cursor));
            Assert.True(restart.Value!.ResyncRequired);
            Assert.Equal("expired_evicted_or_restarted", restart.Value.ResyncReason);
            Assert.Contains("intermediate_states_may_be_coalesced", restart.Value.Coverage, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task RetainedChangesAreRedactedAndPolicyChangesCannotReadAnOldScope()
    {
        var output = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            await WithApp(async (runtime, _, panel, client) =>
            {
                const string secret = "retained-private-value";
                var label = new TextBlock { Name = "Public", Text = secret, Width = 150, Height = 20 };
                var hidden = new TextBlock { Text = "excluded-private-value" };
                AutomationProperties.SetAutomationId(hidden, "Excluded");
                panel.Children.Add(label);
                panel.Children.Add(hidden);
                var policy = new RuntimeEvidencePolicy(output, redactedText: [secret], excludedControlAutomationIds: ["Excluded"]);
                var observation = new RuntimeObservationRequest(runtime.SessionId, maxDepth: 8,
                    outputDirectory: Path.Combine(output, "run"), policy: policy, requestId: "private-run", maxInlineBytes: 131072);
                var initial = (await runtime.ObserveChangesAsync(new RuntimeObservationChangesRequest(observation))).Value!;
                Assert.DoesNotContain(secret, JsonSerializer.Serialize(initial), StringComparison.Ordinal);
                Assert.DoesNotContain("excluded-private-value", JsonSerializer.Serialize(initial), StringComparison.Ordinal);
                label.Text = secret + " changed";
                hidden.Text = "excluded-private-updated";
                var changed = await new RuntimeObserver().ObserveChangesAsync(client, new RuntimeObservationChangesRequest(observation, initial.Cursor));
                Assert.True(changed.Success, changed.Error?.Message);
                var json = JsonSerializer.Serialize(changed.Value);
                Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
                Assert.DoesNotContain("excluded-private", json, StringComparison.Ordinal);
                Assert.Contains("changed", json, StringComparison.Ordinal);
                var noPolicy = new RuntimeObservationRequest(runtime.SessionId, maxDepth: 8);
                var mismatch = (await runtime.ObserveChangesAsync(new RuntimeObservationChangesRequest(noPolicy, changed.Value!.Cursor))).Value!;
                Assert.True(mismatch.ResyncRequired);
                Assert.Equal("cursor_scope_mismatch", mismatch.ResyncReason);
                var replay = (await runtime.ObserveChangesAsync(new RuntimeObservationChangesRequest(observation, initial.Cursor))).Value!;
                Assert.Equal(changed.Value.Events.Select(item => item.Sequence), replay.Events.Select(item => item.Sequence));
                Assert.DoesNotContain(secret, JsonSerializer.Serialize(replay), StringComparison.Ordinal);
                var compactRequest = new RuntimeObservationRequest(runtime.SessionId, maxDepth: 8,
                    outputDirectory: Path.Combine(output, "run"), policy: policy, requestId: "private-run", maxInlineBytes: 4096);
                var compact = await new RuntimeObserver().ObserveChangesAsync(client, new RuntimeObservationChangesRequest(compactRequest));
                Assert.True(compact.Success, compact.Error?.Message);
                Assert.True(compact.Value!.RequiresArtifactRead);
                Assert.Null(compact.Value.Baseline);
                Assert.True(JsonSerializer.SerializeToUtf8Bytes(compact.Value).Length <= 4096);
                Assert.DoesNotContain(secret, await File.ReadAllTextAsync(compact.Value.ResponseBudget!.ArtifactPath!), StringComparison.Ordinal);
            });
        }
        finally { if (Directory.Exists(output)) Directory.Delete(output, recursive: true); }
    }

    private static async Task WithApp(Func<AvaScopeBridgeRuntime, Window, StackPanel, LocalBridgeClient, Task> test)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        await session.Dispatch(async () =>
        {
            AvaScopeBridge.Deactivate();
            var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Change observation"));
            var panel = new StackPanel();
            var window = new Window { Width = 320, Height = 220, Content = panel };
            try
            {
                window.Show();
                using var registration = runtime.RegisterTopLevel(window);
                await test(runtime, window, panel, new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!));
            }
            finally
            {
                window.Close();
                AvaScopeBridge.Deactivate();
            }
        }, CancellationToken.None);
    }
}
