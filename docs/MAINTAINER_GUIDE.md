# Maintainer guide

## Supported environment

- Windows with the **.NET 8 SDK**, WPF workload and a suitable Visual Studio / MSBuild installation.
- Target framework: `net8.0-windows` for the main projects.
- Windows is required to run WPF UI and input integration tests. No cross-platform
  target is planned (see [ADR 0001](adr/0001-wpf-native-sketch-architecture.md)).
- Dependency restore requires access to the configured NuGet sources; some hosts
  may additionally require HALCON/visiondotnet runtime dependencies.

## Repository entry points

| Location | Responsibility |
| --- | --- |
| `Lan.Shapes.sln` | Main source solution, sample applications and unit/integration test projects |
| `Lan.Shapes.SimpleApp/` | Canonical Prism + DryIoc sample |
| `Test/Lan.Shapes.TestApp/` | Alternate Microsoft DI sample |
| `Test/Lan.Shapes.InteractionProbe/` | Real HWND/desktop-input probes, intentionally outside main solution and CI |
| `src/Lan.Shapes/` | Shape model, `DrawingVisual` renderer and geometry contracts |
| `src/Lan.SketchBoard/` | Repository, board host and interaction history |
| `src/Lan.ImageViewer/` | Image viewer and WPF UI |
| `src/Lan.ImageViewer.Prism/` | Prism registration and default view-model |

Read the [architecture overview](ARCHITECTURE_OVERVIEW.md) before editing the
public shape lifecycle or dependency registrations.

## Local verification

Run on Windows at the repository root:

```powershell
dotnet restore Lan.Shapes.sln
dotnet build Lan.Shapes.sln --configuration Release --no-restore
dotnet test Lan.Shapes.sln --configuration Release --no-build --no-restore
```

For targeted regressions:

```powershell
dotnet test Test/Lan.SketchBoard.Tests/Lan.SketchBoard.Tests.csproj --configuration Release
dotnet test src/Lan.Shapes.Tests/Lan.Shapes.Tests.csproj --configuration Release
dotnet run --project Lan.Shapes.SimpleApp/Lan.Shapes.SimpleApp.csproj
```

GUI checks must be performed in an interactive Windows session. Verify creating
and cancelling line/rectangle/circle sketches; selection and handle edits; zoom;
snap indicators; cross-viewer state isolation; layers and locked/hidden shapes;
group/ungroup; undo/redo; DXF and raster export. Unit tests are not a substitute
for desktop pointer/focus/capture tests.

The October 5, 2026 group-feature document reports **634 passing WPF tests**
and **9 passing shape-data tests** at that time. These are historic results,
not a claim that today's head passes.

## CI

Workflow: [`.github/workflows/dotnet-desktop.yml`](../.github/workflows/dotnet-desktop.yml).

- Triggers: push to `master`, pull requests targeting `master`, and manual
  `workflow_dispatch`. The repository's default branch is `master`.
- Environment: `windows-latest`, .NET SDK 8, Debug and Release configurations.
- Pipeline: restore, build, then test `Lan.Shapes.sln`.
- This pipeline does **not** drive real desktop mouse/keyboard input. Use a
  manual interactive smoke test before shipping UI input changes.
- A green CI result must be checked on the actual commit/PR; changing the
  trigger configuration is not evidence of a passing build. Old failures
  should not be treated as the status of the current head.

If CI fails, check the specific step and package restore sources first. The
solution includes the HALCON adapter and sample hosts, so failures may originate
outside the two packaged viewer assemblies. Document the actual error before
excluding a project or disabling tests.

## NuGet package boundaries

The following are project-file `PackageVersion` values at the time of this
documentation update, **not confirmation of versions already uploaded to Nexus**:

| NuGet package | Declared version | Assemblies bundled by project references |
| --- | --- | --- |
| `Lan.ImageViewer` | `2.1.4` | Viewer, `Lan.Shapes`, `Lan.Shapes.Custom`, `Lan.SketchBoard` |
| `Lan.ImageViewer.Prism` | `2.1.5` | Prism integration, viewer, core shapes, custom shapes, dialog/DXF geometry and board |

`Lan.ImageViewer` does **not** directly reference
`Lan.Shapes.DialogGeometry`. Use the Prism package or add the required
dialog/DXF integration explicitly when needed. Do not assume both packages
embed identical assemblies.

Both host-facing projects intentionally use a fat-package model. Their
`PrivateAssets="All"` project references and custom
`CopyProjectReferencesToPackage` target affect which assemblies go into the
nupkg. Do not simplify this packaging without checking the actual nupkg file
list and package restore behavior in a clean consumer.

## Release checklist

1. Review changes and verify CI plus a manual WPF smoke test.
2. Bump `<PackageVersion>` independently in both
   `src/Lan.ImageViewer/Lan.ImageViewer.csproj` and
   `src/Lan.ImageViewer.Prism/Lan.ImageViewer.Prism.csproj` as appropriate.
   Keep install examples and the declared-version table in sync.
3. Run `./scripts/release-packages.ps1 -PackOnly` from PowerShell. This
   restores, builds, then packs into `artifacts/packages` without publishing.
4. Inspect both `.nupkg` files, embedded assemblies and dependency metadata;
   test a clean consumer restore when possible.
5. Confirm Nexus feed access and publication policy; only then set
   `$env:NEXUS_API_KEY` and run `./scripts/release-packages.ps1`.
6. Verify uploaded versions directly in Nexus and record the release outcome.

The script fails on missing API key (unless `-PackOnly`), restore, build, pack
or push errors. It uses `--skip-duplicate`. It does **not** execute tests, so
complete testing before running it.

## Change scope and review

During the current maintenance phase, prioritize CI reliability, build health,
documentation accuracy and regression fixes. No new geometry tools, complete
document save/load, group transformations or parameter constraints are planned
in this pass. Keep behavior changes separate from documentation-only updates.
