# WpfGeometrySketcher

A high-performance WPF image viewer and geometry sketching control. Built on `DrawingVisual` for superior rendering performance compared to standard WPF shape controls, with extensible support for custom shapes.

## Features

- **Performance**: Built on `DrawingVisual` for optimized rendering
- **Shape Support**: Rectangle, ellipse, line, polygon, circle, and cross shapes
- **Custom Shapes**: Extensible architecture for custom geometry types
- **Zoom & Pan**: Mouse wheel zoom and CTRL+drag panning
- **Pixel Info**: Display RGB values at cursor position
- **Scale Display**: Real-time zoom ratio display
- **Auto-Sized Handles**: Drag handles automatically sized based on shape dimensions
- **Dialog Integration**: Grid rectangles with interactive row/column input
- **DXF Export**: Export shapes to DXF format

## Getting Started

### Basic Usage

1. Add the control to your XAML:

```xml
<imageViewer:ImageViewerControl
    Margin="5"
    Padding="10"
    BorderBrush="Red"
    DataContext="{Binding Camera2}"
    BorderThickness="1" />
```

2. Define the view model in your code:

```csharp
// Define image control viewmodel
public IImageViewerViewModel Camera1 { get; set; }

// Instantiation
Camera1 = new ImageViewerControlViewModel();
```

### Navigation Controls

- **Zoom**: Use mouse wheel to zoom in/out
- **Pan**: Press CTRL + Left mouse button and drag to move the sketch area

## Architecture

The project is organized into several core modules:

- **Lan.ImageViewer**: Main image viewer control and view models
- **Lan.Shapes**: Core shape rendering and manipulation
- **Lan.SketchBoard**: Canvas and drawing infrastructure
- **Lan.Shapes.Custom**: Custom shape implementations
- **Lan.Shapes.DialogGeometry**: Dialog-based geometry types (grid rectangles, DXF export)
- **Lan.ImageViewer.Prism**: Prism framework integration

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

Register your shape with the `SketchBoardDataManager` so it can be instantiated by the drawing tools:

```csharp
// Assuming you have access to the SketchBoardDataManager instance
dataManager.RegisterDrawingTool("MyShape", typeof(MyShape));

// Select it for drawing
dataManager.SetGeometryType(typeof(MyShape));

// Or select by name
dataManager.SetGeometryType("MyShape");
```

If using the `ImageViewerControlViewModel`, register tools during initialization:

```csharp
var viewModel = new ImageViewerControlViewModel();
viewModel.SketchBoardDataManager.RegisterDrawingTool("MyShape", typeof(MyShape));
```

### Step 5: Load Existing Shapes from Data

To deserialize and display a previously saved shape:

```csharp
var data = new MyShapeData { Center = new Point(100, 100), Radius = 50 };
dataManager.LoadShape<MyShape, MyShapeData>(data);
```

## Requirements

- .NET 6.0 Windows
- WPF
- Extended.Wpf.Toolkit (v4.5.1)

## License

See LICENSE.md for details.