# PicoMoldForge v5 - Phase 8 Mold Systems Status

## Status

Phase 8 is closed as a verified preliminary mold-system planning and diagnostic export phase.

## Verified capabilities

The system now supports deterministic preliminary mold-system planning in Core and diagnostic mold-system geometry export through the PicoGK adapter.

The current flow is:

- MoldSystemRequest;
- PreliminaryMoldSystemPlanner;
- MoldSystemPlan;
- EjectorPinPlan;
- VentPlan;
- InsertPlan;
- PicoGK Lattice.AddBeam;
- PicoGK Voxels;
- PicoGK Mesh;
- MoldSystemDiagnostic.stl.

## Core contracts

Core now defines:

- MoldBaseEnvelope;
- MoldSystemRequest;
- MoldSystemPlan;
- EjectorPin;
- EjectorPinPlan;
- VentChannel;
- VentPlan;
- InsertPocket;
- InsertPlan;
- PreliminaryMoldSystemPlanner.

## Core validation

The current mold-system request validation covers:

- output directory is required;
- part size X must be greater than zero;
- part size Y must be greater than zero;
- part size Z must be greater than zero;
- mold margin must be greater than zero;
- ejector pin diameter must be greater than zero;
- ejector pin count must be greater than zero;
- ejector pin count must not exceed the preliminary planner limit;
- vent width must be greater than zero;
- vent depth must be greater than zero;
- insert clearance cannot be negative;
- insert clearance must be less than mold margin.

## Current mold-system planner

The current planner creates deterministic preliminary planning references for:

- mold base envelope;
- ejector pin positions;
- vent channel references;
- insert pocket envelope.

It does not optimize mold-system layout.

It does not evaluate collisions.

It does not validate machining access.

It does not validate shutoff behavior.

It does not certify production manufacturability.

## PicoGK implementation

The PicoGK adapter now defines:

- PicoMoldSystemDiagnosticExporter;
- PicoMoldSystemDiagnosticExportResult.

The current PicoGK export path is:

- create PicoGK Lattice;
- add ejector pins as diagnostic beams;
- add vent channels as diagnostic beams;
- add insert pocket as a diagnostic bounding-box frame;
- convert Lattice to Voxels;
- convert Voxels to Mesh;
- export MoldSystemDiagnostic.stl.

## Generated artifact

The current preliminary mold-system output is:

- MoldSystemDiagnostic.stl.

This file is a diagnostic visualization artifact.

It is not a production-ready mold-system geometry file.

## Explicit limitations

Phase 8 does not yet implement:

- actual ejector mechanism geometry;
- ejector plate design;
- leader pins;
- return pins;
- sprue, runner, or gate system;
- true insert geometry;
- shutoff surfaces;
- parting-line optimized mold base;
- collision checks between ejectors, cooling channels, inserts, lattice, cavity, and core;
- machining access validation;
- tolerance stack-up validation;
- manufacturability certification.

## Safety and accuracy note

MoldSystemDiagnostic.stl must not be treated as validated mold tooling geometry.

It is a preliminary diagnostic representation of mold-system planning data.

## Verified baseline

After Phase 8B, the observed verified state was:

- total tests: 73 passing;
- build passing;
- baseline passing;
- self-test passing.

## Next major phase

Phase 9 should implement preliminary DfAM contracts and checks.

Recommended Phase 9A scope:

- DfAMRule;
- DfAMCheckResult;
- DfAMReport;
- PreliminaryDfAMAnalyzer;
- minimum wall-thickness placeholder;
- cooling clearance sanity check;
- lattice beam radius sanity check;
- ejector pin diameter sanity check;
- explicit non-certification warning;
- Core tests first;
- baseline after implementation.