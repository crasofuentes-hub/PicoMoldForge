# PicoMoldForge v5 - Phase 5 Cavity/Core Status

## Status

Phase 5 is closed as a verified preliminary cavity/core generation phase.

## Verified capabilities

The system now has Core contracts for cavity/core generation and a real PicoGK-backed preliminary generator.

The current flow is:

- binary STL input;
- PicoGK mesh loading;
- uniform shrinkage compensation;
- preliminary cavity mesh export;
- preliminary core mesh export.

## Core contracts

Core now defines:

- ShrinkageCompensator;
- ShrinkageCompensationResult;
- CavityCoreGenerationRequest;
- CavityCoreGenerationResult;
- CavityCoreArtifact;
- CavityCoreArtifactKind;
- CavityCorePreliminaryPlanner.

## PicoGK implementation

The PicoGK adapter now defines:

- PicoCavityCoreGenerator.

The generator currently:

- loads a binary STL with PicoGK;
- applies uniform shrinkage scale;
- writes Cavity.stl;
- writes Core.stl;
- emits explicit preliminary warnings.

## Generated artifacts

The current preliminary outputs are:

- Cavity.stl;
- Core.stl.

These are diagnostic geometry artifacts, not production mold geometry.

## Shrinkage model

The current shrinkage model is uniform scale:

ScaleFactor = 1 + ShrinkageRate

Example:

ShrinkageRate = 0.011
ScaleFactor = 1.011

## Current limitations

The current cavity/core generation does not yet implement:

- true cavity subtraction;
- true core extraction;
- shutoff surfaces;
- parting-line optimized split;
- side actions;
- slides;
- lifters;
- inserts;
- ejector pins;
- venting;
- draft-angle correction;
- mold base integration;
- manufacturability certification.

## Safety and accuracy note

Cavity.stl and Core.stl are preliminary diagnostic outputs.

They must not be treated as production-ready mold components.

## Verified baseline

After Phase 5B, the observed verified state was:

- total tests: 41 passing;
- build passing;
- baseline passing;
- self-test passing.

## Next major phase

Phase 6 should implement preliminary cooling contracts before generating real cooling geometry.

Recommended Phase 6A scope:

- CoolingChannelRequest in Core;
- CoolingChannelPlan in Core;
- CoolingChannelSegment in Core;
- deterministic simple straight-channel planner;
- validation rules for channel diameter, spacing, and clearance;
- Core tests first;
- no PicoGK cooling geometry until Core contracts pass baseline.