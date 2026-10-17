# Feature status, persistence and known limits

This page distinguishes implemented, tested behavior from adjacent capabilities
that **do not** exist as a complete feature. The component is an interactive
WPF annotation/sketching control, not a full parametric CAD application.

## Supported behavior

| Area | Current contract | Important boundary |
| --- | --- | --- |
| Geometry | Line, rectangle, rotated rectangle, circle, ellipse, cross, polygon, angle, ruler cross and extensions | Each shape has its own interaction/data contract |
| Editing | Interactive selection, handles, translation, rotation on supported shapes, locks | Specific shape types/handles determine what's editable |
| Snapping | Nearby anchors during drawing and supported resizing, with a screen-space tolerance | Not a persistent geometric relationship or constraint |
| Grouping | Group/ungroup, select/move a whole single-layer group | One board only; no nested groups or group-wide rotate/scale |
| History | In-memory undo/redo for documented delete, move, rotate, resize and property edits | Up to 100 steps per board; not a file/history journal |
| Layers | Visibility, current layer, styling and JSON catalogue configuration | Layer JSON does not contain geometry or groups |
| Property panel | Numeric coordinate/size editor for built-in line, rectangle and circle | Not a generic parametric editor for every shape |
| Import/export | DXF geometry integration and raster canvas export | Neither is a lossless full-project round trip |
| Imaging | Image viewer, zoom/pan, measurements, pixel readout, optional HALCON adapter | Image viewer is Windows/WPF only |

See [README](../README.md) for end-user controls and
[documentation index](README.md) for detailed contracts.

## What is actually persisted?

| Data or action | Persisted where? | Round-trip guarantee |
| --- | --- | --- |
| Layer definitions, colors, stroke styles, calibration/config | JSON layer / `LanShapesConfig.json` through the layer manager | Layer configuration only; it does not include shapes |
| Individual shape metadata | In-memory model and per-shape `IDataExport<T>` / `FromData` contracts | Caller can build its own integration; no unified document schema |
| Image plus visible annotations | PNG, JPEG or BMP via canvas export | Flattened image; annotations are not subsequently editable shapes |
| DXF geometry | DXF export/import paths | Geometry interchange, **not** the full WPF application state |
| Shape groups | In-memory `ShapeGroup` relationships on one board | **Not** part of layer JSON; no full-document persistence |
| Undo/redo stack | Board-local memory | **Not** saved across application restarts |
| Tool, viewport and current selection | Runtime UI state | No documented complete-document save/load |

**There is currently no public, unified Save Project / Open Project
contract** that captures the source image, shape records, layer assignments,
group identities, z-order, transforms, per-shape custom metadata and UI state
and then restores all of them losslessly. Do not label layer configuration,
DXF or bitmap export as project persistence.

Integrators needing persistence now must explicitly own their data model,
serialization/migration policy and per-shape conversion, and separately
validate reconstruction of layers, groups and custom geometry. These APIs
do not provide an automatic full-document contract.

## Known interaction limits

- Grouping requires compatible unlocked, completed, movable shapes on the
  same visible layer. A shape belongs to at most one group. Group selection
  and translation are supported; nested groups, group rotation/scaling and
  group-move snapping are not.
- Grouping/ungrouping and some layer/model edits clear undo history rather
  than adding an undoable command. Supported history stores up to 100
  in-memory steps. It cannot guarantee rollback on arbitrary third-party
  drawing exceptions.
- The numeric property editor supports exact built-in
  `Line`, `Rectangle`, and `Circle` shapes (not automatically every
  subclass); grouped, locked, multi-selected, or independently transformed
  shapes are restricted.
- Snap points are transient geometric alignment aids; they are not linked
  dependencies. Moving a snapped-to shape later does not move its neighbors.
- UI and imaging depend on WPF; the library does not support Linux, macOS,
  headless rendering or a cross-platform CAD geometry core.
- Some shape-specific import/export fidelity depends on extension behavior;
  verify custom shapes and DXF in the consuming application.

## Explicitly deferred functionality

This maintenance period is for correctness, documentation and regression
protection only. The following are **not implemented as part of it**:

- Complete versioned project/document serialization and round-trip import.
- Nested groups and whole-group rotation/scaling/snapping.
- Parametric sketch constraints, dimension-driven geometry and a constraint solver.
- Any new drawing tool or UI feature solely to extend product scope.

These are scope notes, not delivery commitments. If such features are later
required, write a separate design proposal and acceptance tests.

## Related detailed contracts

- [Shape groups](shape-groups.md)
- [Undo / redo](undo-redo.md)
- [Layer management](layer-management-fixes.md)
- [Property-panel edits](shape-geometry-editing.md)
- [Architecture overview](ARCHITECTURE_OVERVIEW.md)
