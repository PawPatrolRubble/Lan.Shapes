# WpfGeometrySketcher

A high-performance WPF image viewer and geometry sketching control. Built on `DrawingVisual` for superior rendering performance compared to standard WPF shape controls, with extensible support for custom shapes.

## Features

- **Performance**: Built on `DrawingVisual` for optimized rendering
- **Shape Support**: Rectangle, ellipse, line, polygon, circle, cross, and ruler cross shapes
- **Custom Shapes**: Extensible architecture for custom geometry types
- **Shape Groups**: Group shapes on one layer for persistent selection and movement; ungroup to edit members separately
- **Zoom & Pan**: Mouse wheel zoom, middle-button drag panning, and CTRL+left-drag panning
- **Pixel Info**: Display RGB values at cursor position
- **Scale Display**: Real-time zoom ratio display
- **Auto-Sized Handles**: Drag handles automatically sized based on shape dimensions
- **Dialog Integration**: Grid rectangles with interactive row/column input
- **DXF Export**: Export shapes to DXF format

## Getting Started

### Canonical host: Prism + DryIoc (`Lan.Shapes.SimpleApp`)

1. Load the module and resolve the view-model from the container (do **not** `new` the VM):

```csharp
// App.xaml.cs (PrismApplication)
protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
{
    base.ConfigureModuleCatalog(moduleCatalog);
    moduleCatalog.AddModule<ImageViewerModule>();
}

// MainPageViewModel
Camera1 = ContainerLocator.Container.Resolve<IImageViewerViewModel>();
```

2. Bind the control:

```xml
<imageViewer:ImageViewerControl
    Margin="5"
    Padding="10"
    BorderBrush="Red"
    DataContext="{Binding Camera1}"
    BorderThickness="1" />
```

`ImageViewerModule` registers:

| Interface | Implementation / note |
|-----------|------------------------|
| `IGeometryTypeManager` | `GeometryTypeManager` (singleton) |
| `IShapeLayerManager` | `ShapeLayerManager` (singleton) |
| `IGeometryIconProvider` | `ResourceDictionaryGeometryIconProvider` |
| `IShapeStylerFactory` | `ShapeStylerFactory` |
| `IImageViewerViewModel` | `ImageViewerControlViewModel` (transient) |
| `ISketchBoardDataManager` | `SketchBoardDataManager` (transient) |
| `IShapeRepository` | same instance as the fat manager |

Lan.Shapes configuration is loaded from `lanShapesConfigPath`. The allowed palette types,
measurement calibration, and per-layer styling all live in that dedicated
file. A missing `AvailableGeometryTypes` value registers the full catalog; `[]` registers none.
Unknown names fail at startup. Crosshair overlay display is controlled via the `ImageViewerControl.ShowCrossLine` DependencyProperty.

The shipped file seeds `%LOCALAPPDATA%\Lan.Shapes\{entry assembly name}\LanShapesConfig.json`.
Subsequent loads and saves use that application's runtime copy. Set `lanShapesApplicationId`
for a stable application scope, or `lanShapesRuntimeDirectory` for a custom location.
An old shared runtime file is preserved; migrate known edits by reading it explicitly and
saving to the application's new path.

```json
{
  "AvailableGeometryTypes": [ "Line", "Angle", "Rectangle", "Rectangle2", "Circle", "Cross", "RulerCross", "DxfGeometry" ],
  "Measurement": {
    "PixelPerUnit": 3410,
    "UnitsPerMillimeter": 1000,
    "UnitName": "um"
  },
  "ShapeLayers": []
}
```

Each shape layer can set `AnnotationFontToHandleRatio` (default `1.5`). Built-in
measurement labels and tags use the normal drag-handle size multiplied by this
ratio: an 8 px handle gives a 12 px label. Both scale together with zoom, and
the label does not change size when the shape is selected or locked. The older
`TagFontSize` setting remains available for custom shapes that use it directly.

The toolbar's layer selector sets the current layer for newly drawn shapes and
stays synchronized with `SelectedShapeLayer`. The property pane shows configured layers as a tree, with each layer's
shapes beneath it. The checkbox hides or restores every shape on that layer for
the current board; hidden shapes stay in the repository and cannot be picked or
used as snap targets. The Edit button changes the layer name and description.
Choose Normal, Mouse hover, Selected, or Locked in the style-state selector to
edit that state's stroke/fill colors, dash pattern, stroke width, and fill opacity.
For a visible hover fill, set its color and an opacity above zero (0 is transparent,
1 is opaque). Switching states preserves the edits in the dialog draft; Apply
commits all edited states, while Cancel leaves the layer unchanged.
Existing shapes refresh immediately. If the layer manager loaded a configuration
file, edits are saved to that file. Stroke widths are treated as their on-screen
width at zoom 1 and scale with the viewport.

