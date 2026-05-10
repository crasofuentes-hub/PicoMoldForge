# PicoMoldForge v5 - Phase 2 Status

## Status

Phase 2 is closed as a verified PicoGK mesh, voxel, metrics, and diagnostic export phase.

## Verified environment

- PicoGK 2.0.0 is installed in `src/PicoMoldForge.PicoGK`.
- `PicoMoldForge.PicoGK` targets `net9.0`.
- `PicoMoldForge.PicoGK.Tests` targets `net9.0`.
- `PicoMoldForge.Core`, `PicoMoldForge.Cli`, and `PicoMoldForge.Reporting` remain on `net8.0`.

## Verified PicoGK APIs

The following PicoGK APIs were inspected and used directly:

- `Library.Go`
- `Library.oLibrary`
- `Library.strName`
- `Library.strVersion`
- `Library.strBuildInfo`
- `Mesh.mshFromStlFile`
- `Mesh.nTriangleCount`
- `Mesh.nVertexCount`
- `Mesh.oBoundingBox`
- `Mesh.SaveToStlFile`
- `Voxels(in Mesh)`
- `Voxels.CalculateProperties`
- `Voxels.nSliceCount`
- `Voxels.nMemUsage`
- `Voxels.mshAsMesh`
- `BBox3`

## Verified project capabilities

- The PicoGK runtime can be probed from the adapter.
- Binary STL can be loaded into a real PicoGK `Mesh`.
- Mesh metrics can be read:
  - triangle count;
  - vertex count;
  - bounding box string.
- A real PicoGK `Mesh` can be converted into `Voxels`.
- Voxel metrics can be read:
  - voxel size;
  - voxelized volume;
  - slice count;
  - memory usage;
  - bounding box string.
- A voxelized diagnostic mesh can be exported to STL through `voxels.mshAsMesh()` and `Mesh.SaveToStlFile`.

## Runtime constraints discovered

- PicoGK work requiring `Library.oLibrary()` must run inside `Library.Go`.
- Non-interactive execution and tests require `Library.Go(..., bEndAppWithTask: true, ...)`.
- PicoGK 2.0.0 did not load ASCII STL through the tested `Mesh.mshFromStlFile` path.
- Binary STL is the verified functional STL input format for the PicoGK adapter.
- PicoGK tests have parallelization disabled to avoid runtime-global conflicts.

## Explicit non-goals for Phase 2

- No mold cavity generation.
- No core generation.
- No shrinkage compensation.
- No conformal cooling.
- No lattice optimization.
- No parting plane.
- No undercut detection.
- No DfAM validation.
- No production certification.

## Baseline

The baseline remains mandatory:

- `dotnet restore`
- `dotnet build`
- `dotnet test`
- `dotnet run --project src/PicoMoldForge.Cli -- --self-test`

Latest verified state after Phase 2H:

- PicoGK diagnostic export tests pass.
- Full baseline passes.
- Total test count observed: 18 passing tests.

## Next phase

Phase 3 should implement preliminary geometric analysis.

Recommended Phase 3A scope:

- Create `PartAnalysisReport` in Core.
- Create a Core-facing analysis DTO that does not depend on PicoGK types.
- Use existing PicoGK metrics from mesh and voxel services.
- Report:
  - source path;
  - triangle count;
  - vertex count;
  - mesh bounding box;
  - voxelized volume;
  - voxel bounding box;
  - warnings.
- Keep undercut detection and wall thickness as explicit later substeps unless implemented heuristically and tested.