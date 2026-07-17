# Lan.Shapes — Architectural Issues

> **Assessed:** 2026-04-06  
> **Scope:** `Lan.Shapes` core library + direct consumers (`Lan.SketchBoard`, `Lan.ImageViewer`)  
> **Status legend:** 🔴 Critical · 🟠 Major · 🟡 Minor

> **Target architecture:** [`docs/adr/0001-wpf-native-sketch-architecture.md`](adr/0001-wpf-native-sketch-architecture.md)  
> **Execution plan:** [`docs/refactor-checklist.md`](refactor-checklist.md)  
> Re-verify row statuses against code before starting work — several items below have been fixed since this log was written.

---

## Summary

| # | Severity | Issue | Status |
|---|----------|-------|--------|
| 1 | 🔴 Critical | `ISketchBoardDataManager` mixed shape data with visual-host concerns | ✅ **Fixed** (Step 1–4) |
| 2 | 🔴 Critical | `ShapeLayer` ↔ `ShapeVisualBase` circular ownership + dual render path | ✅ **Fixed** (Phase 3) |
| 3 | 🔴 Critical | Data Transfer Objects live inside the `Interfaces/` folder/namespace | ✅ **Fixed** (earlier — Models/) |
| 4 | 🟠 Major | `ISketchBoardDataManager` was a God Interface (ISP violation) | ✅ **Fixed** (Step 1–4) |
| 5 | 🟠 Major | Four interfaces/types are defined but never implemented or used | ✅ **Fixed** (Phase 3) |
| 6 | 🟠 Major | `ViewportScalingService` uses globally mutable state + unexplained magic formula | ✅ **Fixed** (Phase 2) |
| 7 | 🟡 Minor | `DragLocation.cs` placed in `Shapes/` instead of `Handle/` | ✅ **Fixed** (already in Handle/) |
| 8 | 🟡 Minor | `BrushToHexConverter` and `AffineTransformationHelper` are root-level orphans | ✅ **Fixed** (Converters/Utilities) |
| 9 | 🟡 Minor | `ShapeStylerFactory` is not behind an interface | ✅ **Fixed** (Phase 5) |
| 10 | 🔴 Critical | `OnSelected()`/`OnDeselected()` throw `NotImplementedException` in 4 subclasses | ✅ **Fixed** (Phase 1) |
| 11 | 🔴 Critical | `Cross.UpdateVisual()` double `RenderOpen()` wipes base drawing | ✅ **Fixed** (Phase 1) |
| 12 | 🔴 Critical | `Polygon.FromData()` empty loop — deserialization is broken | ✅ **Fixed** (earlier + Phase 1 tests) |
| 13 | 🔴 Critical | `ShapeStylerFactory.DottedLineStyler()` corrupts `_selectedStyler` field | ✅ **Fixed** (Phase 1) |
| 14 | 🟠 Major | `ShapeLayer.GetStyler()` unguarded dictionary access | ✅ **Fixed** (fallback to Normal) |
| 15 | 🟠 Major | `IShapeRepository.NewShapeSketched` is `Action` instead of `event` | ✅ **Fixed** (earlier) |
| 16 | 🟠 Major | `Circle`/`Line` bypass `CreateFormattedText()` — duplicated text rendering | ✅ **Fixed** (already use helper) |
| 17 | 🟡 Minor | Dead code: `Pointer.cs` / commented `Cross` / StrokeWidened stubs | ✅ **Fixed** (Phase 6) |
| 18 | 🟡 Minor | `#region Propeties` typo across shapes/controls | ✅ **Fixed** (Phase 6) |
| 19 | 🟡 Minor | Duplicate `using System.Windows.Media` in `ShapeStylerParameter.cs` | ✅ **Fixed** (already clean) |
| 20 | 🟡 Minor | `PointExtension.MiddleWith` duplicates `ShapeVisualBase.GetMiddleToTwoPoints` | ✅ **Fixed** (base helper removed) |
| 21 | 🟡 Minor | `IGeometryMetaData` XML doc comment is cut off mid-sentence | ✅ **Fixed** (already complete) |

---

## 🔴 Issue 1 — Shape data mixed with visual-host concerns ✅ Fixed

### Problem

`ISketchBoardDataManager` exposed both shape CRUD/selection and live visual-host members:

```csharp
// Lan.Shapes/Interfaces/ISketchBoardDataManager.cs
VisualCollection VisualCollection { get; }        // System.Windows.Media
void InitializeVisualCollection(Visual visual);   // System.Windows.Media
ISketchBoard SketchBoard { get; }                 // WPF control reference
```

Any consumer that only needed shape state — ViewModels, services, tests — still had to
take the full visual-host surface. `VisualCollection` also requires a live WPF visual
parent, so tests that only exercised data logic were forced through host initialization.

This project is **WPF-only**; the issue was interface segregation inside the WPF stack,
not portability to other UI frameworks.

### Fix applied

The interface was split by concern:

