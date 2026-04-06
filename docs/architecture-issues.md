# Lan.Shapes — Architectural Issues

> **Assessed:** 2026-04-06  
> **Scope:** `Lan.Shapes` core library + direct consumers (`Lan.SketchBoard`, `Lan.ImageViewer`)  
> **Status legend:** 🔴 Critical · 🟠 Major · 🟡 Minor

---

## Summary

| # | Severity | Issue | Status |
|---|----------|-------|--------|
| 1 | 🔴 Critical | `ISketchBoardDataManager` leaks WPF types into the core library | ✅ **Fixed** (Step 1–4) |
| 2 | 🔴 Critical | `ShapeLayer` ↔ `ShapeVisualBase` circular ownership + dual render path | ⏳ Open |
| 3 | 🔴 Critical | Data Transfer Objects live inside the `Interfaces/` folder/namespace | ⏳ Open |
| 4 | 🟠 Major | `ISketchBoardDataManager` was a God Interface (ISP violation) | ✅ **Fixed** (Step 1–4) |
| 5 | 🟠 Major | Four interfaces/types are defined but never implemented or used | ⏳ Open |
| 6 | 🟠 Major | `ViewportScalingService` uses globally mutable state + unexplained magic formula | ⏳ Open |
| 7 | 🟡 Minor | `DragLocation.cs` placed in `Shapes/` instead of `Handle/` | ⏳ Open |
| 8 | 🟡 Minor | `BrushToHexConverter` and `AffineTransformationHelper` are root-level orphans | ⏳ Open |
| 9 | 🟡 Minor | `ShapeStylerFactory` is not behind an interface | ⏳ Open |
| 10 | 🔴 Critical | `OnSelected()`/`OnDeselected()` throw `NotImplementedException` in 4 subclasses | ⏳ Open |
| 11 | 🔴 Critical | `Cross.UpdateVisual()` double `RenderOpen()` wipes base drawing | ⏳ Open |
| 12 | 🔴 Critical | `Polygon.FromData()` empty loop — deserialization is broken | ⏳ Open |
| 13 | 🔴 Critical | `ShapeStylerFactory.DottedLineStyler()` corrupts `_selectedStyler` field | ⏳ Open |
| 14 | 🟠 Major | `ShapeLayer.GetStyler()` unguarded dictionary access | ⏳ Open |
| 15 | 🟠 Major | `IShapeRepository.NewShapeSketched` is `Action` instead of `event` | ⏳ Open |
| 16 | 🟠 Major | `Circle`/`Line` bypass `CreateFormattedText()` — duplicated text rendering | ⏳ Open |
| 17 | 🟡 Minor | Dead code: `Pointer.cs` entirely commented out, ~290 lines in `Cross.cs` | ⏳ Open |
| 18 | 🟡 Minor | `#region Propeties` typo in `Rectangle`, `Circle`, `Ellipse`, `Polygon` | ⏳ Open |
| 19 | 🟡 Minor | Duplicate `using System.Windows.Media` in `ShapeStylerParameter.cs` | ⏳ Open |
| 20 | 🟡 Minor | `PointExtension.MiddleWith` duplicates `ShapeVisualBase.GetMiddleToTwoPoints` | ⏳ Open |
| 21 | 🟡 Minor | `IGeometryMetaData` XML doc comment is cut off mid-sentence | ⏳ Open |

---

## 🔴 Issue 1 — WPF types leaked into the core library interface ✅ Fixed

### Problem

`ISketchBoardDataManager` lived in `Lan.Shapes` (the portable core) but exposed concrete WPF types:

```csharp
// Lan.Shapes/Interfaces/ISketchBoardDataManager.cs  ← WRONG LAYER
VisualCollection VisualCollection { get; }        // System.Windows.Media
void InitializeVisualCollection(Visual visual);   // System.Windows.Media
ISketchBoard SketchBoard { get; }                 // WPF control reference
```

