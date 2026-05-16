# WallFeatureRuleEngine v1

## Purpose

WallFeatureRuleEngine converts expert wall and feature-size rules into machine-readable engineering issues.

It evaluates wall thickness, rib proportions, boss wall proportions, abrupt thickness changes, and internal radius ratios.

The engine is based on:

    docs/EXPERT_INJECTION_MOLD_RULES_V1.md

## Current Inputs

WallFeatureRuleInput includes:

- Material
- CheckType
- ActualValue
- IsCosmeticCritical
- IsCriticalToQuality
- HasEngineerOverride

## Supported Materials

Current supported material groups:

- ABS
- PC
- PP
- Nylon/PA
- POM
- PE
- General

## Supported Check Types

Current supported checks:

- NominalWallThickness
- RibThicknessRatio
- RibHeightRatio
- BossWallThicknessRatio
- AbruptThicknessJumpRatio
- InternalRadiusRatio

## Rule Pack Version

Current source rule pack:

    expert-injection-mold-rules.v1

## Current Rules

### Nominal Wall Thickness

| Material | Recommended Range | Unit |
|---|---:|---|
| ABS | 2.0 to 3.0 | mm |
| PC | 2.0 to 3.0 | mm |
| PP | 1.0 to 2.5 | mm |
| Nylon/PA | 1.0 to 2.5 | mm |
| General | 1.5 to 3.0 | mm |

### Rib Thickness

Rib thickness should typically be:

    40 percent to 60 percent of parent wall thickness

Warning threshold:

    above 60 percent

Fail threshold on cosmetic or critical conditions:

    above 70 percent

### Rib Height

Rib height should typically be:

    about 3x parent wall thickness

Warning threshold:

    above 3x

Fail threshold on cosmetic or critical conditions:

    above 4x

### Boss Wall Thickness

Boss wall thickness should typically be:

    about 60 percent of nominal wall thickness

Warning threshold:

    above 60 percent

### Abrupt Thickness Jump

Local thickness jumps should generally stay below:

    30 percent

Warning threshold:

    above 30 percent

Fail threshold on cosmetic or critical conditions:

    above 50 percent

### Internal Radius

Internal radius should generally be at least:

    50 percent of wall thickness

Critical sharp-radius fail threshold:

    below 25 percent on critical-to-quality features

## Severity Behavior

PASS:

- value is inside the expert preliminary range.

WARNING:

- wall is below the material minimum;
- feature ratio exceeds the recommended range;
- rib, boss, or local transition may cause sink, warp, long cooling time, or stress concentration;
- internal radius is below the recommended ratio.

FAIL:

- cosmetic or critical wall/feature exceeds fail threshold;
- critical internal radius is too sharp;
- value is negative.

NEEDS_ENGINEER_REVIEW:

- explicit engineer override is present.

## Limitations

This is a preliminary engineering rule engine.

It does not replace qualified mold engineer review, material supplier guidance, mold-flow analysis, or actual tooling trial data.

Wall and feature validation depends on:

- material grade
- flow length
- gate location
- packing pressure
- cosmetic requirements
- stiffness requirements
- sink tolerance
- cooling layout
- mold construction

## Next Steps

Future improvements should include:

- mesh-derived wall thickness analysis;
- automatic rib and boss detection;
- per-region cosmetic classification;
- CTQ tagging;
- integration into FinalProjectReport.json;
- combined interaction with shrinkage, draft, gate, and cooling rules.