| Interface | Location | Responsibility |
|-----------|----------|----------------|
| `IShapeRepository` | `Lan.Shapes/Interfaces/` | Shape collections, selection, tools, CRUD, events |
| `IVisualHost` | `Lan.SketchBoard/` | `VisualCollection`, board attachment, scale feedback |
| `ISketchBoardDataManager` | `Lan.Shapes/Interfaces/` | Combined data + visual-host surface for WPF controls |

`SketchBoardDataManager` now implements `ISketchBoardDataManager`, `IVisualHost`, and `INotifyPropertyChanged`.

### Remaining migration (Step 5 / Phase 4) ✅ Done

ViewModel surface now segregates concerns:

| Member | Role |
|--------|------|
| `SketchBoardDataManager` | Control DP / visual host only |
| `ShapeRepository` | Shape collections, selection, CRUD, events |
| `Shapes` / `SelectedShape` | XAML list bind (maps to `SelectedGeometry`) |

`DeleteShapeCommand` removes `SelectedShape`, not `CurrentGeometryInEdit`.
DI registers both `ISketchBoardDataManager` and `IShapeRepository` (same instance).

---

## 🔴 Issue 2 — Circular ownership between `ShapeLayer` and `ShapeVisualBase` ✅ Fixed (Phase 3)

### Problem

Historically both types referenced each other, and `ShapeLayer` owned a shape list plus
dead `RenderShapes` / `AddShapeToLayer` helpers that duplicated shape self-rendering.

### Fix applied (Phase 3)

1. `ShapeLayer` is a **style + units profile only** — no shape collection, no render helpers.
2. Shape ownership remains on `IShapeRepository` / `SketchBoardDataManager`.
3. `ShapeVisualBase.ShapeLayer` stays for styler lookup only (one-way reference).
4. Layer construction and `ShapeLayerManager.ReadShapeLayers` fail-fast when `StyleSchema`
   lacks required `Normal` / `Selected` states (`EnsureRequiredStylerStates`).
5. `GetStyler` keeps fallback to `Normal` for optional states (`MouseOver`, `Locked`).

```
After fix:
  ShapeVisualBase ──holds──► ShapeLayer   (styler lookup only)
  ShapeLayer                              (style/units profile — no shape ownership)
  IShapeRepository ─────────► manages all shape instances
```

---

## 🔴 Issue 3 — Concrete DTOs live in the `Interfaces/` namespace ✅ Fixed (Models/)

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

### Fix applied

DTOs live under `src/Lan.Shapes/Models/` (`CrossData`, `EllipseData`, `PointsData`, `TextGeometryData`).

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

## 🟠 Issue 5 — Defined but never adopted interfaces and types ✅ Fixed (Phase 3)

### Problem

Several types inflated the public API with zero implementations/usages.

### Fix applied (Phase 3)

| Type | Action |
|------|--------|
| `IShapeManipulator` / `IShapeManipulator<T>` | **Deleted** |
| `ISketchBoardMouseHandler` | **Deleted**; `ISketchBoard` is a marker only |
| `ShapeStateMachine` | **Deleted** (0 usages) |
| `IShapeLayerManager` | **Kept** — implemented by `ShapeLayerManager` in Prism host |

---

## 🟠 Issue 6 — `ViewportScalingService` uses globally mutable state and an unexplained formula ✅ Fixed (Phase 2)

### Problem

```csharp
// Historical (pre-Phase 2)
public static double BaseStrokeThickness { get; set; } = 1.0;   // ← global mutable
public static double BaseDragHandleSize  { get; set; } = 8.0;   // ← global mutable

public static double CalculateStrokeThicknessFromViewportSize(double w, double h)
{
    return Math.Pow(1.8, Math.Log2(w + h) - 10);  // ← magic formula, no rationale
}
```

**Global mutable state** broke concurrent `SketchBoard` instances. A second dual path
(`SketchBoard_SizeChanged` → viewport-size formula) also fought zoom-driven thickness.

### Fix applied (Phase 2)

1. Static bases are **readonly process-wide defaults** only.
2. Per-board bases live in `ViewportScalingOptions`, injected via
   `SketchBoardDataManager(ViewportScalingOptions)` and used by
   `OnImageViewerPropertyChanged` → `CalculateStrokeThickness(scale, options)`.
3. Live driver is **`LocalScale` only**. `SketchBoard_SizeChanged` styler mutation removed.
4. Viewport-size formula kept as **seed-only** (`[Obsolete]` + documented derivation);
   not used after the board is attached.
5. Existing shapes refresh via `ShapeVisualBase.RefreshScaleDependentVisuals()`.
6. Viewer chrome (crosshair) uses the same `base / scale` formula on fit and wheel zoom.
7. `ImageViewer` unsubscribes/resubscribes on manager DP rebind to avoid stacked handlers.

```csharp
// Live path
manager.OnImageViewerPropertyChanged(localScale);
// → stylers: thickness = options.BaseStrokeThickness / max(scale, 1)
// → shapes.RefreshScaleDependentVisuals()
```

---

## 🟡 Issue 7 — `DragLocation.cs` in wrong folder ✅ Fixed

### Fix applied

Lives at `src/Lan.Shapes/Handle/DragLocation.cs` (`namespace Lan.Shapes.Handle`).

---

## 🟡 Issue 8 — Root-level utility files with no folder grouping ✅ Fixed