`VisualCollection` requires a **live WPF dispatcher** to instantiate. Any code depending on
`ISketchBoardDataManager` — including ViewModels and services — was forced to bring in the
WPF rendering stack, making unit testing impossible without spinning up a `WPF Application`.

### Fix applied

The interface was split across the correct layers:

| Interface | Location | WPF types? |
|-----------|----------|-----------|
| `IShapeRepository` | `Lan.Shapes/Interfaces/` | ❌ None — fully portable |
| `IVisualHost` | `Lan.SketchBoard/` | ✅ Contains all WPF members |
| `ISketchBoardDataManager` | `Lan.Shapes/Interfaces/` | extends `IShapeRepository`, adds WPF subset for backward compat |

`SketchBoardDataManager` now implements `ISketchBoardDataManager`, `IVisualHost`, and `INotifyPropertyChanged`.

### Remaining migration (Step 5+)

ViewModel and service constructors should be updated to accept `IShapeRepository` instead of
`ISketchBoardDataManager` wherever WPF rendering access is not needed:

```csharp
// Before
public MyViewModel(ISketchBoardDataManager manager) { }

// After — testable, WPF-agnostic
public MyViewModel(IShapeRepository repository) { }
```

---

## 🔴 Issue 2 — Circular ownership between `ShapeLayer` and `ShapeVisualBase`

### Problem

Both types reference each other at the core layer:

```
ShapeVisualBase ──holds──► ShapeLayer          (shape.ShapeLayer property)
ShapeLayer      ──holds──► List<ShapeVisualBase> (_shapeVisuals field)
```

Additionally, `ShapeLayer.RenderShapes(List<ShapeVisualBase>)` calls `shape.RenderOpen()`
directly, duplicating the render path that already exists inside each shape's own `UpdateVisual()`.
This means the same shape can be rendered from two completely different entry points.

Both `ShapeLayer.RenderShapes()` and `ShapeLayer.AddShapeToLayer()` have **zero call sites**
in the entire codebase — they are dead code that makes the public API misleading.

### Recommended fix

1. Remove `_shapeVisuals` from `ShapeLayer`. Shape grouping belongs in the `IShapeRepository`,
   not in the styling descriptor.
2. Remove `RenderShapes()` and `AddShapeToLayer()` — they are dead and create a second render
   path that violates the shape's self-rendering contract.
3. `ShapeVisualBase.ShapeLayer` can remain as a reference (for styler lookup), but `ShapeLayer`
   must not own `ShapeVisualBase` instances.

```
After fix:
  ShapeVisualBase ──holds──► ShapeLayer   (to look up its current IShapeStyler)
  ShapeLayer                              (styling descriptor only — no shape ownership)
  IShapeRepository ─────────► manages all shape instances
```

---

## 🔴 Issue 3 — Concrete DTOs live in the `Interfaces/` namespace

### Problem

`Interfaces/` is a conventional signal for *abstractions only*. The following concrete data
classes are placed there, polluting the namespace contract:

| File | Type |
|------|------|
| `Interfaces/CrossData.cs` | Concrete DTO |
| `Interfaces/EllipseData.cs` | Concrete DTO |
| `Interfaces/PointsData.cs` | Concrete DTO |
| `Interfaces/TextGeometryData.cs` | Concrete DTO |

A developer browsing for abstractions will find concrete types mixed in with interfaces, making
the boundary undefined.

### Recommended fix

Move all DTOs to `Lan.Shapes/Models/` with namespace `Lan.Shapes.Models`:

```
Before:  Lan.Shapes.Interfaces.CrossData
After:   Lan.Shapes.Models.CrossData
```

---

## 🟠 Issue 4 — God Interface (ISP violation) ✅ Fixed

### Problem

The original `ISketchBoardDataManager` had **20+ members** across four unrelated concerns:
shape CRUD, selection state, geometry type registry, and WPF visual host wiring. Consumers
that only needed shape data had to take a dependency on the entire WPF rendering interface.

