# Stable selectors and virtualized logical items

`audit-ui` / `audit_ui` reports duplicate automation ids as well as missing ids. Duplicate matching follows the exact ordinal, case-sensitive identifier search used by `find-nodes`: `Save` and `save` are distinct identities. It returns bounded `selectorRecommendations`, preferring a unique AutomationId and then a unique name/type combination. Recommendations include their measured match count and uncertainty. Localized display text, transient runtime node ids and list positions are never suggested as stable identities.

Verification covers exactly the returned tree snapshot at the requested depth. It cannot establish uniqueness in unseen descendants or unrealized virtualized items. `selectorVerificationScope` states this boundary; re-resolve a recommended selector immediately before acting. Name/type selectors depend on template details, and AutomationIds depend on the host's identity contract. Missing or ambiguous identities produce host-source suggestions without editing source. Recommendations share the existing inventory output limit (at most 100).

## Logical item operations

`avascope virtual-item --request item.json` and MCP `virtual_item(request)` operate on one explicit visual collection target from current inspection/find results:

```json
{
  "collection": {
    "sessionId": "selected-session",
    "topLevelId": "selected-window",
    "treeKind": "visual",
    "nodeId": "current-list-node",
    "nodeGeneration": "current-list-generation"
  },
  "keyProperty": "Id",
  "key": "invoice-173",
  "action": "select",
  "maxItems": 2000,
  "timeoutMs": 1000
}
```

Copy the complete collection target, including generation fields, rather than inventing these identifiers. `keyProperty` explicitly selects one public non-indexed string, Guid or integer property on the application's item model. It must be an application-defined stable identity, independent of the display label and localization. No dotted path, expression, arbitrary method, arbitrary object formatting or first-match fallback is evaluated. Every item must expose a supported nonempty key; the requested key must be unique across the complete bounded collection. Keys compare ordinally and integers/Guids use invariant formatting.

`find` reads logical data without scrolling or selecting and can return an off-screen item without a container. `reveal` requests public `ItemsControl.ScrollIntoView` and waits for a realized rendered container. `select` also sets the desired `SelectingItemsControl.SelectedIndex`, then checks the selected logical key. An already selected item does not trigger a redundant setter. The collection and realized item must be enabled and visible for actions; public semantic selection does not claim native desktop input.

After realization, the bridge resolves the key again and checks the current container/index mapping. A recycled container is never retained as logical item identity. Returned indices, node ids and object generations are diagnostic evidence only; subsequent operations must reuse the logical key against a fresh collection target. A selection handler can change application state. Inspect errors and `selectionDispatched` where returned before retrying; transport failure or cancellation does not prove that no side effect occurred.

## Bounds and supported controls

The implementation uses the public Avalonia 12.1 [`ItemsControl.ItemsView`, `ContainerFromIndex`, `IndexFromContainer` and `ScrollIntoView` APIs](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/ItemsControl.cs) and [`SelectingItemsControl.SelectedIndex`](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/Primitives/SelectingItemsControl.cs). It supports virtualized ListBox and compatible ItemsControl collections, including tabular row templates. Third-party tables/DataGrid implementations without this contract return unsupported; cell/column operations are not inferred from visual coordinates.

The default complete-collection limit is 2000 items, configurable from 1 to 10000. Larger collections fail before any scrolling or selection; filter/paginate the host collection rather than trusting partial uniqueness. Deadlines accept 50–3000 ms, default 1000; realization polls every 25 ms with cancellation and deadline checks between scans. Responses expose scanned-item count, scroll requests and elapsed time. Collection reads, realization and selection run on the UI dispatcher. As with other in-process public APIs, a slow application getter or synchronous event handler cannot be forcibly preempted: key getters must be fast, deterministic and side-effect free. The deadline is checked between calls, and the client transport remains bounded.

Tests use 200-row virtualized two-column fixtures, off-screen keys, recycled containers, reordering, duplicate/localized labels, duplicate keys introduced during realization, stale generation contexts, unsupported properties, search limits, timeout/cancellation and actual CLI/MCP parity for both item results and audit recommendations.