Use **Select** to enter selection mode. Ctrl-click toggles shapes, dragging from
empty space selects shapes fully inside the rectangle (left to right) or crossing
it (right to left), and Ctrl-drag adds to the selection. The tree also supports
Ctrl-click, Shift-click ranges, and selecting a layer's visible unlocked shapes.
Ctrl+A selects eligible shapes and Esc clears selection; the middle mouse button
pans the image. The **Assigned layer** selector applies to one or several selected
shapes, preserving their geometry and identity. Hidden target layers hide the
transferred shapes and remove them from selection.

Select at least two completed, unlocked, movable shapes on the same layer, then
choose **组合** (Group) in the selection card or press **Ctrl+G**. Clicking any
member selects the whole group; dragging a member moves all of its members while
preserving their relative positions, dimensions, styles, identities, and visual
order. A single outline encloses the group and member resize handles are hidden.
Choose **取消组合** (Ungroup) or press **Ctrl+Shift+G** to restore independent
editing without changing geometry. The same shortcuts work in the shape tree.

Ctrl-click toggles the entire group. A left-to-right selection rectangle must
contain all group members; a right-to-left rectangle can cross any member.
Double-click locks or unlocks all group members together. A locked member blocks
movement of the whole group. Assigned-layer changes apply to every member.
Hiding the layer preserves the group; deleting a selected group removes its
members. Directly removing/replacing a member or moving one member to another
layer through code dissolves its group.

Groups are board-local runtime relationships in this first version. Layer
configuration saves do not save drawing geometry or group relationships.
Nested groups, group rotation/scaling, movement snapping, and undo/redo are not
part of this version. Fixed-center circles and image-spanning ruler crosses
cannot join movable groups. Fiber respects its `EnableTranslation` setting.
Details and extension contracts: [`docs/shape-groups.md`](docs/shape-groups.md).

The layer pane provides **New**, **Save**, and **Save as**. New layers copy the
current definition's full state styles and receive a unique ID; edited names must
be nonempty and unique. With an existing configuration path, confirming new or
edited definitions saves them automatically. Without a path, changes remain in
memory until Save chooses a JSON file. Saves replace the file only after writing
a complete temporary file, and failed saves preserve the previous file and path.
Layer configuration files contain layer definitions and global settings; drawing
geometry and shape-to-layer assignments are not included in this configuration.

The manager owns the layer catalogue, and each board owns independent runtime copies.
Direct supported definition/style edits mark the configuration unsaved and require Save;
manager edit commands retain the automatic save behavior described above. A complete
configuration reload updates existing shapes' calibration as well as their styles.
Removed definitions leave existing shapes visible and selectable in an orphan group,
which cannot edit the catalogue. Assign those shapes to a configured layer as needed.
Hiding a layer cancels its unfinished sketch and prevents new drawing on that hidden layer.

Viewer owners must dispose `IImageViewerViewModel` when permanently discarding it to
release shared manager subscriptions. The control does not dispose an externally owned
view-model. `MainPageViewModel` demonstrates owner disposal in Prism; MSDI hosts should
dispose their service scope or provider. Details: [`docs/layer-management-fixes.md`](docs/layer-management-fixes.md).

Custom implementations of `IShapeRepository`, `IShapeLayerManager`, and
`IImageViewerViewModel` must implement the new selection, layer-management, and group
members. Shapes with custom handle visibility should honor `ShowSelectionHandles`
when multiple shapes are selected.

Custom shapes opt into group movement with `CanTranslate` and `TranslateCore(Vector)`.
The base `Translate(Vector)` validates the request, coalesces redraws, and moves
attached text. Shapes must update their model coordinates and geometry in
`TranslateCore`; unadapted shapes keep `CanTranslate == false`.

`Shapes` and manager `Layers` retain their `ObservableCollection` API, with mutations
routed through repository lifecycle and catalogue validation. The compatibility viewer
`Layers` setter accepts its manager's collection only. `ShapeLayer.Stylers` exposes a
read-only dictionary; change state definitions through a `ShapeLayerParameter` draft.

