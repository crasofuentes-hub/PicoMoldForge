# PicoMoldForge v5 - Phase 2A PicoGK Inspection

## Status

Phase 2A inspected the local environment before implementing any PicoGK-dependent code.

## Local findings

- Installed .NET SDK: 8.0.420.
- PicoGK is not installed as a NuGet package in `src/PicoMoldForge.PicoGK`.
- ShapeKernel is not installed.
- LatticeLibrary is not installed.
- The only local PicoGK-related files are the internal `PicoMoldForge.PicoGK` adapter project and its build outputs.

## External dependency finding

The current PicoGK NuGet package targets .NET 9.0. The official setup documentation also uses .NET SDK 9.0.

## Architecture decision

- Do not implement PicoGK APIs until the real package is installed and inspected.
- Keep `PicoMoldForge.Core`, `PicoMoldForge.Cli`, and `PicoMoldForge.Reporting` on .NET 8 for now.
- Keep `PicoMoldForge.PicoGK` isolated as the future geometry adapter boundary.
- Remove any unused CLI reference to `PicoMoldForge.PicoGK` so the CLI remains independent from future .NET 9 geometry runtime requirements.

## Next required action

Install .NET SDK 9 before adding the PicoGK NuGet package.

After .NET SDK 9 is available, the next controlled step is:

1. Retarget only `src/PicoMoldForge.PicoGK` and `tests/PicoMoldForge.PicoGK.Tests` if required.
2. Add the PicoGK NuGet package only to `src/PicoMoldForge.PicoGK`.
3. Build the adapter project.
4. Inspect real public types and examples.
5. Only then implement mesh or voxel behavior.