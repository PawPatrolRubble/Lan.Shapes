# Layer management ownership and lifecycle

The layer catalogue belongs to `IShapeLayerManager`. A viewer uses that catalogue;
assigning a different collection to its compatibility `Layers` setter is rejected
before changing viewer state. Change catalogues by reading a complete configuration
through the manager.
The current-layer setter resolves by catalogue ID and rejects unknown layers before
changing viewer or board state. A catalogue must retain at least one configured layer.

The public `Shapes` and `Layers` collection types remain `ObservableCollection` for
host compatibility. Their implementations control mutations, so direct collection
operations follow the same validation, subscriptions and lifecycle as manager methods.
New host code should use repository CRUD and manager definition commands.

Catalogue publication does not suppress edits made by external collection callbacks.
Those edits run normal validation, update the snapshot and remain unsaved until Save;
an automatically saved creation command does not overwrite their dirty state.
Definition updates prepare validation, styler creation and `ConfigurationChanging`
checks before writing the file. The validated proposal passed to that event is a copy
for inspection and rejection. A successful command commits the prepared definition
and snapshot before publishing its property and definition-change notifications.

Shape replacement rejects an incoming visual already attached to another host before
changing either shape. It attaches the replacement before detaching the original;
an attach failure removes a partially attached replacement while retaining the original
visual, selection and subscriptions.

## Ownership

- The layer manager owns configured definitions, validation, configuration snapshots,
  save state and persistence. Direct supported definition/style edits update the
  snapshot, mark it unsaved and notify viewers; they require an explicit save.
- Each board owns its independent runtime layers and their scaled stylers. Adding or
  assigning a shape resolves its layer into the board's owned instance. Zoom cannot
  write to another board or the shared catalogue.
- The repository owns shapes, selection, drawing state and visibility. Collection
  writes reconcile the visual mirror and subscriptions before publishing changes.
- The viewer's tree is a projection. Incremental collection changes update affected
  groups; configuration replacement and batch assignments can reconcile the projection.

## Configuration replacement

`ConfigurationChanged` is published when the complete new catalogue and global
settings are ready. The viewer calls `ApplyLayerDefinitions` once, rebinding measurement
settings as well as styles. Current drawing layers are retained by ID when possible.
Shapes whose definitions disappear remain board-local orphan shapes: visible and
selectable, but their catalogue editor is disabled. They can be assigned to an active
configured layer. Orphan layers are not added back into the saved catalogue.

Construction, loading, creation, editing and saving share layer validation. IDs and
normalized names must be unique in a catalogue. Styler dictionary structure is read-only;
edit a `ShapeLayerParameter.StyleSchema` draft and apply it to change state definitions.

## Drawing and lifetime

Hiding a layer cancels its unfinished sketch. Drawing on a hidden current layer is
blocked. Tree selection and other selection inputs cancel the current sketch through
the same repository operation before selecting existing shapes.
Cancellation preserves the completed selection; the explicit Select command clears it.

Viewer owners must call `IImageViewerViewModel.Dispose` when the viewer is permanently
discarded. The default implementation releases catalogue, repository, shape and tree
subscriptions. Controls use weak property subscriptions and do not dispose an externally
owned view-model. `DetachVisualHost` removes a manager's references to its old WPF host
while preserving shapes for later reattachment.

## Runtime files

The default runtime directory is now:

```text
%LOCALAPPDATA%\Lan.Shapes\{entry assembly name}\
```

Hosts can pass a stable `applicationId` to `LayerConfigurationStore.ResolveRuntimeFile`.
An explicit `runtimeDirectory` without an application ID is treated as an already scoped
directory for backwards compatibility. With an application ID, that directory is the
parent of the application subdirectory. A file-name override can identify separate profiles.
Prism configuration accepts `lanShapesApplicationId` and `lanShapesRuntimeDirectory`.

An existing file is never overwritten by shipped defaults. The old shared
`%LOCALAPPDATA%\Lan.Shapes\LanShapesConfig.json` is preserved; it is not automatically
attributed to an application because its original owner is unknown. To migrate known
edits, read that file explicitly and save it to the intended application's scoped path.
Defaults continue to seed only new runtime files; changing shipped defaults does not
silently merge into user configuration.

## Regression verification

The architecture regressions cover cross-board style isolation, direct collection
mutations, full configuration replacement, invalid definitions, dirty notifications,
viewer disposal, native tree selection, interrupted drawing and host isolation.
Run the WPF test suite on Windows:

```powershell
dotnet test Test/Lan.SketchBoard.Tests/Lan.SketchBoard.Tests.csproj
```

Verified on Windows on 2026-10-05: 434 tests passed, none failed or skipped.
The pre-change baseline contained 385 passing cases. The initial architecture fixes
added 40 cases, and the follow-up fixes for the first three review findings add 9 more,
including parameterized cases. These cover callback edits and duplicate rejection,
validation veto before persistence, coherent public update notifications, and visual
replacement failure recovery. Real WPF control tests cover catalogue reload
bindings, native tree selection and disabled orphan editors. The complete solution
also builds successfully with `dotnet build Lan.Shapes.sln --no-restore`.
Existing nullable and unused-field warnings remain outside this layer-management work.