### Fix applied

See Issue 1. `IShapeRepository` now contains the data-management concerns only. Each consumer
can depend on the narrowest interface that satisfies its need.

---

## 🟠 Issue 5 — Defined but never adopted interfaces and types

### Problem

Four types are declared in `Lan.Shapes` with **zero implementations and zero usages**:

| Type | File | Problem |
|------|------|---------|
| `IShapeManipulator` / `IShapeManipulator<T>` | `Interfaces/IShapeManipulator.cs` | 0 implementations, 0 usages |
| `IShapeLayerManager` | `Interfaces/IShapeLayerManager.cs` | 0 usages inside `Lan.Shapes` |
| `ISketchBoardMouseHandler` | `Interfaces/ISketchBoardMouseHandler.cs` | 0 implementations anywhere |
| `ShapeStateMachine` (enum) | `Shapes/ShapeStateMachine.cs` | 0 usages, wrong folder |

These inflate the public API surface and create false signals about capabilities the library
provides. A consumer reading the headers would assume manipulation, layer management, and a
state machine are wired up — they are not.

### Recommended fix

- **`IShapeManipulator<T>`**: Implement it within each shape, or remove it and document the
  intent in a tracking issue.
- **`IShapeLayerManager`**: Move its implementation into `Lan.ImageViewer` where it is actually
  used (`GeometryTypeManager.cs`), or wire it up in `Lan.Shapes`.
- **`ISketchBoardMouseHandler`**: Implement on `SketchBoard` control and wire into `ISketchBoard`,
  or remove if the event-routing design has changed.
- **`ShapeStateMachine`**: Move to `Lan.Shapes/Enums/` and integrate into `ShapeVisualBase`'s
  state tracking, or remove.

---

## 🟠 Issue 6 — `ViewportScalingService` uses globally mutable state and an unexplained formula

### Problem

```csharp
// Lan.Shapes/Scaling/ViewportScalingService.cs
public static double BaseStrokeThickness { get; set; } = 1.0;   // ← global mutable
public static double BaseDragHandleSize  { get; set; } = 8.0;   // ← global mutable

public static double CalculateStrokeThicknessFromViewportSize(double w, double h)
{
    return Math.Pow(1.8, Math.Log2(w + h) - 10);  // ← magic formula, no rationale
}
```

**Global mutable state** (`static { get; set; }`) breaks any scenario where two `SketchBoard`
instances run simultaneously with different zoom levels, because changing the global base
for one board affects the other.

The `Math.Pow(1.8, Math.Log2(w + h) - 10)` formula is empirical and undocumented. Its range
and expected behaviour at edge values (very small or large viewports) is unknown.

### Recommended fix

1. Make `BaseStrokeThickness` and `BaseDragHandleSize` instance configuration passed in through
   a `ViewportScalingOptions` record, not static fields.
2. Document the scaling formula with its derivation, or replace it with an explicit piecewise
   function whose behaviour is auditable.

```csharp
// Suggested approach
public sealed class ViewportScalingOptions
{
    public double BaseStrokeThickness { get; init; } = 1.0;
    public double BaseDragHandleSize  { get; init; } = 8.0;
}

public readonly struct ViewportScalingService(ViewportScalingOptions options)
{
    public double CalculateStrokeThickness(double scale) =>
        options.BaseStrokeThickness / Math.Max(scale, double.Epsilon);
}
```

---

## 🟡 Issue 7 — `DragLocation.cs` in wrong folder

### Problem

`Lan.Shapes/Shapes/DragLocation.cs` defines the `DragLocation` enum. This enum exists
exclusively to describe *which edge or corner of a drag handle is being dragged*. It drives
cursor logic in `ShapeVisualBase` and is a parameter to `RectDragHandle.CreateRectDragHandleFromStyler`.
It is a handle concept, not a shape concept.

### Recommended fix

Move to `Lan.Shapes/Handle/DragLocation.cs` with no other changes.

