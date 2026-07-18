# Refactor checklist — WPF-native sketch architecture

- **ADR:** [`docs/adr/0001-wpf-native-sketch-architecture.md`](adr/0001-wpf-native-sketch-architecture.md)
- **Issue log:** [`docs/architecture-issues.md`](architecture-issues.md)
- **Rule:** finish a phase’s acceptance checks before starting the next. Prefer small PRs per phase.

Status legend: `[ ]` todo · `[~]` partial / started · `[x]` done

> Re-verify against code when starting a phase. Some items in `architecture-issues.md` are already fixed in tree (e.g. `Polygon.FromData`, `NewShapeSketched` as `event`, `DragLocation` under `Handle/`, `ViewportScalingOptions` introduced).

---

## Phase 0 — Baseline (no behavior change)

Goal: know what “good” looks like before refactors.

- [x] Run `Lan.SketchBoard.Tests` and record pass/fail
- [~] Smoke in `Lan.Shapes.SimpleApp` or `Lan.Shapes.TestApp` (manual — deferred; unit coverage added instead)
  - [ ] draw rectangle / circle / line / polygon
  - [ ] select, resize via handles, translate
  - [ ] delete (keyboard + command)
  - [ ] zoom in/out; confirm stroke/handle size
  - [ ] two viewers if multi-camera sample is available
- [x] Note current broken behaviors (do not “fix” them by widening scope mid-phase)

**Acceptance:** short baseline note in the PR description (commands run + smoke results).

**Baseline (2026-07-17):** `dotnet test Test/Lan.SketchBoard.Tests` → **21/21 passed** after Phase 1 fixes.

---

## Phase 1 — Correctness / lifecycle invariants (P0)

Goal: create / load / render / select share one pipeline. No dual-draw, no silent load loss.

### 1.1 Shared geometry builders

| Shape | Files | Check |
|---|---|---|
| `Polygon` | `src/Lan.Shapes/Shapes/Polygon.cs` | `[x]` `FromData` calls `CreateNewGeometryAndRenderIt` + regression test |
| `Rectangle` | `Rectangle.cs` | `[x]` `FromData` / sketch share corner setters |
| `Line` | `Line.cs` | `[x]` `FromData` / sketch share endpoints path; single `UpdateVisual` |
| `Circle` / `Ellipse` | `Circle.cs`, `Ellipse.cs` | `[x]` `FromData` sets fields + `UpdateVisual` |
| `Cross` | `Cross.cs` | `[x]` load sets fields, `IsGeometryRendered`, single `UpdateVisual` |
| Custom / Dialog | `Lan.Shapes.Custom/*`, `Lan.Shapes.DialogGeometry/*` | `[x]` selection hooks no-throw; `ThickenedLine.FromData` marks rendered |

Pattern:

```csharp
// good
public void FromData(T data) {
    ApplyData(data);      // sets fields
    RebuildGeometry();    // same as mouse path
    IsGeometryRendered = true;
    UpdateVisual();
}
```

### 1.2 Single `RenderOpen()` per update

| File | Action |
|---|---|
| `ShapeVisualBase.cs` | `[~]` optional helper deferred; existing single-open path kept |
| `Cross.cs` | `[x]` one render pass only (issue 11) |
| `Line.cs` | `[x]` `UpdateVisual` / locked path share one open |
| Other overrides | `[x]` grep `RenderOpen(` — each override opens once |

### 1.3 Selection hooks

| File | Action |
|---|---|
| `ShapeVisualBase.cs` | `[x]` empty `virtual OnSelected` / `OnDeselected` — keep |
| `Line` / `Cross` | `[x]` removed empty throw-comment overrides |
| `CustomGeometryBase` / `DxfGeometry` | `[x]` no-throw no-ops (styling via `State`) |
| `SketchBoardDataManager.SelectedGeometry` | `[x]` still calls hooks; covered by test |

### 1.4 Styler factory correctness

| File | Action |
|---|---|
| `src/Lan.Shapes/Styler/ShapeStylerFactory.cs` | `[x]` dedicated `_dottedLineStyler` field (issue 13) |
| same | `[x]` no method mutates another state’s cached styler |

### 1.5 Tests (phase 1)

| Test project | Cases |
|---|---|
| `Test/Lan.SketchBoard.Tests` | `[x]` `LoadShape` round-trip for `Polygon`, `Rectangle`, `Line`, `Circle`, `Ellipse`, `Cross` |
| same | `[x]` selection change does not throw |
| same | `[x]` `CreateNewGeometry` requires layer + type |
| same | `[x]` dotted styler isolation |

