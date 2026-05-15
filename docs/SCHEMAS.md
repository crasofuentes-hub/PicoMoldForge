# PicoMoldForge JSON Schemas

PicoMoldForge includes JSON schemas for key machine-readable contracts.

## Schemas

| File | Purpose |
|---|---|
| docs/schemas/picomoldforge.project-config.schema.json | Generator project configuration. |
| docs/schemas/picomoldforge.final-project-report.schema.json | FinalProjectReport.json output. |
| docs/schemas/picomoldforge.run-manifest.schema.json | RunManifest.json output. |

## Current Contract Status

The schemas are intended as public contracts for users, automation, and future tooling.

The project config schema is strict for the currently supported generator sections:

- material
- machine
- moldBlock
- parting
- cooling
- lattice
- moldSystem
- dfam

The RunManifest schema includes artifact integrity metadata:

- FileName
- Path
- SizeBytes
- Sha256

The FinalProjectReport schema is intentionally permissive while the report evolves.

## Schema Versioning

Current schema identifiers:

    picomoldforge.project-config.v1
    picomoldforge.run-manifest.v1

The current sample config does not require schemaVersion yet to preserve compatibility, but future configs should add it when schema enforcement becomes strict.

## Verification

Run:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\verify-schemas.ps1"

Full verification still runs through:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\verify-generator-publish.ps1"
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\verify-baseline.ps1"

## Limitations

The schema verification script currently performs structural sanity checks and JSON parsing. Full JSON Schema validation may be added later with a dedicated validator dependency.