---

## 🟡 Issue 8 — Root-level utility files with no folder grouping

### Problem

Two files sit at the `Lan.Shapes` project root with no folder:

| File | Purpose |
|------|---------|
| `BrushToHexConverter.cs` | `Brush` → hex string conversion |
| `AffineTransformationHelper.cs` | 2-D affine math helpers |

At the root they have no namespace grouping signal and are harder to discover.

### Recommended fix

```
Move to:
  Lan.Shapes/Converters/BrushToHexConverter.cs         → namespace Lan.Shapes.Converters
  Lan.Shapes/Utilities/AffineTransformationHelper.cs   → namespace Lan.Shapes.Utilities
```

---

## 🟡 Issue 9 — `ShapeStylerFactory` is not behind an interface

### Problem

`SketchBoardDataManager` creates `ShapeStylerFactory` directly:

```csharp
private readonly ShapeStylerFactory _shapeStylerFactory = new ShapeStylerFactory();
```

There is no `IShapeStylerFactory`. This makes it impossible to substitute a different styler
factory (e.g. for testing, theming, or a different styling strategy) without modifying
`SketchBoardDataManager`.

### Recommended fix

```csharp
// New interface in Lan.Shapes/Styler/
public interface IShapeStylerFactory
{
    IShapeStyler CreateStyler(ShapeStylerParameter parameter);
}

// Inject via constructor
public class SketchBoardDataManager(IShapeStylerFactory stylerFactory, ...)
```

---

## 🔴 Issue 10 — `OnSelected()`/`OnDeselected()` throw `NotImplementedException`

### Problem

These methods are declared `abstract` in `ShapeVisualBase`, forcing every subclass to implement
them. However, four out of six shapes throw `NotImplementedException`:

| Shape | `OnSelected()` | `OnDeselected()` |
|-------|-----------------|-------------------|
| `Rectangle` | ❌ throws | ❌ throws |
| `Circle` | ❌ throws | ❌ throws |
| `Ellipse` | ❌ throws | ❌ throws |
| `Polygon` | ❌ throws | ❌ throws |
| `Line` | ✅ empty | ✅ empty |
| `Cross` | ✅ empty | ✅ empty |

If any caller invokes these polymorphically via `ShapeVisualBase` (e.g. during selection
change in `SketchBoardDataManager`), the application crashes at runtime.

### Recommended fix

Change both methods from `abstract` to `virtual` with empty default bodies in `ShapeVisualBase`:

```csharp
// Before
public abstract void OnSelected();
public abstract void OnDeselected();

// After
public virtual void OnSelected() { }
public virtual void OnDeselected() { }
```

Remove all `throw new NotImplementedException()` overrides in subclasses.

---

## 🔴 Issue 11 — `Cross.UpdateVisual()` double `RenderOpen()` wipes base drawing

### Problem

```csharp
// Cross.cs
public override void UpdateVisual()
{
    base.UpdateVisual();           // ← opens RenderOpen(), draws geometry+text, closes

    var renderContext = RenderOpen();  // ← opens AGAIN, wipes everything base drew
    if (ShapeStyler != null)
    {
        renderContext.DrawGeometry(ShapeStyler.FillColor, ShapeStyler.SketchPen, _verticalLine);
        renderContext.DrawGeometry(ShapeStyler.FillColor, ShapeStyler.SketchPen, _horizontalLine);
    }
    renderContext.Close();
}
```

`DrawingVisual.RenderOpen()` replaces the entire visual content. The `base.UpdateVisual()` call
renders geometry and text, then the second `RenderOpen()` immediately erases it all. Only the
cross lines survive. The base call is wasted work and any text/geometry from the base is lost.

### Recommended fix

Remove the `base.UpdateVisual()` call and render everything in a single `RenderOpen()` context,
or override to build a complete render in one pass.

---

## 🔴 Issue 12 — `Polygon.FromData()` empty loop — deserialization is broken