**Acceptance:**

- [x] All phase-1 tests green (`21` tests)
- [~] Manual: load saved polygon/rect/line appears correctly (unit-covered; UI smoke optional)
- [~] Manual: select/deselect each basic shape without crash (unit-covered)
- [x] Update issue log statuses for 10–13, 15 if still listed open

---

## Phase 2 — One scale policy (P0)

Goal: multi-viewer safe, one authority for stroke/handle size.

### 2.1 Policy

- [x] **Zoom scale** (`LocalScale`) is the only live driver after the board is attached
- [x] Formula: `base / max(scale, ε)` via `ViewportScalingService` + `ViewportScalingOptions`
- [x] Document that viewport-size formula is seed-only or delete it (`[Obsolete]` + XML docs)

### 2.2 Code edits

| File | Action |
|---|---|
| `src/Lan.Shapes/Scaling/ViewportScalingService.cs` | `[x]` options type; viewport-size formula seed-only / obsolete |
| `src/Lan.SketchBoard/SketchBoardDataManager.cs` | `[x]` hold `ViewportScalingOptions` (ctor inject + property) |
| same `OnImageViewerPropertyChanged` | `[x]` options overload; refresh existing shapes via `RefreshScaleDependentVisuals` |
| `src/Lan.SketchBoard/SketchBoard.cs` | `[x]` removed `SketchBoard_SizeChanged` styler mutation (dual path) |
| `src/Lan.ImageViewer/ImageViewer.cs` | `[x]` single scale → manager subscription; rebind-safe; seed on attach |
| `src/Lan.ImageViewer/ImageViewerBasic.cs` | `[x]` fit + wheel chrome stroke use same `CalculateStrokeThickness(LocalScale)` |
| `src/Lan.Shapes/ShapeVisualBase.cs` | `[x]` `RefreshScaleDependentVisuals()` for post-scale redraw |

### 2.3 Multi-viewer

- [x] Two managers with different `ViewportScalingOptions` do not clobber each other (unit-covered)
- [x] Static bases remain **readonly defaults only**

### 2.4 Tests

- [x] Unit: `CalculateStrokeThickness(2.0)` halves base; scale `0` / negative guarded
- [x] Unit/integration: two managers with different options do not clobber each other
- [x] Scale applies on layer attach after prior zoom; existing shape handle size refreshes

**Acceptance:**

- [x] Zoom path is the only live styler mutation (`LocalScale` → manager)
- [x] Resize window alone does not fight zoom-driven thickness (`SizeChanged` dual path removed)
- [x] Issue 6 updated in issue log

**Baseline (Phase 2, 2026-07-16):** `dotnet test Test/Lan.SketchBoard.Tests` → **29/29 passed**.

---

## Phase 3 — Ownership cleanup (P1)

Goal: repository owns shapes; layer is style/units only.

### 3.1 `ShapeLayer`

| File | Action |
|---|---|
| `src/Lan.Shapes/ShapeLayer.cs` | `[x]` style/units only; no shape list / dead render helpers |
| same | `[x]` comments describe style + units profile only |
| `GetStyler` | `[x]` fallback to `Normal` kept |
| layer load (`ShapeLayerManager`) | `[x]` fail-fast if required states missing (`Normal`, `Selected`); recommended `MouseOver`/`Locked` optional |
| ctor validation | `[x]` `EnsureRequiredStylerStates` shared by ctor + loader |

### 3.2 Dead / misleading APIs

| Symbol | File | Action |
|---|---|---|
| `IShapeManipulator` / `IShapeManipulator<T>` | `Interfaces/IShapeManipulator.cs` | `[x]` deleted (0 impl) |
| `ISketchBoardMouseHandler` | `Interfaces/ISketchBoardMouseHandler.cs` | `[x]` deleted; mouse stays on WPF control events |
| `ISketchBoard` empty inherit | `Interfaces/ISketchBoard.cs` | `[x]` marker only — no unused mouse inheritance |
| `ShapeStateMachine` | `Shapes/ShapeStateMachine.cs` | `[x]` deleted (0 usages) |
| any `RenderShapes` / `AddShapeToLayer` | `ShapeLayer` | `[x]` already absent; confirmed gone |

### 3.3 Folder / namespace honesty

| Item | Action |
|---|---|
| DTOs in `Models/` | `[x]` `CrossData`/`EllipseData`/`PointsData`/`TextGeometryData` under `Models/` |
| root orphans | `[x]` already under `Converters/` and `Utilities/` |
| `DragLocation` | `[x]` already under `Handle/` (issue 7) |

### 3.4 Tests

