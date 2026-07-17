# ADR 0001 — WPF-native sketch architecture

- **Status:** Accepted
- **Date:** 2026-07-16
- **Scope:** `Lan.Shapes`, `Lan.SketchBoard`, `Lan.ImageViewer`, `Lan.ImageViewer.Prism`, extension packages, sample hosts
- **Related:** [`docs/architecture-issues.md`](../architecture-issues.md), [`docs/refactor-checklist.md`](../refactor-checklist.md)

## Context

Lan.Shapes is a Windows-only WPF image annotator / geometry sketcher. Shapes are `DrawingVisual` instances for performance. The library is not planned for non-Windows platforms.

Recent work split `ISketchBoardDataManager` into:

| Contract | Concern |
|---|---|
| `IShapeRepository` | shape collections, selection, tools, CRUD, events |
| `IVisualHost` | `VisualCollection`, board attach, scale feedback |
| `ISketchBoardDataManager` | combined control-facing facade |

That split is correct for a WPF-only stack, but consumers (especially ViewModels) still take the fat surface, scale is applied from two paths, and several shape lifecycle edges remain inconsistent.

## Decision

Stay **WPF-native**. Optimize for clear ownership and one lifecycle inside WPF — do **not** introduce cross-platform geometry abstractions.

### Target ownership model

```
Composition root (App / ImageViewerModule)
  registers tools, loads layer JSON, wires dialogs / Halcon

IImageViewerViewModel
  image, scale, palette, commands, selection intent
  depends on IShapeRepository for shape state

SketchBoard + ImageViewer controls
  depend on ISketchBoardDataManager / IVisualHost

IShapeRepository  ──owns──►  ShapeVisualBase instances
ShapeVisualBase   ──holds──► ShapeLayer (styler / units lookup only)
ShapeLayer                   stylers + unit config only (no shape list)
SketchBoard                  VisualCollection mirror of repository shapes
```

### Layer rules (WPF-only)

| Layer | May know | Must not know |
|---|---|---|
| Shape (`ShapeVisualBase`) | `DrawingVisual`, pens, hit geometry, handles | ImageViewer, IoC, DXF UI, tool palette |
| Repository (`IShapeRepository`) | shape list, selection, tools, events | zoom chrome, toolbar commands |
| Visual host (`IVisualHost` / board) | `VisualCollection`, board attach, scale notify | recipes, Halcon, dialogs |
| VM (`IImageViewerViewModel`) | image, scale, palette, delete/zoom intent | `RenderOpen`, handle hit-testing |
| Composition root | concrete shape types, JSON paths, DI | almost nothing else |

### Shape lifecycle (single pipeline)

```
Create (ShapeFactory)
  → optional IBoardContextAware
  → interactive sketch  OR  FromData (shared geometry builders)
  → commit (IsGeometryRendered / NewShapeSketched)
  → select / resize / translate
  → lock / unlock
  → remove
```

Invariants:

1. Constructor only takes `ShapeLayer` and wires empty geometry — no dialogs, no board-size assumptions.
2. Board dimensions only via `IBoardContextAware`.
3. `FromData` and mouse sketch call the **same** private geometry builders.
4. Exactly one `RenderOpen()` per visual update.
5. `OnSelected` / `OnDeselected` are empty `virtual` hooks, never forced throws.

### Scale policy (single path)

**Zoom scale is authoritative** for stroke thickness and drag-handle size:

```
thickness = baseStroke / max(scale, ε)
handle    = baseHandle / max(scale, ε)
```

Rules:

- Drive updates from `ImageViewer.LocalScale` → `IVisualHost.OnImageViewerPropertyChanged(scale)`.
- Do **not** re-apply a second competing formula from `SketchBoard.SizeChanged` after zoom is live.
- Viewport-size formula may seed **initial** defaults only (first attach / no scale yet), or be removed.
- Prefer per-manager / per-board `ViewportScalingOptions` over process-wide mutable bases. Static defaults may remain as read-only fallbacks.

### Dependency guidance

```csharp
// ViewModels, services, unit tests
IShapeRepository

// SketchBoard, ImageViewer control wiring only
ISketchBoardDataManager  // and/or IVisualHost
```

`ISketchBoardDataManager` stays as the **control-facing facade**. It is not the default dependency for application logic.

### Extensibility

- New shapes live in `Lan.Shapes`, `Lan.Shapes.Custom`, or `Lan.Shapes.DialogGeometry`.
- Register tools at the composition root (`GeometryTypeRegistration` / host startup).
- Core packages must not reference Custom/Dialog (already true — keep it).
- Palette icons via `IGeometryIconProvider` (default: `ResourceDictionaryGeometryIconProvider` → `Geometries.xaml`); VM does not own an icon dictionary.
- Layer stylers via `IShapeStylerFactory` injected into `ShapeLayer` / `ShapeLayerManager`.
- Board-size-dependent shapes implement `IBoardContextAware`; cancel mid-create via `ShapeCreationCancelled` → repository remove.

### Explicit non-goals

- No Skia / Avalonia / cross-platform geometry core.
- No requirement that `IShapeRepository` avoid `System.Windows` types.
- No Model-View split of every shape unless a concrete pain appears; `DrawingVisual` + `IDataExport<T>` is the supported model.

## Consequences

### Positive

- Multi-viewer zoom no longer fights itself once dual scale paths are collapsed.
- VMs/tests can depend on shape state without visual-host members.
- New shapes follow one documented lifecycle.
- Public API can shrink to what is actually implemented.

### Negative / cost

- ISP migration requires VM + XAML binding updates.
- Removing `SketchBoard.SizeChanged` scaling needs a visual check at first load and after zoom.
- Deleting unused interfaces is a breaking API cleanup if anything external referenced them (none in-repo today).

### Follow-up work

Execute [`docs/refactor-checklist.md`](../refactor-checklist.md) in phase order. Keep [`docs/architecture-issues.md`](../architecture-issues.md) status table updated as items land.

## Alternatives considered

1. **Cross-platform pure domain model** — rejected; no second UI target, high cost, fights `DrawingVisual` performance model.
2. **Keep fat `ISketchBoardDataManager` everywhere** — rejected; forces tests and VMs through visual-host surface.
3. **FrameworkElement-per-shape** — rejected; worse performance for dense annotations.
