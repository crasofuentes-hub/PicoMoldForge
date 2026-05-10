# PicoMoldForge v5 - Phase 3A Preliminary Analysis Status

## Status

Phase 3A is closed as a verified preliminary geometric-analysis phase.

## Verified capabilities

`PartAnalysisReport` now represents a preliminary analysis result without exposing PicoGK runtime types to Core.

The current analysis pipeline is:

- binary STL input;
- PicoGK mesh load;
- PicoGK voxelization;
- Core-facing analysis report.

## Reported metrics

The current report includes:

- source path;
- triangle count;
- vertex count;
- mesh bounding box;
- voxel size;
- voxelized volume;
- voxel slice count;
- voxel memory usage;
- voxel bounding box;
- analysis warnings.

## Current warnings

The current analyzer emits explicit warnings for:

- preliminary analysis only;
- binary STL required by the current PicoGK adapter path.

## Verified architecture

- `PicoMoldForge.Core` defines analysis DTOs.
- `PicoMoldForge.PicoGK` composes real PicoGK mesh and voxel metrics.
- Core does not depend on PicoGK runtime types.
- PicoGK work continues to run through `Library.Go`.

## Explicit limitations

The current analysis does not yet include:

- wall thickness estimation;
- undercut detection;
- draft-angle analysis;
- support-angle analysis;
- parting plane estimation;
- mold manufacturability certification;
- automatic geometry correction.

## Next phase

Phase 3C should add the first deterministic undercut heuristic.

Recommended scope:

- keep the result reportable, not corrective;
- use opening direction from configuration or a default direction;
- start with a bounded, deterministic heuristic;
- add tests before expanding behavior.

## Baseline requirement

The baseline must remain green after this phase:

- dotnet restore
- dotnet build
- dotnet test
- dotnet run --project src/PicoMoldForge.Cli -- --self-test