- [x] `ShapeLayer` requires Normal + Selected
- [x] Missing MouseOver falls back to Normal
- [x] `ToShapeLayerParameter` round-trip reconstructs

**Acceptance:**

- [x] No public type claims capabilities that are unimplemented (removed dead APIs)
- [x] Solution builds; tests green (`33` tests)
- [x] Issues 2, 5, 7, 8, 14 updated

**Baseline (Phase 3, 2026-07-16):** `dotnet test Test/Lan.SketchBoard.Tests` → **33/33 passed**.

---

## Phase 4 — ISP migration for consumers (P1)

Goal: only controls take the fat manager.

### 4.1 Contracts

| File | Action |
|---|---|
| `IImageViewerViewModel.cs` | `[x]` expose `ShapeRepository`, `Shapes`, `SelectedShape` |
| recommended shape | `[x]` keep `ISketchBoardDataManager SketchBoardDataManager` **for control DPs only** |
| recommended shape | `[x]` intent-level members: `SelectedShape`, `Shapes`, `ShapeRepository` |
| `ImageViewerControlViewModel.cs` | `[x]` ctor receives fat manager; logic uses repository members |
| XAML `ImageViewerControl.xaml` | `[x]` list binds `Shapes` / `SelectedShape` (not `CurrentGeometryInEdit`) |
| hosts (`SimpleApp`, `TestApp`) | `[x]` compile against new VM surface |

VM surface:

```csharp
ISketchBoardDataManager SketchBoardDataManager { get; } // control host only
IShapeRepository ShapeRepository { get; }               // same instance, shape state
ObservableCollection<ShapeVisualBase> Shapes { get; }
ShapeVisualBase? SelectedShape { get; set; }            // wraps SelectedGeometry
// existing: Image, Scale, GeometryTypeList, commands...
```

### 4.2 DI registrations

| File | Action |
|---|---|
| `ImageViewerModule.cs` | `[x]` `ISketchBoardDataManager` → `SketchBoardDataManager` |
| optional | `[x]` `Register<IShapeRepository>(c => c.Resolve<ISketchBoardDataManager>())` |
| MSDI host `App.xaml.cs` | `[x]` mirror factory registration |

### 4.3 Tests / docs

- [x] Update `scripts/IImageViewerViewModel-IoC使用说明.md` dependency guidance
- [x] VM unit tests: `ImageViewerViewModelTests` (SelectedShape, delete, no visual host)

**Acceptance:**

- [x] No new VM code depends on `VisualCollection` / `InitializeVisualCollection`
- [x] Shape list select/delete uses `SelectedShape` / repository
- [x] Issue 1 “remaining migration” marked done

**Baseline (Phase 4, 2026-07-16):** `dotnet test Test/Lan.SketchBoard.Tests` → **37/37 passed**.

---

## Phase 5 — Extensibility quality (P2)

Goal: composition root owns concretes; libraries stay dumb.

### 5.1 Geometry palette

| File | Action |
|---|---|
| `IGeometryIconProvider` (`Lan.ImageViewer`) | `[x]` `Geometry? GetIcon(string name)` |
| `ResourceDictionaryGeometryIconProvider` | `[x]` reads `Geometries.xaml`; type→key aliases; headless-safe |
| `ImageViewerControlViewModel.CreateGeometryTypeList` | `[x]` builds from `IGeometryTypeManager` + icon provider |
| `GeometryTypeRegistration.cs` | `[x]` remains the default type list for hosts |
| `Geometries.xaml` | `[x]` icons stay resources; provider reads them |

### 5.2 Styler factory injection

| File | Action |
|---|---|
| `IShapeStylerFactory` | `[x]` includes `CreateStyler(ShapeStylerParameter)` |
| `ShapeLayer` | `[x]` dual ctor: default factory + inject `IShapeStylerFactory` |
| `ShapeLayerManager` | `[x]` injects factory; `ReadShapeLayers` builds via factory |
| DI (`ImageViewerModule`, TestApp) | `[x]` `IShapeStylerFactory` → `ShapeStylerFactory` singleton |
| tests | `[x]` substitute factory (`ExtensibilityTests`) |

### 5.3 Board context

| File | Action |
|---|---|
| `IBoardContextAware` | `[x]` used in `CreateNewGeometry` when host is `SketchBoard` |
| board-size-dependent shapes | `[x]` only `FixedCenterCircle` needs board size — already implements |
| cancel path | `[x]` `ShapeCreationCancelled` → `RemoveShape` (AddShapeCore subscribe; DxfGeometry raises) |

**Acceptance:**

