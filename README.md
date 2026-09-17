# WpfGeometrySketcher

A high-performance WPF image viewer and geometry sketching control. Built on `DrawingVisual` for superior rendering performance compared to standard WPF shape controls, with extensible support for custom shapes.

## Features

- **Performance**: Built on `DrawingVisual` for optimized rendering
- **Shape Support**: Rectangle, ellipse, line, polygon, circle, cross, and ruler cross shapes
- **Custom Shapes**: Extensible architecture for custom geometry types
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
crosshair overlay, measurement calibration, and per-layer styling all live in that dedicated
file. A missing `AvailableGeometryTypes` value registers the full catalog; `[]` registers none.
Unknown names fail at startup. A missing `ShowCrossLine` value defaults to true.

```json
{
  "AvailableGeometryTypes": [ "Line", "Rectangle", "Rectangle2", "Circle", "Cross", "RulerCross", "DxfGeometry" ],
  "ShowCrossLine": true,
  "Measurement": {
    "PixelPerUnit": 3410,
    "UnitsPerMillimeter": 1000,
    "UnitName": "um"
  },
  "ShapeLayers": []
}
```

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
- **Pan**: Hold the middle mouse button (press the scroll wheel) and drag to move the sketch area. CTRL + Left mouse button and drag also pans.
- **Select overlapping geometry**: Click its outline; outlines and active resize handles take priority over shape interiors. Hold ALT and click repeatedly at the same point to cycle through overlapping unlocked geometries from topmost to bottommost. ALT + click selects without dragging or toggling locks. You can also select a geometry directly in the shape list.
- **Lock / unlock**: Double-click a completed geometry to lock it; its text turns gray. Double-click it again to unlock it and restore the usual text color.
- **Drawing**: With a drawing tool active, clicks create or continue the new geometry, including over existing shapes. Selection and double-click locking resume when drawing finishes; right-click ends the active sketch.
- **Line direction**: Choose Free, Horizontal, or Vertical in the toolbar's Line selector. In Free mode, hold SHIFT while drawing or resizing a line endpoint to use the nearest horizontal or vertical axis; release SHIFT to return to free drawing. Fixed Horizontal and Vertical modes persist across sketches. This also works for thickened and arrowed lines. The opposite endpoint stays fixed when resizing, and snapping only accepts anchors on the constrained axis.
- **Snapping**: While drawing or dragging a resize handle, points snap to nearby rectangle corners (including rotated rectangles), line endpoints and midpoints, and circle centers and quadrant points (top, right, bottom, and left), including locked shapes. A blue marker shows the target; the default distance is 8 screen pixels at every zoom level. Thickened rectangles, thickened lines, arrowed lines, fixed-center circles, and thickened circles also provide anchors. Thickened circle anchors follow the circle's centerline. The geometry being edited is excluded from snap targets.

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
