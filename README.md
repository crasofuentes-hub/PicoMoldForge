# PicoMoldForge

PicoMoldForge is a preliminary injection-mold generation pipeline built with .NET and PicoGK.

It includes a publishable Windows command-line generator that reads a project JSON config and a binary STL input file, then produces a preliminary engineering output package.

## Current status

- Generator executable: available
- PicoGK integration: active
- End-to-end sample: available
- Verified baseline: 105 tests passing
- License: MIT

## What it generates

The current generator produces:

- DiagnosticMesh.stl
- Cavity.stl
- Core.stl
- CoolingDiagnostic.stl
- LatticeDiagnostic.stl
- MoldSystemDiagnostic.stl
- FinalProjectReport.json

## Important limitation

The current STL outputs are preliminary diagnostic engineering artifacts.

They are not production-certified injection mold tooling.

The system does not yet implement full production cavity subtraction, optimized parting lines, shutoff surfaces, gates, runners, sprues, validated ejector mechanisms, thermal simulation, pressure-drop simulation, collision checks, or manufacturing certification.

A qualified mold engineer must review and validate any tooling before manufacturing.

## Requirements

- Windows
- .NET SDK 8/9 environment used by the solution
- PicoGK dependency resolved by the project
- Binary STL input files

ASCII STL is not supported by the current PicoGK generator path.

## Publish the generator

From the repository root:

    Set-Location "C:\repos\PicoMoldForge"

    dotnet publish ".\src\PicoMoldForge.Generator\PicoMoldForge.Generator.csproj" -c Release -r win-x64 --self-contained false -o ".\publish\PicoMoldForge.Generator"

The executable is created at:

    publish\PicoMoldForge.Generator\PicoMoldForge.Generator.exe

## Run the generator

    Set-Location "C:\repos\PicoMoldForge"

    .\publish\PicoMoldForge.Generator\PicoMoldForge.Generator.exe --config ".\samples\generator-valid-project.json" --generate-all

Expected output directory:

    samples\generated\generator-sample

## Verify publish and sample generation

    Set-Location "C:\repos\PicoMoldForge"

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\verify-generator-publish.ps1"

## Run baseline

    Set-Location "C:\repos\PicoMoldForge"

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\verify-baseline.ps1"

## Project documentation

Additional documentation is available in:

- docs/GENERATOR_USAGE.md
- docs/V5_FINAL_ROADMAP_STATUS.md
- phase status documents under docs/

## Next development track

The next major development step is to move from preliminary diagnostic geometry toward functional mold geometry:

- real boolean cavity/core generation
- cooling channel subtraction
- runner/gate/sprue generation
- ejector/vent/insert collision checks
- draft, undercut, and wall-thickness analysis
- stronger manufacturing validation reports

## License

MIT License. See LICENSE.