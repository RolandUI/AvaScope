using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    public async Task<CoreResult<RuntimeVirtualItemResponse>> VirtualItemAsync(RuntimeVirtualItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var elapsed = Stopwatch.StartNew();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(request.TimeoutMs);
        var scanned = 0;
        var scrolls = 0;
        var selectionDispatched = false;
        try
        {
            while (true)
            {
                var result = await Dispatcher.UIThread.InvokeAsync(() => Step(), DispatcherPriority.Background, deadline.Token);
                if (!result.Success || result.Value!.Status != "realizing") return result;
                await Task.Delay(25, deadline.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("timeout", "Item resolution or realization exceeded its deadline. Scroll may have occurred; no successful selection is claimed.");
        }
        catch (Exception exception) when (exception is TargetInvocationException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            return Failure("unsupported", $"The public item model could not be safely resolved ({exception.GetType().Name}). No alternate item was selected.");
        }

        CoreResult<RuntimeVirtualItemResponse> Step()
        {
            deadline.Token.ThrowIfCancellationRequested();
            if (request.Collection.SessionId != SessionId || _sessionRegistry.Get(SessionId).Value?.State != SessionLifecycleState.Active)
                return Failure("stale_collection", "The collection session is unavailable or differs from the selected bridge.");
            var topLevel = FindTopLevel(request.Collection.TopLevelId);
            if (topLevel is null) return Failure("stale_collection", "The collection window is no longer registered.");
            var resolved = ResolveMutationTarget(topLevel, request.Collection);
            if (!resolved.Success) return Failure("stale_collection", resolved.Error!.Message);
            if (resolved.Value!.Node is not ItemsControl items)
                return Failure("unsupported", "The collection must expose the public Avalonia ItemsControl/ItemsView contract.");
            if (request.Action == "select" && items is not SelectingItemsControl)
                return Failure("unsupported", "This collection does not expose public SelectingItemsControl selection.");
            var match = ResolveKey(items);
            if (!match.Success) return CoreResult<RuntimeVirtualItemResponse>.Fail(match.Error!);
            var index = match.Value;
            var container = items.ContainerFromIndex(index);
            var rendered = container is not null && CreateInteractionState(topLevel, container)?.Rendered == true;
            if (request.Action != "find")
            {
                if (!items.IsEffectivelyVisible || !items.IsEffectivelyEnabled)
                    return Failure("not_actionable", "The collection must be visible and enabled before scrolling or selection.");
                if (!rendered)
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    items.ScrollIntoView(index);
                    scrolls++;
                    return Response("realizing", index, items, container, rendered, resolved.Value.Target);
                }
                // Re-scan after realization: recycled containers and changed collections are never logical identity.
                var verified = ResolveKey(items);
                if (!verified.Success) return CoreResult<RuntimeVirtualItemResponse>.Fail(verified.Error!);
                if (verified.Value != index || items.IndexFromContainer(container!) != index)
                    return Failure("stale_item", "The item moved during resolution; refresh its logical key before retrying.");
                if (!container!.IsEffectivelyEnabled)
                    return Failure("not_actionable", "The realized item is disabled.");
                if (request.Action == "select")
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    var selection = (SelectingItemsControl)items;
                    if (selection.SelectedIndex != index)
                    {
                        selectionDispatched = true;
                        selection.SelectedIndex = index;
                    }
                    var after = ResolveKey(items);
                    if (!after.Success || selection.SelectedIndex != after.Value)
                        return Failure("selection_unverified", "Selection was dispatched, but its logical-key postcondition did not hold. Do not blindly retry a side effect.");
                    index = after.Value;
                    container = items.ContainerFromIndex(index);
                    rendered = container is not null && CreateInteractionState(topLevel, container)?.Rendered == true;
                }
            }
            return Response(request.Action == "find" ? "found" : request.Action == "select" ? "selected" : "revealed",
                index, items, container, rendered, resolved.Value.Target);

            CoreResult<RuntimeVirtualItemResponse> Response(string status, int itemIndex, ItemsControl owner, Control? realized,
                bool isRendered, RuntimeTargetContext target) => CoreResult<RuntimeVirtualItemResponse>.Ok(new RuntimeVirtualItemResponse(
                    target, request.KeyProperty, request.Key, request.Action, status, itemIndex, owner.ItemsView.Count,
                    realized is not null, isRendered, owner is SelectingItemsControl selection ? selection.SelectedIndex == itemIndex : null,
                    realized is null ? null : CreateNodeTarget(target.TopLevelId, TreeKinds.Visual, topLevel, realized),
                    elapsed.ElapsedMilliseconds, scanned, scrolls));
        }

        CoreResult<int> ResolveKey(ItemsControl items)
        {
            var count = items.ItemsView.Count;
            if (count > request.MaxItems)
                return KeyFailure("search_limit", "Uniqueness requires the complete logical collection; narrow the host collection or raise maxItems within its fixed limit.");
            var matched = -1;
            for (var index = 0; index < count; index++)
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (elapsed.ElapsedMilliseconds >= request.TimeoutMs) throw new OperationCanceledException(deadline.Token);
                if (items.ItemsView.Count != count) return KeyFailure("stale_item", "The logical collection changed while reading keys.");
                var item = items.ItemsView[index];
                if (item is null) return KeyFailure("unsupported_identity", "Every logical item must expose the requested scalar key.");
                var property = item.GetType().GetProperty(request.KeyProperty, BindingFlags.Instance | BindingFlags.Public);
                if (property?.GetMethod?.IsPublic != true || property.GetIndexParameters().Length != 0 || !IsItemKeyType(property.PropertyType))
                    return KeyFailure("unsupported_identity", "The requested key must be one public non-indexed string, Guid or integer property on every item.");
                var value = property.GetValue(item);
                scanned++;
                var key = value switch
                {
                    string text => text,
                    Guid id => id.ToString("D", CultureInfo.InvariantCulture),
                    IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
                    _ => null
                };
                if (string.IsNullOrWhiteSpace(key) || key.Length > 256)
                    return KeyFailure("unsupported_identity", "Every logical item key must contain 1–256 characters.");
                if (!string.Equals(key, request.Key, StringComparison.Ordinal)) continue;
                if (matched >= 0) return KeyFailure("ambiguous", "Multiple logical items have the requested key; no first-match fallback is allowed.");
                matched = index;
            }
            deadline.Token.ThrowIfCancellationRequested();
            if (items.ItemsView.Count != count) return KeyFailure("stale_item", "The logical collection changed while reading keys.");
            return matched >= 0 ? CoreResult<int>.Ok(matched) : KeyFailure("not_found", "The requested logical key is absent from the complete bounded collection.");
        }

        CoreResult<int> KeyFailure(string code, string message) => CoreResult<int>.Fail(new CoreError("virtual_item_" + code, message));
        CoreResult<RuntimeVirtualItemResponse> Failure(string code, string message) => CoreResult<RuntimeVirtualItemResponse>.Fail(
            new CoreError("virtual_item_" + code, message, new Dictionary<string, string>
            {
                ["scannedItems"] = scanned.ToString(CultureInfo.InvariantCulture),
                ["scrollRequests"] = scrolls.ToString(CultureInfo.InvariantCulture),
                ["selectionDispatched"] = selectionDispatched.ToString().ToLowerInvariant(),
                ["elapsedMs"] = elapsed.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)
            }));
    }

    private static bool IsItemKeyType(Type type) => type == typeof(string) || type == typeof(Guid)
        || type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
        || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong);
}
