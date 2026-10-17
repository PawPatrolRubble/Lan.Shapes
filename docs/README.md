# Lan.Shapes documentation index

Lan.Shapes is a **Windows-only WPF** image viewer and 2D annotation/sketch
component. Its renderer uses `DrawingVisual`; it is not a standalone
parametric CAD engine.

## Start here

| Document | Audience | Contents |
| --- | --- | --- |
| [Maintainer guide](MAINTAINER_GUIDE.md) | Maintainers and integrators | Development prerequisites, build/test/CI, release process and package boundaries |
| [Feature status and persistence](FEATURE_STATUS.md) | Application integrators | Supported interactions, data export vs save, limits and explicitly deferred features |
| [Architecture overview](ARCHITECTURE_OVERVIEW.md) | Developers and reviewers | Assembly ownership, shape lifecycle, extension points and related implementation files |
| [Project README](../README.md) | Everyone | Installation, host wiring, controls, custom shapes and API examples |

## Behavior contracts

- [Shape groups](shape-groups.md) — grouping, selection and translation invariants
- [Undo / redo](undo-redo.md) — history scope, transactions and limitations
- [Geometry property editor](shape-geometry-editing.md) — input/edit validation and model updates
- [Layer management](layer-management-fixes.md) — layer catalogue, styles and persistence
- [Shape visual validation](shape-visual-validation.md) — rendering regression checks

## Design decisions and prior maintenance

- [ADR 0001 — WPF-native sketch](adr/0001-wpf-native-sketch-architecture.md)
- [ADR 0002 — geometry property editing](adr/0002-property-panel-geometry-editing.md)
- [Proposals](proposals/README.md) — design proposals; some are historical
- [Architecture issue log](architecture-issues.md) — historical audit; verify statuses against current code
- [Refactor checklist](refactor-checklist.md) — historical implementation phases and baselines

## Scope of this documentation update

This maintenance pass fixes CI targeting and stale package documentation and
clarifies existing behavior. It does **not** introduce document serialization,
CAD constraints or any new drawing feature. In particular, documentation of a
limitation is not a promise of a release date.
