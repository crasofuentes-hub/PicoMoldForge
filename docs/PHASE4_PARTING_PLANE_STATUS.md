# PicoMoldForge v5 - Phase 4 Parting Plane Status

## Status

Phase 4 is closed as a verified preliminary parting-plane phase.

## Verified capabilities

The system now supports a deterministic automatic parting-plane result.

The current flow is:

- load binary STL through PicoGK;
- inspect real mesh vertices;
- compute numeric bounding box;
- convert the bounding box to Core PartingBoundingBox;
- select dominant bounding-box axis;
- place the parting plane at the center of that axis;
- report the opening direction;
- include PartingPlaneResult in PartAnalysisReport.

## Current automatic method

The current automatic method is:

Dominant bounding-box axis with center-plane placement.

Axis selection is deterministic:

- X is selected when X is greater than or equal to Y and Z.
- Y is selected when Y is greater than or equal to Z after X is not dominant.
- Z is selected otherwise.

Tie-break order:

- X
- Y
- Z

## Current output

PartingPlaneResult includes:

- mode;
- selected axis;
- opening direction;
- plane offset in millimeters;
- method description;
- warnings.

## Current warnings

The current system explicitly warns that the parting plane is preliminary and based on bounding-box analysis.

## Verified architecture

- Core owns PartingPlaneEngine and PartingPlaneResult.
- PicoGK adapter owns PicoPartingPlaneAnalyzer.
- Core remains independent from PicoGK runtime types.
- PartAnalysisReport can include a PartingPlaneResult.
- PicoGK execution remains isolated behind Library.Go.

## Explicit limitations

The current parting-plane implementation does not yet evaluate:

- local surface complexity;
- side actions;
- slides;
- lifters;
- inserts;
- draft angle;
- true undercut accessibility;
- parting-line optimization;
- shutoff surfaces;
- manufacturability certification.

The current implementation is a deterministic preliminary estimate, not a production mold design.

## Phase 4 verified baseline

The verified baseline after Phase 4C showed:

- total tests: 32 passing;
- build passing;
- self-test passing;
- baseline passing.

## Next major phase

Phase 5 should begin with Cavity and Core contracts.

Recommended Phase 5A scope:

- create ShrinkageCompensator in Core;
- create CavityCoreGenerationRequest in Core;
- create CavityCoreGenerationResult in Core;
- create preliminary Cavity/Core warnings;
- add deterministic tests;
- do not perform final mold-quality geometry yet.

PicoGK geometry generation for cavity and core should be introduced only after Core contracts pass baseline.