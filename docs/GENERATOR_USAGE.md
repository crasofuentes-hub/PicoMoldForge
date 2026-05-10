# PicoMoldForge Generator Usage

## Status

PicoMoldForge now includes a publishable Windows executable:

PicoMoldForge.Generator.exe

Latest verified state:

- Tests: 105 PASS
- Build: PASS
- Baseline: PASS
- Self-test: PASS

## Publish the generator

From the repository root:

Set-Location "C:\repos\PicoMoldForge"

dotnet publish ".\src\PicoMoldForge.Generator\PicoMoldForge.Generator.csproj" -c Release -r win-x64 --self-contained false -o ".\publish\PicoMoldForge.Generator"

The executable is created at:

publish\PicoMoldForge.Generator\PicoMoldForge.Generator.exe

## Verify the published executable

Use:

powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\verify-generator-publish.ps1"

This script publishes the generator, runs --self-test, runs the sample config, verifies generated artifacts, and verifies FinalProjectReport.json.

## Run the generator self-test

.\publish\PicoMoldForge.Generator\PicoMoldForge.Generator.exe --self-test

## Run the generator with the included sample

.\publish\PicoMoldForge.Generator\PicoMoldForge.Generator.exe --config ".\samples\generator-valid-project.json" --generate-all

Expected generated output directory:

samples\generated\generator-sample

Expected artifacts:

- DiagnosticMesh.stl
- Cavity.stl
- BooleanCavity.stl
- Core.stl
- CoolingDiagnostic.stl
- LatticeDiagnostic.stl
- MoldSystemDiagnostic.stl
- FinalProjectReport.json

## Input requirements

The current generator requires:

- valid project JSON config
- binary STL input file
- valid output directory
- PicoGK-compatible runtime environment

Important: ASCII STL is not supported by the current PicoGK generator path. Use binary STL files.

## Current pipeline

config JSON -> binary STL validation -> PicoGK mesh analysis -> PicoGK voxel analysis -> DiagnosticMesh.stl -> preliminary Cavity.stl -> preliminary Core.stl -> CoolingDiagnostic.stl -> LatticeDiagnostic.stl -> MoldSystemDiagnostic.stl -> DfAM preliminary report -> FinalProjectReport.json


## Boolean cavity output

The generator now emits:

    BooleanCavity.stl

This file is produced by PicoGK voxel boolean subtraction:

    mold block voxels - part voxels

This is more functional than the legacy preliminary Cavity.stl artifact, but it is still preliminary and not production-certified.

During the current transition period, the generator emits both:

    Cavity.stl
    BooleanCavity.stl

Cavity.stl is kept for compatibility. BooleanCavity.stl is the newer functional-preliminary cavity artifact.

## Important limitations

The current generated STL files are preliminary diagnostic artifacts.

They are not production-ready injection mold tooling.

The generator does not yet implement:

- true production cavity subtraction
- true core extraction
- optimized parting line
- shutoff surfaces
- slides or lifters
- runner, gate, or sprue system
- validated ejector system
- validated cooling channels
- collision checks
- thermal simulation
- pressure-drop simulation
- stress simulation
- wall-thickness geometric solving
- manufacturing certification

A qualified mold engineer must review and validate any tooling before manufacturing.

## Main developer commands

Run baseline:

powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\verify-baseline.ps1"

Publish and verify generator:

powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\verify-generator-publish.ps1"

## Next recommended development track

1. Extend config schema for cooling, lattice, mold-system, and DfAM parameters.
2. Replace preliminary defaults in GeneratorPipelineRunner.
3. Add export manifest file-existence and file-size validation.
4. Add --clean-output.
5. Add --output <path> override.
6. Add real user STL sample workflow documentation.
7. Add installer or release packaging.