### Problem

```csharp
// Polygon.cs
public void FromData(PointsData data)
{
    foreach (var point in data.DataPoints)
    {
        // empty — no points are added
    }
}
```

Loading a polygon from serialized data does nothing. The polygon will have zero vertices
after a load/deserialize cycle, silently losing all data.

### Recommended fix

Call `CreateNewGeometryAndRenderIt(point)` for each point in the loop body, matching the
logic used in `OnMouseLeftButtonDown`.

---

## 🔴 Issue 13 — `ShapeStylerFactory.DottedLineStyler()` corrupts `_selectedStyler`

### Problem

```csharp
// ShapeStylerFactory.cs
public IShapeStyler DottedLineStyler()
{
    if (_selectedStyler == null)        // ← checks the WRONG field
    {
        _selectedStyler = new ShapeStyler();   // ← overwrites the WRONG field
        _selectedStyler.SetStrokeColor(Brushes.Green);
        _selectedStyler.SetPenDashStyle(DashStyles.Dash);
        ...
    }
    return _selectedStyler;
}
```

`DottedLineStyler()` reads and writes `_selectedStyler` instead of a dedicated
`_dottedLineStyler` field. This creates two bugs:

1. If `ShapeSelectedVisualState()` was called first → `DottedLineStyler()` returns the
   red selected styler instead of a green dotted one.
2. If `DottedLineStyler()` was called first → `ShapeSelectedVisualState()` returns the
   green dotted styler instead of the red selected one.

### Recommended fix

Add a `private IShapeStyler _dottedLineStyler;` field and use it in `DottedLineStyler()`.

---

## 🟠 Issue 14 — `ShapeLayer.GetStyler()` unguarded dictionary access

### Problem

```csharp
// ShapeLayer.cs
public IShapeStyler GetStyler(ShapeVisualState shapeState) => _stylers[shapeState];
```

If a `ShapeLayerParameter` configuration omits a state entry (e.g. `MouseOver` or `Locked`),
any shape transitioning to that state throws `KeyNotFoundException` at runtime with no
meaningful error message.

### Recommended fix

Either validate completeness at construction time (fail-fast), or use `TryGetValue` with a
fallback to the `Normal` styler:

```csharp
public IShapeStyler GetStyler(ShapeVisualState shapeState) =>
    _stylers.TryGetValue(shapeState, out var styler) ? styler : _stylers[ShapeVisualState.Normal];
```

---

## 🟠 Issue 15 — `NewShapeSketched` is `Action` instead of `event`

### Problem

```csharp
// IShapeRepository.cs
Action<ShapeVisualBase>? NewShapeSketched { get; set; }
```

All other notification members on `IShapeRepository` use `event EventHandler<T>`.
An `Action` property allows only **one subscriber** — assigning a second handler silently
replaces the first. This is inconsistent and error-prone.

### Recommended fix

```csharp
// Before
Action<ShapeVisualBase>? NewShapeSketched { get; set; }

// After
event EventHandler<ShapeVisualBase> NewShapeSketched;
```

---

## 🟠 Issue 16 — `Circle`/`Line` bypass `CreateFormattedText()` helper

### Problem

`Circle.AddRadiusText()` and `Line.DrawLengthText()` construct `FormattedText` manually
with hardcoded culture, font family, DPI, and brush values — duplicating the exact logic
that `ShapeVisualBase.CreateFormattedText()` now encapsulates.

```csharp
// Circle.cs — AddRadiusText()
var formattedText = new FormattedText(
    $"{lengthInMm:f4} ...",
    CultureInfo.GetCultureInfo("en-us"),   // ← hardcoded
    FlowDirection.LeftToRight,
    new Typeface("Verdana"),               // ← hardcoded
    ShapeLayer.TagFontSize,
    Brushes.Red,                           // ← hardcoded
    96);                                   // ← hardcoded
```

If default font, culture, or DPI constants change in the base class, these methods won't
pick up the change.