- [x] Adding a shape does not require editing VM icon dictionary by hand (provider/resource only)
- [x] Issue 9 updated

**Baseline (Phase 5, 2026-07-16):** `dotnet test Test/Lan.SketchBoard.Tests` → **42/42 passed**.
---

## Phase 6 — Maintainability / packaging (P3)

- [x] Fix `#region Propeties` typos (all remaining under `src/`)
- [x] Remove dead commented blocks (`Pointer` already gone; `Cross` clean; deleted `StrokeWidened*`)
- [x] Align package `<Description>` metadata with actual package content
- [x] Document NuGet `PrivateAssets=All` + copy-ref targets as intentional fat packages (csproj comments + README)
- [x] Canonical host sample in README = Prism (`SimpleApp`); MSDI (`TestApp`) as alternate
- [x] README Architecture section links ADR + this checklist
- [x] Sweep `architecture-issues.md` statuses so the table matches main

**Acceptance:** docs and metadata match code; no new behavior required.

**Baseline (Phase 6, 2026-07-16):** `dotnet test Test/Lan.SketchBoard.Tests` → **42/42 passed**; solution builds.

---

## Residual hardening (post Phase 0–6)

Not a redesign phase. Closes the top residual risks from the post-Phase-6 re-audit.

### Consumer selection cutover

- [x] `Lan.ImageViewer.Halcon/ImageViewerHalcon.xaml` list → `Shapes` / `SelectedShape`
- [x] SimpleApp `MainPageViewModel` + `MainPage.xaml.cs` use `ShapeRepository` / `SelectedShape`
- [x] TestApp `MainWindowViewModel` + `MainWindow.xaml.cs` use `ShapeRepository` / `SelectedShape`

### Multi-viewer scale isolation

- [x] `ShapeLayer.CreateIndependentCopy()` + `SetShapeLayer` clones unowned layers
- [x] `ApplyScaleToOwnedLayers` / `GetLayersAffectedByScale` — current + all shape layers
- [x] `ShapeStyler(ShapeStylerParameter)` honors `StrokeThickness` (was hardcoded 1)
- [x] `ShapeStyler.Clone()` preserves thickness
- [x] Tests: shared-config-layer isolation, non-current-layer scale, independent managers

### Serialization lifecycle

- [x] `GridGeometryData` + real `GridGeometry.FromData` / `GetMetaData` (`IDataExport<GridGeometryData>`)
- [x] `TextGeometry.FromData` / `GetMetaData` no longer throw / no-op
- [x] LoadShape round-trips for Grid + Text

### Custom shape load lifecycle (follow-up)

- [x] `ThickenedCircle` / `ThickenedRectangle` / `ThickenedCross` / `FixedCenterCircle` / `Fiber` `FromData` end with rendered + `UpdateVisual`
- [x] `ThickenedCross` export contract = 4 corners (VTL, VBR, HTL, HBR); field-assign load (no drag side-effects)
- [x] LoadShape round-trips for thickened / fiber / fixed-center shapes

### API hygiene

- [x] Removed dead `ShapeLayerManager.Shapes` DP (ownership stays on repository)
- [x] Removed `GetSketchBoardVisuals()` alias of `Shapes`
- [x] Dual init events documented: subscribe to one of `SketchBoardManagerInitialized` / `HostInitialized`, not both

**Baseline (residual + custom load, 2026-07-16):** `dotnet test Test/Lan.SketchBoard.Tests` → **51/51 passed**; solution builds.

## Phase dependency graph

```text
Phase 0 baseline
    │
    ▼
Phase 1 correctness  ──►  Phase 2 scale policy
    │                         │
    └──────────┬──────────────┘
               ▼
        Phase 3 ownership
               │
               ▼
        Phase 4 ISP / VM
               │
               ▼
        Phase 5 extensibility
               │
               ▼
        Phase 6 hygiene
```

Phases 1 and 2 can proceed in parallel after Phase 0 if staffing allows; neither should block the other, but both block calling Phase 3 “done.”

---

## Definition of done (architecture track)

- [x] One scale authority; multi-viewer independent
- [x] Repository owns shapes; layer is style/units only
- [x] VMs depend on shape-state contract; controls own visual host
- [x] Create/load/render/select share builders; no double `RenderOpen`
- [x] Unused public APIs removed or implemented
- [x] ADR + issue log + README agree with the code
- [x] Tests cover load round-trip for basic shapes + repository selection

---

## Out of scope (do not sneak in)

- Cross-platform / non-WPF backends
- Rewriting shapes as FrameworkElements
- Large feature work (new shape types, Halcon features) until Phase 1–2 acceptance is green