Pointer updates coalesce geometry render requests so coordinate changes within a
single drag sample produce one redraw. Custom shape setters should call
`RequestVisualUpdate()`; `UpdateVisual()` remains the immediate drawing operation.
Use `DeferVisualUpdates()` to group related programmatic changes. Built-in tags
and line/circle measurement labels reuse glyph drawings when only their position
changes, with separate entries for font size, DPI, text, and foreground.

Full IoC walkthrough: [`scripts/IImageViewerViewModel-IoC使用说明.md`](scripts/IImageViewerViewModel-IoC使用说明.md).


### Alternate host: MSDI (`Lan.Shapes.TestApp`)

```csharp
_serviceCollection.AddSingleton<IShapeLayerManager, ShapeLayerManager>();
_serviceCollection.AddSingleton<IGeometryTypeManager>(geometryTypeManager);
_serviceCollection.AddSingleton<IGeometryIconProvider, ResourceDictionaryGeometryIconProvider>();
_serviceCollection.AddSingleton<IShapeStylerFactory, ShapeStylerFactory>();
_serviceCollection.AddTransient<IImageViewerViewModel, ImageViewerControlViewModel>();
_serviceCollection.AddTransient<ISketchBoardDataManager, SketchBoardDataManager>();
_serviceCollection.AddTransient<IShapeRepository>(
    sp => sp.GetRequiredService<ISketchBoardDataManager>());
```

Resolve `IImageViewerViewModel` from `IServiceProvider` the same way as any other transient service.

### Navigation Controls

- **Zoom**: Use mouse wheel to zoom in/out
- **Pan**: Hold the middle mouse button (press the scroll wheel) and drag to move the sketch area. Ctrl + left mouse gestures select shapes or add a selection rectangle.
- **Select overlapping geometry**: Click its outline; outlines and active resize handles take priority over shape interiors. Hold ALT and click repeatedly at the same point to cycle through overlapping unlocked geometries from topmost to bottommost. ALT + click selects without dragging or toggling locks. You can also select a geometry directly in the shape list.
- **Lock / unlock**: Double-click a completed geometry to lock it; its text turns gray. Double-click it again to unlock it and restore the usual text color.
- **Drawing**: With a drawing tool active, clicks create or continue the new geometry, including over existing shapes. Selection and double-click locking resume when drawing finishes; right-click ends the active sketch.
- **Line direction**: Choose Free, Horizontal, or Vertical in the toolbar's Line selector. In Free mode, hold SHIFT while drawing or resizing a line endpoint to use the nearest horizontal or vertical axis; release SHIFT to return to free drawing. Fixed Horizontal and Vertical modes persist across sketches. This also works for thickened and arrowed lines. The opposite endpoint stays fixed when resizing, and snapping only accepts anchors on the constrained axis.
- **Snapping**: While drawing or dragging a resize handle, points snap to nearby rectangle corners (including rotated rectangles), line endpoints and midpoints, and circle centers and quadrant points (top, right, bottom, and left), including locked shapes. When moving a circle or thickened circle, its center snaps to these anchors while preserving the pointer's grab offset and the circle's radius. A blue marker shows the target; the default distance is 8 screen pixels at every zoom level. Thickened rectangles, thickened lines, arrowed lines, fixed-center circles, and thickened circles also provide anchors. Thickened circle anchors follow the circle's centerline. The geometry being edited is excluded from snap targets.

Set `SketchBoard.IsSnappingEnabled` to disable snapping, or adjust `SketchBoard.SnapTolerance`
(in screen device-independent pixels). Custom shapes can override `ShapeVisualBase.GetSnapPoints()`
to supply model-space anchors. Snapping copies a position; moving the target later does not move
the geometry drawn there. Whole-shape movement, rotation, and stroke-width adjustment remain free
of snapping. Custom shapes can override `CanSnapDuringResize` to distinguish special handles
from geometry resize handles.

`SketchBoard.LineDirectionMode`, `ImageViewer.LineDirectionMode`, and
`ImageViewerControl.LineDirectionMode` expose the same Free / Horizontal / Vertical setting
using `Lan.Shapes.Enums.LineDirectionMode`. The default is Free. Custom line shapes can
implement `ILineDirectionConstraint` to supply the stationary endpoint for an active edit.

### Angle tool

Choose **Angle**, then click the first endpoint, the vertex, and the second endpoint.
The smaller angle (0–180°) appears beside the arc. Drag either endpoint or the vertex
to change the angle; drag a ray to move the whole annotation. Right-click during
creation to cancel it. `PointsData` stores the three points in click order.

