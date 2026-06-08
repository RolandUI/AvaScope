# BUG-0001: PreviewHost fails for Window-rooted AXAML previews

- Status: `Implemented`
- Fix Status: `Fixed`
- Stored: `2026-06-08`
- Privacy Review: local absolute paths and personal directory names were replaced with placeholders.

## Summary

AvaScope PreviewHost fails when rendering an Avalonia view whose root control is a `Window`. `UserControl`-rooted views render successfully.

## Environment

- AvaScope alpha from: `<avascope-root>`
- AvaScope executable used: `<avascope-root>\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe`
- Target app: external Avalonia application
- Target project: `<target-app-root>\TargetApp.csproj`
- Target Avalonia version: `12.0.2`
- OS: Windows

## Reproduction

Run a preview for a `Window`-rooted AXAML view:

```powershell
& "<avascope-root>\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe" preview "<target-app-root>\TargetApp.csproj" --view "Views\MainWindow.axaml" --out "<output-root>\main-window-preview.png" --width 1440 --height 900 --theme dark
```

## Actual Result

Preview fails with `preview_render_failed` during the render phase:

```json
{
  "success": false,
  "error": {
    "code": "preview_render_failed",
    "message": "The control TargetApp.Views.MainWindow (Name = RootWindow, Content = Grid (Name = RootLayoutGrid)) already has a visual parent TopLevelHost while trying to add it as a child of ContentPresenter (Name = PART_ContentPresenter, Host = Window).",
    "details": {
      "phase": "render",
      "exceptionType": "System.InvalidOperationException"
    }
  }
}
```

## Expected Result

AvaScope should detect that the requested AXAML root is already a `Window` or `TopLevel` and render it directly instead of wrapping it inside another host `Window`.

## Resolution

Implemented on `2026-06-08`.

- PreviewHost now uses a loaded `Window` root as the render window instead of assigning it as another window's content.
- Non-window `Control` roots continue to render through the existing host `Window` path.
- Regression coverage: `PreviewHostRendersCompiledWindowRootViewDirectly`.

## Control Case

These `UserControl`-rooted views render successfully:

```powershell
& "<avascope-root>\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe" preview "<target-app-root>\TargetApp.csproj" --view "Views\ChartView.axaml" --out "<output-root>\chart-view-preview.png" --width 1440 --height 900 --theme dark

& "<avascope-root>\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe" preview "<target-app-root>\TargetApp.csproj" --view "Views\LiveTradeView.axaml" --out "<output-root>\live-trade-view-preview.png" --width 1440 --height 900 --theme dark
```

Both return `success: true`.

## Suspected Cause

PreviewHost likely wraps every loaded root control in a host `Window`. That is correct for `UserControl` or `Control` roots, but wrong for roots that are already `Window` or another `TopLevel`.

## Original Suggested Fix

When selected for implementation, check the PreviewHost render flow:

- If the loaded root is `Window` or `TopLevel`, use it as the render target directly.
- If the loaded root is a normal `Control`, wrap it in the preview host window as today.
- Add regression tests for `UserControl` roots and `Window` roots.