### Recommended fix

Replace the manual `FormattedText` construction with calls to `CreateFormattedText()`.

---

## 🟡 Issue 17 — Dead code files

### Problem

| File | Lines | Content |
|------|-------|---------|
| `Shapes/Pointer.cs` | 60 | Entire class is commented out |
| `Shapes/Cross.cs` lines 16–305 | ~290 | Old `Cross` implementation, fully commented out |

Version control preserves history. Commented-out code adds noise, inflates search results,
and creates confusion about what is active.

### Recommended fix

Delete `Pointer.cs` entirely. Remove the commented-out block in `Cross.cs`.

---

## 🟡 Issue 18 — `#region Propeties` typo in four subclasses

### Problem

The same `Propeties` → `Properties` typo that was fixed in `ShapeVisualBase` still exists in:

- `Shapes/Rectangle.cs` line 32
- `Shapes/Circle.cs` line 64
- `Shapes/Ellipse.cs` line 31
- `Shapes/Polygon.cs` line 35

### Recommended fix

Find-and-replace `#region Propeties` → `#region Properties` across all files.

---

## 🟡 Issue 19 — Duplicate `using` in `ShapeStylerParameter.cs`

### Problem

```csharp
// ShapeStylerParameter.cs
using System.Windows.Media;
using System.Windows.Media;   // ← duplicate
```

### Recommended fix

Remove the duplicate line.

---

## 🟡 Issue 20 — `PointExtension.MiddleWith` duplicates `ShapeVisualBase.GetMiddleToTwoPoints`

### Problem

```csharp
// PointExtension.cs
public static Point MiddleWith(this Point pointStart, Point pointEnd)
{
    return new Point((pointStart.X + pointEnd.X) / 2, (pointStart.Y + pointEnd.Y) / 2);
}

// ShapeVisualBase.cs
protected static Point GetMiddleToTwoPoints(Point p1, Point p2)
{
    return new Point((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
}
```

Identical logic in two places. `MiddleWith` is used by `Rectangle`, `GetMiddleToTwoPoints`
is defined in the base class. Keeping both invites divergence.

### Recommended fix

Consolidate on the extension method `MiddleWith` (more discoverable and idiomatic C#).
Remove `GetMiddleToTwoPoints` from `ShapeVisualBase` and update any callers.

---

## 🟡 Issue 21 — `IGeometryMetaData` XML doc is incomplete

### Problem

```csharp
// IGeometryMetaData.cs
/// <summary>
/// it is used to exchange data with 
/// </summary>
```

The sentence is cut off mid-phrase. The intent of the interface is unclear to consumers.

### Recommended fix

Complete the documentation, e.g.:

```csharp
/// <summary>
/// Marker interface for geometry metadata DTOs used to serialize and deserialize shape data.
/// </summary>
```

---

## Dependency flow — before vs. after Issue 1 fix

```
BEFORE (Issue 1 unfixed)
─────────────────────────────────────────────────────
Lan.Shapes          ──defines──► ISketchBoardDataManager
                                      │
                                      └──► VisualCollection  (WPF)
                                      └──► Visual            (WPF)

Lan.SketchBoard     ──references──► Lan.Shapes
Lan.ImageViewer     ──references──► Lan.Shapes
Lan.ImageViewer.Prism──references──► Lan.Shapes
  (all three drag in WPF through the core interface)


AFTER (Issue 1 fixed)
─────────────────────────────────────────────────────
Lan.Shapes          ──defines──► IShapeRepository     (zero WPF types)
                    ──defines──► ISketchBoardDataManager extends IShapeRepository
                                      └── still has WPF members for compat

Lan.SketchBoard     ──defines──► IVisualHost          (contains VisualCollection etc.)
                    ──implements──► SketchBoardDataManager : ISketchBoardDataManager, IVisualHost

ViewModels / Services ──can now depend on──► IShapeRepository only (testable)
```
