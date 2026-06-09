# BUG-0003: Preview diagnostics reports false positive warnings for DataTemplate bindings and template internals

- Status: `Stored`
- Fix Status: `Not started`
- Stored: `2026-06-09`
- Privacy Review: local absolute paths, personal directory names, and target-specific identifiers were replaced with placeholders or generic names.

## Summary

AvaScope CLI `0.2.1` renders an external Avalonia `SettingsView.axaml` successfully, but reports noisy diagnostics that appear to be false positives. The rendered preview is visually correct, but the browser viewer shows 35 warnings:

- 9 `binding_path_not_found`
- 21 `elements_overlap`
- 3 `text_clipped`
- 2 `hit_target_too_small`

At least the DataTemplate binding warnings and most template/layer overlap warnings appear to be AvaScope diagnostics issues rather than target app UI defects.

## Environment

- AvaScope CLI: `0.2.1`
- Target app: external Avalonia application
- Target project: `<target-app-root>\TargetApp.csproj`
- Target view: `<target-app-root>\Views\SettingsView.axaml`
- Output image: `<output-root>\SettingsView.png`
- Render size: `1200x900`
- Theme: `dark`
- OS: Windows

## Reproduction

Run a persisted preview session for the settings view:

```powershell
avascope create-preview-session "<target-app-root>\TargetApp.csproj" --view "Views\SettingsView.axaml" --out "<output-root>\SettingsView.png" --theme dark --width 1200 --height 900
```

Then inspect `lastRender.value.diagnostics` or open the generated preview viewer.

## Actual Result

The preview render succeeds and the output image is visually acceptable, but AvaScope reports 35 diagnostics. The report groups the noisy warnings into four categories.

### DataTemplate Binding False Positives

AvaScope reports `binding_path_not_found` for bindings under `ItemsControl.ItemTemplate`, even when the template declares an item `x:DataType` and the bound properties exist on the item view model.

Example shape:

```xml
<ItemsControl ItemsSource="{Binding SystemProfileOptions}">
  <ItemsControl.ItemTemplate>
    <DataTemplate x:DataType="vm:SystemProfileOptionViewModel" x:CompileBindings="False">
      <ToggleButton
          Classes.serverProfile="{Binding IsServerProfile}"
          Classes.performanceProfile="{Binding IsPerformanceProfile}">
        <TextBlock Text="{Binding DisplayName}" />
      </ToggleButton>
    </DataTemplate>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

Observed diagnostic shape:

```text
Binding path 'IsServerProfile' was not found on preview DataContext 'TargetApp.ViewModels.SettingsViewModel'.
```

The diagnostic details also include a template data type such as:

```text
dataTypeName: vm:SystemProfileOptionViewModel
dataContextType: TargetApp.ViewModels.SettingsViewModel
```

This is contradictory: AvaScope recognizes the template `x:DataType`, but still validates the binding path against the root view model. The same issue appears for another item template whose bindings target properties such as `Label`, `WebhookUrl`, and `RemoveCommand` on an item view model.

### Template And Layer Overlap False Positives

AvaScope reports `elements_overlap` for normal Avalonia template/layer internals, including root-layer and full-window visuals such as:

```text
Avalonia.Controls.Primitives.VisualLayerManager bounds 0,0,1200,900
Avalonia.Controls.Panel bounds 0,0,1200,900
Avalonia.Controls.Border bounds 0,0,1200,900
```

It also reports overlaps for normal control-template internals, such as icon `Viewbox` plus `Path` visuals and slider or checkbox template parts. No visible bad overlap appears in the rendered image.

### Text Clipping False Positives

AvaScope reports `text_clipped` for tab header labels, where the desired size slightly exceeds rendered bounds. The rendered labels do not appear visually clipped. This looks like a font metric or line-height tolerance issue rather than visible pixel clipping.

### Template Internal Hit Target Noise

AvaScope reports `hit_target_too_small` for slider internal `RepeatButton` parts. These may technically be below the 24x24 policy, but they are internal template parts rather than independent user-facing buttons. The full control interaction area is the more useful diagnostic target.

## Expected Result

AvaScope should reduce diagnostic noise for visually correct previews:

- Binding diagnostics under `DataTemplate` should validate against the template item context or `x:DataType`, not the root view model.
- If `DataTemplate x:DataType` is known, it should be treated as the authoritative binding context even when `x:CompileBindings="False"`.
- Layout overlap diagnostics should ignore or downgrade natural template/layer overlaps, including `VisualLayerManager`, root overlays, icon `Viewbox` plus `Path` compositions, and slider/checkbox template internals.
- Text clipping diagnostics should tolerate small font metric differences and only warn when clipping is visually meaningful.
- Hit-target diagnostics should evaluate the user-facing control target, or downgrade internal slider template parts to a lower-priority hint.

## Suspected Cause

Preview diagnostics currently appear to use the root preview `DataContext` for some template binding checks even when the source metadata contains a DataTemplate `x:DataType`. Layout diagnostics appear to compare sibling visuals without filtering Avalonia control-template internals or root layer structures.

## Suggested Fix

When selected for implementation:

- Make binding diagnostics template-scope aware.
- Prefer `DataTemplate x:DataType` as the binding context for template-contained bindings.
- Infer `ItemsControl.ItemsSource` element type where reliable, but avoid root-DataContext path warnings when the template data type is known.
- Add template-aware overlap filtering for root layers, visual layer managers, adorner/presenter structures, and common icon/control template internals.
- Add tolerance or pixel-aware checks for text clipping.
- Treat internal slider template hit targets as lower priority or evaluate the owning control's effective hit target.

## Regression Tests To Add

- `ItemsControl.ItemTemplate` with `x:DataType` and item-property bindings does not report root-DataContext `binding_path_not_found`.
- A template binding with `x:CompileBindings="False"` still avoids root context false positives when `x:DataType` is present.
- Fluent `Window`/root layer visuals do not produce full-window `elements_overlap` diagnostics.
- Icon `Viewbox` plus `Path` template internals do not produce noisy overlap diagnostics.
- Small text desired-height deltas do not produce `text_clipped` warnings when rendered bounds are visually acceptable.
- Slider internal `RepeatButton` parts are ignored, downgraded, or evaluated through the owning `Slider`.

## Priority

Medium. Rendering succeeds, but warning noise makes it harder for agents and users to distinguish real UI issues from AvaScope diagnostic false positives.