### Fix applied

| File | Location |
|------|----------|
| `BrushToHexConverter.cs` | `src/Lan.Shapes/Converters/` (`Lan.Shapes.Converters`) |
| `AffineTransformationHelper.cs` | `src/Lan.Shapes/Utilities/` (`Lan.Shapes.Utilities`) |

---

## 🟡 Issue 9 — `ShapeStylerFactory` is not behind an interface ✅ Fixed (Phase 5)

### Problem

Historically `SketchBoardDataManager` / layer construction newed `ShapeStylerFactory`
directly, so tests and themes could not substitute a factory.

### Fix applied (Phase 5)

1. `IShapeStylerFactory` lives under `Lan.Shapes/Styler/` with
   `CreateStyler(ShapeStylerParameter)` plus the preset styler methods.
2. `ShapeLayer` dual ctor: default factory, or inject `IShapeStylerFactory`.
3. `ShapeLayerManager` injects the factory and passes it into every
   `new ShapeLayer(parameter, factory)` during `ReadShapeLayers`.
4. Composition roots register
   `IShapeStylerFactory` → `ShapeStylerFactory` (Prism module + TestApp MSDI).
5. Palette icons follow the same composition pattern via
   `IGeometryIconProvider` / `ResourceDictionaryGeometryIconProvider`.

```csharp
// Layer construction
public ShapeLayer(ShapeLayerParameter p, IShapeStylerFactory stylerFactory) { ... }

// DI
containerRegistry.RegisterSingleton<IShapeStylerFactory, ShapeStylerFactory>();
```
---

> **Status (2026-07-17):** ✅ Fixed in Phase 1. Base hooks are empty `virtual`s; `CustomGeometryBase` / `DxfGeometry` no-ops; selection covered by `ShapeLifecycleTests`.

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

> **Status (2026-07-17):** ✅ Fixed. `Cross.UpdateVisual` uses a single `RenderOpen()` pass.

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

> **Status (2026-07-17):** ✅ Fixed earlier; Phase 1 added `LoadShape_RoundTrips_Polygon` regression.

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

> **Status (2026-07-17):** ✅ Fixed. Dedicated `_dottedLineStyler` field; isolation test added.

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

> **Status:** ✅ Fixed earlier — `TryGetValue` with fallback to `Normal`.

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

> **Status:** ✅ Fixed earlier — `NewShapeSketched` is an `event`.

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

## 🟠 Issue 16 — `Circle`/`Line` bypass `CreateFormattedText()` helper ✅ Fixed

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

### Fix applied

`Circle.AddRadiusText` and `Line.DrawLengthText` already call `CreateFormattedText(...)`.
No further change required.

---

## 🟡 Issue 17 — Dead code files ✅ Fixed (Phase 6)

### Problem

| File | Lines | Content |
|------|-------|---------|
| `Shapes/Pointer.cs` | 60 | Entire class is commented out |
| `Shapes/Cross.cs` lines 16–305 | ~290 | Old `Cross` implementation, fully commented out |

Version control preserves history. Commented-out code adds noise, inflates search results,
and creates confusion about what is active.

### Recommended fix

Delete `Pointer.cs` entirely. Remove the commented-out block in `Cross.cs`.

### Fix applied (Phase 6)

- `Pointer.cs` already absent from tree.
- `Cross.cs` is a short active implementation (no commented legacy block).
- Deleted fully commented stubs: `StrokeWidenedCircle.cs`, `StrokeWidenedCross.cs`, `StrokeWidenedRectangle.cs`.

---

## 🟡 Issue 18 — `#region Propeties` typo ✅ Fixed (Phase 6)

### Problem

The same `Propeties` → `Properties` typo that was fixed in `ShapeVisualBase` still exists in:

- `Shapes/Rectangle.cs` line 32
- `Shapes/Circle.cs` line 64
- `Shapes/Ellipse.cs` line 31
- `Shapes/Polygon.cs` line 35

### Recommended fix

Find-and-replace `#region Propeties` → `#region Properties` across all files.

### Fix applied (Phase 6)

Replaced all remaining `#region Propeties` with `#region Properties` under `src/`
(controls, custom shapes, dialog geometry, sketch board). Core shape files already used `Properties`.

---

## 🟡 Issue 19 — Duplicate `using` in `ShapeStylerParameter.cs` ✅ Fixed

### Problem

```csharp
// ShapeStylerParameter.cs
using System.Windows.Media;
using System.Windows.Media;   // ← duplicate
```

### Recommended fix

Remove the duplicate line.

### Fix applied

`ShapeStylerParameter.cs` already has a single `using System.Windows.Media;` line.

---

## 🟡 Issue 20 — `PointExtension.MiddleWith` duplicates base helper ✅ Fixed

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

### Fix applied

`GetMiddleToTwoPoints` is gone from `ShapeVisualBase`. Call sites use `PointExtension.MiddleWith`.

---

## 🟡 Issue 21 — `IGeometryMetaData` XML doc is incomplete ✅ Fixed

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

### Fix applied

XML summary documents the marker-interface role for serialize/deserialize DTOs.

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
