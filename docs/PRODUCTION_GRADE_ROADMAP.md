# PicoMoldForge Production-Grade Roadmap

## Current Status

PicoMoldForge is currently a mature prototype with:

- publishable generator CLI
- JSON configuration
- output override
- clean-output mode
- output path safety guard
- generated STL and JSON artifacts
- FinalProjectReport.json
- RunManifest.json
- artifact SHA256 checksums
- JSON schema contracts
- expert injection mold rule documentation
- engineering issue contracts
- verified baseline

Current baseline:

- 165 tests passing after DraftRuleEngine v1
- generator publish verification passing
- schema verification passing
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
- PASS, INFO, WARNING, FAIL, and NEEDS_ENGINEER_REVIEW issues
- engineer-review workflow
- documented limitations
- release-ready packaging

## Completed Product Hardening Phases

### Phase 22A - Preserve expert engineering rules - DONE

PicoMoldForge captures expert-provided injection mold rules in:

- docs/EXPERT_INJECTION_MOLD_RULES_V1.md

### Phase 22B - SHA256 checksums in RunManifest.json - DONE

RunManifest.json artifact entries include:

- FileName
- Path
- SizeBytes
- Sha256

### Phase 22C - Document RunManifest SHA256 integrity - DONE

RunManifest SHA256 integrity is documented in README and generator usage docs.

### Phase 23A - JSON schemas - DONE

PicoMoldForge includes schemas for:

- project config
- FinalProjectReport.json
- RunManifest.json

### Phase 23B - Document JSON schema contracts - DONE

Schema contracts are documented in:

- docs/SCHEMAS.md

### Phase 24A - CLI and path safety - DONE

PicoMoldForge refuses unsafe output targets before generation starts.

Rejected output targets include:

- filesystem roots
- current working directory
- config directory
- user profile root
- Program Files root
- system directory
- existing file paths

### Phase 24B - Document output path safety - DONE

Output path safety is documented in README, generator usage docs, and this roadmap.

### Phase 25A - Engineering rule engine foundation - DONE

PicoMoldForge includes base engineering contracts:

- EngineeringSeverity
- EngineeringIssue
- EngineeringRuleResult
- EngineeringIssueFactory

These are located in:

- src/PicoMoldForge.Core/Engineering/

### Phase 25B - Document engineering issue contracts - DONE

PicoMoldForge documents the engineering issue model used to convert expert mold-design rules into machine-readable validation results.

Documentation:

- docs/ENGINEERING_ISSUES.md

The contracts support:

- PASS
- INFO
- WARNING
- FAIL
- NEEDS_ENGINEER_REVIEW

Each issue can carry:

- RuleId
- Category
- FeatureType
- Material
- ActualValue
- RequiredValue
- RecommendedValue
- Unit
- CorrectiveAction
- RequiresEngineerReview
- SourceRulePackVersion

## Next Implementation Target

### Phase 26B - Document DraftRuleEngine v1

Document the first implemented rule engine.

### Phase 27A - ShrinkageRuleEngine v1

Implement shrinkage compensation checks from expert rules.

### Phase 28A - WallFeatureRuleEngine v1

Implement wall, rib, boss, radius, and abrupt-thickness checks.

### Phase 29A - CoolingRuleEngine v1

Implement cooling distance and thickness-ratio checks.

### Phase 30A - Gate, Ejector, Venting, SteelSafe rule packs

Implement preliminary rule evaluation from expert tables.

## Mid-Term Engineering Roadmap

- mesh validation
- draft angle analyzer from STL normals
- undercut detection
- parting strategy report
- cooling channel subtraction
- ejector placement candidate generation
- vent placement candidate generation
- gate, runner, and sprue preliminary generation
- collision and clearance report
- HTML engineering report
- packaged release ZIP
- GitHub Actions CI

## Production-Grade Definition

PicoMoldForge is production-grade when it can safely accept real user configurations, validate geometry and project assumptions, generate repeatable output packages, report risks clearly, preserve run evidence, avoid destructive CLI behavior, and support expert engineering review.

It is not production-grade merely because it generates STL files.

### Phase 26A - DraftRuleEngine v1 - DONE

PicoMoldForge now implements the first codified engineering rule engine.

DraftRuleEngine v1 evaluates:

- material
- surface type
- feature type
- actual draft angle
- texture depth
- feature depth
- cosmetic criticality
- engineer override flags

It outputs:

- EngineeringRuleResult
- EngineeringIssue records
- PASS, WARNING, FAIL, or NEEDS_ENGINEER_REVIEW findings

The implementation is based on the expert injection mold rule pack documented in:

- docs/EXPERT_INJECTION_MOLD_RULES_V1.md

Current verified baseline after this phase:

- 165 tests passing
