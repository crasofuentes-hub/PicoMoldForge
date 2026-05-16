# EjectorRuleEngine v1

## Purpose

EjectorRuleEngine converts expert ejector-design rules into machine-readable engineering issues.

It evaluates preliminary ejector-pin and ejection-risk indicators, including pin land clearance, placement risk, local draft at the ejector location, and ejector concentration.

The engine is based on:

    docs/EXPERT_INJECTION_MOLD_RULES_V1.md

## Current Inputs

EjectorRuleInput includes:

- CheckType
- ActualValue
- IsCosmeticSurface
- IsThinWall
- IsCriticalToQuality
- HasEngineerOverride

## Supported Check Types

Current supported checks:

- PinLandClearanceMm
- SurfacePlacementRiskScore
- DraftAtEjectorLocationDeg
- EjectorConcentrationRatio

## Rule Pack Version

Current source rule pack:

    expert-injection-mold-rules.v1

## Current Rules

### Pin Land Clearance

Ejector pin land clearance should generally be:

    0.02 mm to 0.05 mm

Warning conditions:

    below 0.02 mm
    above 0.05 mm

Fail condition on cosmetic, thin-wall, or critical features:

    below 0.01 mm
    above 0.10 mm

Too-tight clearance risks galling. Too-loose clearance risks flash.

### Surface Placement Risk

Preferred ejector locations:

    stiff
    non-cosmetic
    thick-supported areas

Avoid:

    cosmetic faces
    thin walls
    fragile ribs
    unsupported flat regions
    low-draft areas

Current risk score behavior:

    pass at or below 0.30
    warning above 0.30
    fail above 0.70 on cosmetic, thin-wall, or critical conditions

### Draft at Ejector Location

Local draft near ejector areas should generally be at least:

    0.50 degrees

Warning condition:

    below 0.50 degrees

Fail condition on thin-wall, cosmetic, or critical conditions:

    below 0.25 degrees

### Ejector Concentration

High ejector concentration can mark, deform, or locally overstress the part.

Current preliminary behavior:

    pass at or below 0.50
    warning above 0.50
    fail above 0.75 on critical, thin-wall, or cosmetic conditions

## Severity Behavior

PASS:

- value is inside the expert preliminary range.

WARNING:

- clearance is outside the recommended range;
- placement risk is elevated;
- local draft is low;
- ejector concentration is elevated.

FAIL:

- cosmetic, thin-wall, or critical condition exceeds a fail threshold;
- validation value is negative.

NEEDS_ENGINEER_REVIEW:

- explicit engineer override is present.

## Limitations

This is a preliminary engineering rule engine.

It does not replace:

- qualified mold engineer review;
- actual ejection-force calculation;
- deformation simulation;
- mold-trial data;
- cosmetic approval;
- final tooling review.

Ejector validation depends on:

- material stiffness;
- part depth;
- draft;
- texture;
- wall thickness;
- geometry support;
- cosmetic requirements;
- pin diameter;
- ejection stroke;
- mold construction;
- cooling layout.

## Next Steps

Future improvements should include:

- automatic ejector candidate generation;
- ejector-to-cooling clearance checks;
- ejector-to-gate/runner clearance checks;
- visible ejector mark reporting;
- ejection-force estimation;
- integration into FinalProjectReport.json;
- combined interaction with draft, wall, cooling, gate, and venting rules.