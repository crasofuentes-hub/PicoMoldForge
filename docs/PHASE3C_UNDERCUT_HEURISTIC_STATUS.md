# PicoMoldForge v5 - Phase 3C Undercut Heuristic Status

## Status

Phase 3C is closed as a verified preliminary undercut-risk reporting phase.

## Verified behavior

The system now provides a deterministic and reportable undercut-risk heuristic.

The current heuristic:

- loads a binary STL through the real PicoGK adapter;
- reads triangles from the real PicoGK Mesh;
- computes a triangle normal from triangle vertices;
- compares the triangle normal against an opening direction;
- counts triangles whose normals oppose the opening direction;
- reports the opposing-normal count and ratio;
- emits a warning in PartAnalysisReport when risk is detected.

## Default opening direction

The current default opening direction is positive Z:

- X: 0
- Y: 0
- Z: 1

This is represented by OpeningDirection3.PositiveZ.

## Current report integration

PicoPartAnalyzer now includes the undercut heuristic in the preliminary analysis flow.

When the heuristic detects opposing-normal triangles, PartAnalysisReport includes this warning code:

- UNDERCUT_HEURISTIC_RISK

## Important limitation

This is not certified industrial undercut detection.

This heuristic does not yet perform:

- true accessibility analysis;
- shadow-volume analysis;
- draft-angle classification;
- side-action detection;
- parting-line interaction analysis;
- automatic geometry correction;
- toolpath validation;
- manufacturability certification.

It is only a deterministic preliminary risk signal.

## Why this limitation is intentional

The project must remain honest about what has been implemented.

At this phase, the correct behavior is to report a risk indicator, not to claim that all real mold undercuts are detected.

## Verified architecture

- Core owns OpeningDirection3 and UndercutHeuristicResult.
- PicoGK adapter owns PicoUndercutHeuristicAnalyzer.
- Core does not depend on PicoGK runtime types.
- PicoGK-dependent execution remains isolated behind the adapter.
- PicoGK runtime work continues to run through Library.Go.

## Phase 3 status

Phase 3 now includes:

- bounding-box reporting;
- voxelized-volume reporting;
- preliminary PartAnalysisReport;
- preliminary undercut-risk heuristic;
- explicit warnings and limitations.

Approximate wall-thickness detection is deferred.

The correct future location for stronger wall-thickness validation is the DfAM phase, where it can be implemented as a separate validated rule instead of being mixed into the preliminary analyzer.

## Next major phase

Phase 4 should implement PartingPlaneEngine.

Recommended Phase 4A scope:

- create PartingPlaneEngine in Core;
- support automatic mode based on dominant bounding-box axis;
- support manual override from configuration later;
- keep output deterministic and reportable;
- add tests before connecting it to PicoGK-generated metrics.

## Baseline requirement

The baseline must remain green after this documentation phase:

- dotnet restore
- dotnet build
- dotnet test
- dotnet run --project src/PicoMoldForge.Cli -- --self-test