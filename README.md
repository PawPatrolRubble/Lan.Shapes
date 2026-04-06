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

## Requirements

- .NET 6.0 Windows
- WPF
- Extended.Wpf.Toolkit (v4.5.1)

## License

See LICENSE.md for details.