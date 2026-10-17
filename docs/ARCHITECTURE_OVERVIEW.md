# Architecture overview

## Intent and target

Lan.Shapes is a **Windows-only WPF image annotation and 2D geometry sketcher**,
with `DrawingVisual` rendering for dense overlays. It deliberately does not
model every shape as its own `FrameworkElement`, and it does not introduce a
platform-independent CAD kernel. See [ADR 0001](adr/0001-wpf-native-sketch-architecture.md).

## Assemblies and dependency direction

```text
Host application (Prism/DryIoc or Microsoft DI)
    |
    +-- Lan.ImageViewer.Prism : default VM, registration, config composition
    |       |
    |       +-- Lan.ImageViewer : image viewer, chrome, controls
    |       +-- Lan.Shapes.DialogGeometry : DXF/grid-related shapes/dialogs
    |
    +-- Lan.ImageViewer : image viewer, palette, zoom/pan
            |
            +-- Lan.SketchBoard : WPF board + VisualCollection host
            +-- Lan.Shapes : core geometry/rendering/contracts
            +-- Lan.Shapes.Custom : extended geometries

Lan.ImageViewer.Halcon : optional HALCON-specific adapter/host
```

This is a conceptual map; see the actual `.csproj` ProjectReferences for
packaging inputs. The two consumer NuGet packages contain different sets of
assemblies; [maintainer guide](MAINTAINER_GUIDE.md) has the exact declared
versions and package boundaries.

## State ownership

| Component | Owns / does | Should not own |
| --- | --- | --- |
| `ShapeVisualBase` and concrete shapes | Geometry, visual rendering, hit testing, handles, shape-specific state | Dependency injection, application dialogs, image toolbar |
| `IShapeRepository` / `SketchBoardDataManager` | Shape collection, selection, creation/removal, grouping, history | Viewer toolbar/palette presentation |
| `SketchBoard` and `IVisualHost` | WPF VisualCollection host and pointer interactions | Recipe/document file ownership |
| `ShapeLayer` | Styling and measurement unit profile | Shape collection or document serialization |
| `IShapeLayerManager` | Shared layer definitions and configuration loading/saving | Full drawing document state |
| `IImageViewerViewModel` | Image/viewer state and user commands | Direct visual drawing calls |
| Host composition root | DI, icon catalog, tool registration, configuration and optional HALCON | Shape's geometric algorithms |

The `ISketchBoardDataManager` facade is intentionally control-facing.
Application logic should prefer the smaller `IShapeRepository` contract.
The two can resolve to the **same instance**, so avoid accidentally creating
independent repositories when registering or resolving services.

## Shape lifecycle

```text
Register tool / ShapeFactory
         |
Create new interactive shape or load its own metadata (FromData)
         |
Rebuild shape geometry -> request one visual update
         |
Commit completed sketch -> register/host/select
         |
Edit/resize/translate/lock/group -> refresh model + rendered visual
         |
Remove shape -> detach host and clean up selection/history relations
```

`ShapeVisualBase` derives from `DrawingVisual` and defines standard
extension points. Built-in shape rendering, hit-testing and handle behavior
must remain consistent with the repository's lifecycle. Prefer a single
geometry source of truth and avoid duplicating model vs display transforms.

### Zoom and on-screen sizes

`ImageViewer.LocalScale` drives the live stroke/handle size policy through
`ViewportScalingService`. The intended formula is
`baseSize / max(scale, epsilon)`; no second competing window-size scaler
should modify the same visual state after viewer attachment. Board-specific
options prevent one camera's scale settings from mutating another viewer.

### Extensibility expectations

- New tools register at the host composition root through
  `GeometryTypeRegistration` / `IGeometryTypeManager`.
- For per-shape metadata, use `IDataExport<T>` and an explicit
  `FromData` reconstruction path. These are **not** a whole-document
  serialization service.
- `GetSnapPoints()` supplies geometry-specific snap anchors.
- Movable custom shapes override `CanTranslate` and
  `TranslateCore(Vector)` and update model coordinates, not visual-only
  offsets.
- Shape history support uses capture/restore state contracts. Custom shapes
  that mutate geometry must implement the appropriate snapshot behavior or
  allow history invalidation; see [undo/redo](undo-redo.md).
- Keep sample-host initialization, DI lifetime ownership and event unsubscription
  aligned with [layer-management notes](layer-management-fixes.md).

## Related source and design documents

- [Shape visual base](../src/Lan.Shapes/ShapeVisualBase.cs)
- [Repository interface](../src/Lan.Shapes/Interfaces/IShapeRepository.cs)
- [Board data manager](../src/Lan.SketchBoard/SketchBoardDataManager.cs)
- [History implementation](../src/Lan.SketchBoard/SketchBoardDataManager.History.cs)
- [Viewer control](../src/Lan.ImageViewer/ImageViewerControl.xaml)
- [ADR 0001](adr/0001-wpf-native-sketch-architecture.md)
- [Feature status and persistence](FEATURE_STATUS.md)
