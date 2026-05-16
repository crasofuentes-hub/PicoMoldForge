# VentingRuleEngine v1

## Purpose

VentingRuleEngine converts expert venting rules into machine-readable engineering issues.

It evaluates preliminary venting indicators, including vent depth, vent width, vent land length, end-of-fill vent risk, and gas-trap risk.

The engine is based on:

    docs/EXPERT_INJECTION_MOLD_RULES_V1.md

## Current Inputs

VentingRuleInput includes:

- CheckType
- ActualValue
- IsFlashSensitive
- IsLongFlowPath
- IsCriticalToQuality
- HasEngineerOverride

## Supported Check Types

Current supported checks:

- VentDepthMm
- VentWidthMm
- VentLandLengthMm
- EndOfFillVentRiskScore
- GasTrapRiskScore

## Rule Pack Version

Current source rule pack:

    expert-injection-mold-rules.v1

## Current Rules

### Vent Depth

Vent depth should generally be:

    0.02 mm to 0.05 mm

Warning conditions:

    below 0.02 mm
    above 0.05 mm

Fail conditions:

    below 0.01 mm on long-flow or critical conditions
    above 0.08 mm on flash-sensitive or critical conditions

Too-shallow vents may trap gas. Too-deep vents may cause flash.

### Vent Width

Vent width is resin and tool dependent.

Current preliminary range:

    0.20 mm minimum
    0.50 mm recommended minimum
    5.00 mm recommended maximum

Warning conditions:

    below 0.20 mm
    above 5.00 mm

Fail conditions:

    below 0.10 mm on long-flow or critical conditions
    above 10.00 mm on flash-sensitive or critical conditions

### Vent Land Length

Vent land length should generally be enough to control flash while allowing gas escape.

Current preliminary range:

    0.50 mm minimum
    0.80 mm recommended minimum
    2.00 mm recommended maximum

Warning conditions:

    below 0.50 mm
    above 2.00 mm

Fail conditions:

    below 0.20 mm on long-flow or critical conditions
    above 5.00 mm on flash-sensitive or critical conditions

### End-of-Fill Vent Risk

Vents should exist at end-of-fill and likely trapped-air areas.

Current risk score behavior:

    pass at or below 0.30
    warning above 0.30
    fail above 0.70 on critical conditions

### Gas-Trap Risk

Gas-trap risk increases around:

- bosses
- ribs
- shutoff corners
- inserts
- long flow-path endpoints
- isolated pockets

Current risk score behavior:

    pass at or below 0.30
    warning above 0.30
    fail above 0.70 on critical conditions

## Severity Behavior

PASS:

- value is inside the expert preliminary range.

WARNING:

- vent depth, width, or land length is outside the recommended range;
- end-of-fill vent risk is elevated;
- gas-trap risk is elevated.

FAIL:

- long-flow, flash-sensitive, or critical condition exceeds a fail threshold;
- validation value is negative.

NEEDS_ENGINEER_REVIEW:

- explicit engineer override is present.

## Limitations

This is a preliminary engineering rule engine.

It does not replace:

- qualified mold engineer review;
- mold-flow analysis;
- resin-specific venting guidance;
- flash-risk validation;
- burn-mark validation;
- short-shot validation;
- tooling trial data.

Venting validation depends on:

- plastic material;
- melt viscosity;
- flow length;
- gate location;
- injection speed;
- clamp force;
- parting-line quality;
- tool steel fit;
- wall thickness;
- rib and boss layout;
- shutoff geometry;
- inserts;
- process window.

## Next Steps

Future improvements should include:

- automatic end-of-fill detection;
- gas-trap detection from flow-path heuristics;
- vent candidate generation;
- parting-line vent strategy;
- overflow well strategy;
- integration into FinalProjectReport.json;
- combined interaction with gate, cooling, wall, and ejector rules.