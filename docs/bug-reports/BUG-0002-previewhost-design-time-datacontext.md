# BUG-0002: PreviewHost ignores Avalonia design-time DataContext metadata

- Status: `Stored`
- Fix Status: `Not started`
- Stored: `2026-06-08`
- Privacy Review: local absolute paths, personal directory names, and target-specific identifiers were replaced with placeholders or generic names.

## Summary

AvaScope PreviewHost does not appear to honor Avalonia design-time `DataContext` metadata when rendering AXAML previews. Views render successfully, but they still appear as if no `DataContext` was assigned, even when the view has `d:DataContext` set in XAML.

## Environment

- AvaScope alpha from: `<avascope-root>`
- AvaScope executable used: `<avascope-root>\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe`
- Target app: external Avalonia application
- Target project: `<target-app-root>\TargetApp.csproj`
- Target Avalonia version: `12.0.4`
- OS: Windows

## Reproduction

In a target app view, add Avalonia design-time preview metadata:

```xml
<UserControl
    x:Class="TargetApp.Views.LiveTradeView"
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:design="clr-namespace:TargetApp.DesignTime"
    xmlns:vm="clr-namespace:TargetApp.ViewModels"
    x:CompileBindings="True"
    x:DataType="vm:LiveTradeViewModel"
    d:DataContext="{x:Static design:TargetDesignData.LiveTrade}"
    d:DesignWidth="1440"
    d:DesignHeight="900"
    mc:Ignorable="d">
```

The design data source exists in the target assembly:

```csharp
namespace TargetApp.DesignTime;

public static class TargetDesignData
{
    public static LiveTradeDesignData LiveTrade { get; } = new();
}

public sealed class LiveTradeDesignData
{
    public string ConnectButtonText { get; } = "Connect";
    public decimal Balance { get; } = 10472.35m;
    public decimal AvailableBalance { get; } = 7420.12m;
    public string MarketWsStateText { get; } = "Connected";
}
```

Then run:

```powershell
& "<avascope-root>\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe" preview "<target-app-root>\TargetApp.csproj" --view "Views\LiveTradeView.axaml" --out "<output-root>\live-trade-design-context-smoke.png" --width 1440 --height 900 --theme dark
```

## Actual Result

The preview command succeeds, but the rendered image still behaves as if `DataContext` is null or missing. Design-time values from `TargetDesignData.LiveTrade` are not visible in the rendered preview.

Observed command result:

```json
{
  "success": true,
  "value": {
    "filePath": "<output-root>\\live-trade-design-context-smoke.png",
    "pixelWidth": 1440,
    "pixelHeight": 900,
    "dpi": 96,
    "projectPath": "<target-app-root>\\TargetApp.csproj",
    "viewPath": "<target-app-root>\\Views\\LiveTradeView.axaml",
    "themeVariant": "dark"
  }
}
```

## Expected Result

AvaScope should support Avalonia design-time `DataContext` metadata and apply it during preview rendering, so views with `d:DataContext` or `Design.DataContext` render with mock/design data.

Expected supported cases:

- `d:DataContext="{x:Static design:TargetDesignData.LiveTrade}"`
- `<Design.DataContext>...</Design.DataContext>`
- `d:DesignWidth` and `d:DesignHeight` should also be considered as default preview dimensions when width/height are not explicitly provided by the CLI or MCP request.

## Why This Matters

Avalonia IDE previewers use `d:DataContext` or `Design.DataContext` to make complex views render with safe mock data without instantiating side-effecting runtime view models. Target app view models may start API clients, websocket clients, cache loaders, timers, persisted workspace restore logic, or other services, so AvaScope should not require constructing runtime view models just to render a useful preview.

## Suggested Fix

Do not implement this until explicitly requested.

When selected for implementation, check the PreviewHost render flow:

- During preview loading, detect `d:DataContext` and `Design.DataContext` on the root element.
- For `d:DataContext` with `x:Static`, resolve the static property from the loaded project assembly and assign it to the root control `DataContext` before rendering.
- For `Design.DataContext` object element syntax, instantiate or load the provided design object if supported by Avalonia runtime/design APIs.
- Apply design width/height from `d:DesignWidth` and `d:DesignHeight` if the preview request does not explicitly specify dimensions.
- Keep this isolated in PreviewHost, not MCP.
- Return structured diagnostics if the design data expression cannot be resolved.

## Regression Tests To Add

- `UserControl` with `d:DataContext="{x:Static ...}"` renders text bound to the static design object.
- `UserControl` without `d:DataContext` keeps current behavior.
- Invalid `d:DataContext` returns a structured preview diagnostic instead of crashing.
- `d:DesignWidth` and `d:DesignHeight` are used as fallback dimensions when request dimensions are absent or optional in a future API shape.
