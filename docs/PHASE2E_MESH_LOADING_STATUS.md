# PicoMoldForge v5 - Phase 2E Mesh Loading Status

## Status

Phase 2E is closed as a verified PicoGK mesh-loading phase.

## Verified behavior

- PicoGK 2.0.0 is installed only in the `PicoMoldForge.PicoGK` adapter project.
- `PicoMoldForge.PicoGK` targets `net9.0`.
- `PicoMoldForge.Core`, `PicoMoldForge.Cli`, and `PicoMoldForge.Reporting` remain on `net8.0`.
- `PicoGkTaskRunner` executes PicoGK work inside `Library.Go`.
- `Library.Go` must use `bEndAppWithTask: true` for tests and non-interactive execution.
- `PicoMeshService` can load a binary STL through `Mesh.mshFromStlFile`.
- `PicoMeshService` reports:
  - source path;
  - triangle count;
  - vertex count;
  - bounding box string.

## Runtime findings

- Calling `Library.oLibrary()` outside `Library.Go` fails.
- ASCII STL loading is not implemented by the tested PicoGK 2.0.0 mesh import path.
- Binary STL is the functional STL format for real PicoGK mesh loading in this project.

## Design decision

PicoMoldForge Core may continue to recognize `.stl` as the user-facing input format, but the PicoGK adapter requires a binary STL when invoking real `Mesh.mshFromStlFile`.

ASCII STL support must not be assumed.

If ASCII STL support is needed later, it must be implemented as a controlled conversion step before calling PicoGK, or delegated to a verified external converter.

## Current baseline expectation

The baseline must remain green after this phase:

- dotnet restore
- dotnet build
- dotnet test
- dotnet run --project src/PicoMoldForge.Cli -- --self-test

## Next phase

Phase 2G should convert a real PicoGK `Mesh` into `Voxels` using verified APIs.

Candidate APIs already observed by reflection:

- `Voxels.RenderMesh(Mesh& msh)`
- `Voxels.mshAsMesh()`
- `Voxels.CalculateProperties(Single& fVolumeCubicMM, BBox3& oBBox)`
- `Voxels.oCalculateBoundingBox()`