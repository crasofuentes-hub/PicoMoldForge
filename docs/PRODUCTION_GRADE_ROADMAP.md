# PicoMoldForge Production-Grade Roadmap

## Current Status

PicoMoldForge is currently a mature prototype with:

- publishable generator CLI
- JSON configuration
- output override
- clean-output mode
- generated STL/JSON artifacts
- FinalProjectReport.json
- RunManifest.json
- verified baseline

Current baseline:

- 139 tests passing
- generator publish verification passing
- CLI supports --generate-all, --clean-output, --output, --self-test, and --help

## Production-Grade Goal

PicoMoldForge should become a serious preliminary injection mold engineering generator.

It must not claim to produce certified production-ready molds automatically.

Production-grade means:

- safe CLI behavior
- auditable runs
- validated configs
- repeatable output packages
- explicit engineering warnings
- codified expert rules
- PASS/WARNING/FAIL issues
- engineer-review workflow
- documented limitations
- release-ready packaging

## Immediate Roadmap

### Phase 22A Ã¢â‚¬â€ Preserve expert engineering rules

Create repository documentation for:

- expert-provided injection mold rules
- production-grade roadmap
- implementation sequence

### Phase 22B Ã¢â‚¬â€ SHA256 checksums in RunManifest.json

Add SHA256 for every generated artifact.

Expected manifest artifact fields:

- FileName
- Path
- SizeBytes
- Sha256

### Phase 23A Ã¢â‚¬â€ JSON schemas

Add schemas for:

- PicoMoldForge project config
- FinalProjectReport.json
- RunManifest.json

Schemas must include versioning.

### Phase 24A Ã¢â‚¬â€ CLI and path safety

Harden:

- --clean-output
- --output
- invalid paths
- destructive root paths
- missing permissions
- exit codes

### Phase 25A Ã¢â‚¬â€ Engineering rule engine foundation

Create generic issue/reporting contracts:

- EngineeringIssue
- EngineeringSeverity
- EngineeringRuleResult
- RulePackVersion
- CorrectiveAction
- RequiresEngineerReview

### Phase 25B Ã¢â‚¬â€ DraftRuleEngine v1

Implement codified expert draft rules.

### Phase 26A Ã¢â‚¬â€ ShrinkageRuleEngine v1

Implement shrinkage compensation checks.

### Phase 27A Ã¢â‚¬â€ WallFeatureRuleEngine v1

Implement wall, rib, boss, radius, and abrupt-thickness checks.

### Phase 28A Ã¢â‚¬â€ CoolingRuleEngine v1

Implement cooling distance and thickness-ratio checks.

### Phase 29A Ã¢â‚¬â€ Gate/Ejector/Venting/SteelSafe rule packs

Implement preliminary rule evaluation from expert tables.

## Mid-Term Engineering Roadmap

- Mesh validation
- Draft angle analyzer from STL normals
- Undercut detection
- Parting strategy report
- Cooling channel subtraction
- Ejector placement candidate generation
- Vent placement candidate generation
- Gate/runner/sprue preliminary generation
- Collision/clearance report
- HTML engineering report
- Packaged release ZIP
- GitHub Actions CI

## Production-Grade Definition

PicoMoldForge is production-grade when it can safely accept real user configurations, validate geometry and project assumptions, generate repeatable output packages, report risks clearly, preserve run evidence, avoid destructive CLI behavior, and support expert engineering review.

It is not production-grade merely because it generates STL files.
### Phase 22C â€” Document RunManifest SHA256 integrity â€” DONE

RunManifest.json artifact entries now include:

- FileName
- Path
- SizeBytes
- Sha256

The publish verification script recomputes artifact hashes and fails if any stored checksum does not match the generated file.

### Phase 23B — Document JSON schema contracts — DONE

PicoMoldForge now documents JSON schema contracts for:

- project config
- FinalProjectReport.json
- RunManifest.json

The schemas are located in:

- docs/schemas/picomoldforge.project-config.schema.json
- docs/schemas/picomoldforge.final-project-report.schema.json
- docs/schemas/picomoldforge.run-manifest.schema.json

The schema documentation is located in:

- docs/SCHEMAS.md

Verification is handled by:

- scripts/verify-schemas.ps1

RunManifest schema includes artifact SHA-256 integrity metadata.
