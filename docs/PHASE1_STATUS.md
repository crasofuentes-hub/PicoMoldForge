# PicoMoldForge v5 - Phase 1 Status

## Status

Phase 1 is closed as a verified input-contract phase.

## Verified capabilities

- The solution builds on .NET 8.
- The baseline script exists at scripts/verify-baseline.ps1.
- The CLI supports --self-test.
- The CLI supports --config <path> --validate-config.
- JSON project configuration can be loaded.
- Configuration validation covers project name, input path, output directory, mold mode, mold standard, voxel resolution, material profile, and machine profile.
- STL is the first functional part input format.
- STEP and STP are recognized but require an external converter.
- Unsupported input extensions fail with a clear error.
- Missing STL files fail with a clear error.

## Explicit limitations

- PicoGK is not invoked yet.
- ShapeKernel is not invoked yet.
- LatticeLibrary is not invoked yet.
- Mesh loading is not implemented yet.
- Voxel conversion is not implemented yet.
- Native STEP import is not implemented.
- STEP conversion is only represented by IPartInputConverter.
- No mold geometry is generated yet.
- No cavity, core, cooling, lattice, mold base, ejection, vents, inserts, DfAM, or export geometry exists yet.
- Generated geometry is preliminary and not certified for production manufacturing.

## Current baseline

Required baseline command:

Set-Location "C:\repos\PicoMoldForge"
.\scripts\verify-baseline.ps1

The baseline must continue to execute:

- dotnet restore
- dotnet build
- dotnet test
- dotnet run --project src/PicoMoldForge.Cli -- --self-test

## Next phase

Phase 2 must begin with inspection of real PicoGK APIs, examples, local documentation, or installed package signatures before implementing any PicoGK-dependent mesh or voxel behavior.