### Ruler cross tool

Choose **RulerCross** in the sketch palette, then click the image to add horizontal and
vertical rulers at its center. Drag either ruler line or the intersection to move the
origin. The axes stay aligned with the image and span its width and height.
Tick values are relative to the intersection: zero at the origin, positive to the right
and down, and negative to the left and up. Labels use the same `Measurement` calibration
as the other measurement tools. Tick spacing adjusts with zoom, while tick length and
label size remain constant on screen. Add `"RulerCross"` to `AvailableGeometryTypes` in
hosts that restrict the palette. `RulerCrossData` saves the origin and image dimensions.

## Architecture

The project is a **Windows-only WPF** image viewer and geometry sketcher. Shapes render via `DrawingVisual` for performance. There is no non-WPF target.

### Modules

- **Lan.Shapes**: Core shape rendering, layers, stylers, handles, metadata contracts
- **Lan.SketchBoard**: Canvas host, shape repository, visual-collection mirror
- **Lan.ImageViewer**: Image zoom/pan control and viewer chrome
- **Lan.Shapes.Custom**: Extended shape implementations
- **Lan.Shapes.DialogGeometry**: Dialog-based geometry types (grid rectangles, DXF)
- **Lan.ImageViewer.Prism**: Prism composition root (DI module, default VM, registrations)

### Design docs

- [`docs/adr/0001-wpf-native-sketch-architecture.md`](docs/adr/0001-wpf-native-sketch-architecture.md) — target ownership model, lifecycle, scale policy
- [`docs/refactor-checklist.md`](docs/refactor-checklist.md) — phased refactor plan mapped to concrete files
- [`docs/architecture-issues.md`](docs/architecture-issues.md) — issue log (status table kept in sync with phases)

### Packaging note (fat packages)

`Lan.ImageViewer` and `Lan.ImageViewer.Prism` ship as **fat packages**:

- Project references use `PrivateAssets="All"` so NuGet restore does **not** emit separate dependency packages for core projects.
- `CopyProjectReferencesToPackage` embeds those project (and selected third-party) DLLs into the nupkg.

This is intentional for single-package host consumption. Do not drop `PrivateAssets` / the copy target without switching to multi-package dependency publishing.

## Adding a New Shape

The shape system is extensible. To add a new shape, follow these steps:

### Step 1: Create a Data Model (if needed)

Create a class that implements `IGeometryMetaData` to hold the shape's serializable state. Place it in `src/Lan.Shapes/Models/`.

```csharp
using System.Windows;
using Lan.Shapes.Interfaces;

namespace Lan.Shapes.Models
{
    public class MyShapeData : IGeometryMetaData
    {
        public Point Center { get; set; }
        public double Radius { get; set; }
        public double StrokeThickness { get; set; }
    }
}
```

Existing models you can reuse:
- `PointsData` — two or more `Point` values (used by `Rectangle`, `Line`, `Polygon`)
- `EllipseData` — `Center`, `RadiusX`, `RadiusY` (used by `Circle`, `Ellipse`)
- `CrossData` — `Center`, `Width`, `Height` (used by `Cross`)

### Step 2: Create the Shape Class

Create a class that inherits `ShapeVisualBase` and implements `IDataExport<T>`. Place it in `src/Lan.Shapes/Shapes/` (or `src/Lan.Shapes.Custom/` for extended shapes).

```csharp
using System.Windows;
using System.Windows.Media;
using Lan.Shapes.Interfaces;
using Lan.Shapes.Models;

namespace Lan.Shapes.Shapes
{
    public class MyShape : ShapeVisualBase, IDataExport<MyShapeData>
    {
        private readonly EllipseGeometry _ellipseGeometry = new EllipseGeometry();
        private Point _center;
        private double _radius;

        public MyShape(ShapeLayer layer) : base(layer)
        {
            RenderGeometryGroup.Children.Add(_ellipseGeometry);
        }

        public Point Center
        {
            get => _center;
            set
            {
                SetField(ref _center, value);
                UpdateGeometry();
            }
        }

        public double Radius
        {
            get => _radius;
            set
            {
                SetField(ref _radius, value);
                UpdateGeometry();
            }
        }

        public override Rect BoundsRect => RenderGeometryGroup.Bounds;

        private void UpdateGeometry()
        {
            _ellipseGeometry.Center = _center;
            _ellipseGeometry.RadiusX = _radius;
            _ellipseGeometry.RadiusY = _radius;
            UpdateVisual();
        }

        // ── Required abstract overrides ─────────────────────────────

        protected override void CreateHandles()
        {
            // Create drag handles for resizing the shape
        }

        protected override void HandleResizing(Point point)
        {
            // Handle drag-handle-based resizing logic
        }

        protected override void HandleTranslate(Point newPoint)
        {
            if (OldPointForTranslate.HasValue)
            {
                _center += newPoint - OldPointForTranslate.Value;
                OldPointForTranslate = newPoint;
                UpdateGeometry();
            }
        }

        public override void UpdateVisual()
        {
            if (ShapeStyler == null) return;

            var renderContext = RenderOpen();
            renderContext.DrawGeometry(ShapeStyler.FillColor, ShapeStyler.SketchPen, RenderGeometry);
            renderContext.Close();
        }

        // ── Mouse interaction ───────────────────────────────────────

        public override void OnMouseLeftButtonDown(Point mousePoint)
        {
            base.OnMouseLeftButtonDown(mousePoint);
            if (!IsGeometryRendered)
            {
                _center = mousePoint;
                _radius = 10;
                IsGeometryRendered = true;
                UpdateGeometry();
            }
            else
            {
                FindSelectedHandle(mousePoint);
            }
        }

        public override void OnMouseMove(Point point, MouseButtonState buttonState)
        {
            base.OnMouseMove(point, buttonState);
            if (buttonState == MouseButtonState.Pressed && !IsGeometryRendered)
            {
                _radius = GetDistanceBetweenTwoPoint(_center, point);
                UpdateGeometry();
            }
        }

        // ── Serialization ───────────────────────────────────────────

        public void FromData(MyShapeData data)
        {
            _center = data.Center;
            _radius = data.Radius;
            IsGeometryRendered = true;
            UpdateGeometry();
        }

        public MyShapeData GetMetaData()
        {
            return new MyShapeData
            {
                Center = _center,
                Radius = _radius,
                StrokeThickness = ShapeStyler?.SketchPen.Thickness ?? 1
            };
        }
    }
}
```

**Key members to implement:**

| Member | Purpose |
|---|---|
| `CreateHandles()` | Instantiate drag handles for corner/edge resizing |
| `HandleResizing(Point)` | Logic when a drag handle is moved |
| `HandleTranslate(Point)` | Logic when the shape body is dragged |
| `UpdateVisual()` | Render the shape via `DrawingContext` |
| `BoundsRect` | Return the bounding rectangle |
| `FromData(T)` | Deserialize and reconstruct the shape |
| `GetMetaData()` | Serialize shape state for persistence |

### Step 3: For Shapes with Adjustable Stroke Thickness

If your shape needs a user-adjustable stroke width (e.g., thickened lines), inherit `CustomGeometryBase` instead of `ShapeVisualBase`:

```csharp
using System.Windows;
using Lan.Shapes.Custom;

namespace Lan.Shapes.Custom
{
    public class ThickenedMyShape : CustomGeometryBase
    {
        public ThickenedMyShape(ShapeLayer layer) : base(layer) { }

        protected override void OnStrokeThicknessChanges(double strokeThickness)
        {
            // Update geometry based on new thickness
        }

        // Implement remaining abstract members...
    }
}
```

### Step 4: Register the Shape

Add the type to `GeometryTypeRegistration` catalog, then enable it in `LanShapesConfig.json`:

```json
"AvailableGeometryTypes": [ "Line", "MyShape" ]
```

Or register at the composition root / repository:

```csharp
// Preferred: GeometryTypeRegistration / host startup
geometryTypeManager.RegisterGeometryType<MyShape>();

// Or on the board repository
dataManager.RegisterDrawingTool("MyShape", typeof(MyShape));
dataManager.SetGeometryType(typeof(MyShape));
```


Palette icons: add a resource key in `Lan.ImageViewer/Geometries.xaml` (or implement `IGeometryIconProvider`). Do **not** hardcode icons in the VM.

### Step 5: Load Existing Shapes from Data

```csharp
var data = new MyShapeData { Center = new Point(100, 100), Radius = 50 };
dataManager.LoadShape<MyShape, MyShapeData>(data);
```

## Requirements

- .NET 8.0 Windows
- WPF
- Extended.Wpf.Toolkit (v4.5.1)

## License

See LICENSE.md for details.
