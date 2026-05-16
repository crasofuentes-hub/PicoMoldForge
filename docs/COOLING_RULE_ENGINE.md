# CoolingRuleEngine v1

## Purpose

CoolingRuleEngine converts expert cooling-design rules into machine-readable engineering issues.

It evaluates preliminary cooling-channel and thermal-risk indicators, including channel distance, local wall-thickness jumps, cooling balance, and relative cooling-time risk.

The engine is based on:

    docs/EXPERT_INJECTION_MOLD_RULES_V1.md

## Current Inputs

CoolingRuleInput includes:

- CheckType
- ActualValue
- IsCosmeticCritical
- IsCriticalToQuality
- HasEngineerOverride

## Supported Check Types

Current supported checks:

- ChannelDistanceToCavityDiameterRatio
- LocalThicknessJumpRatio
- CoolingBalanceDeltaRatio
- RelativeCoolingTimeRatio

## Rule Pack Version

Current source rule pack:

    expert-injection-mold-rules.v1

## Current Rules

### Channel Distance to Cavity

Cooling channel distance from the cavity surface should generally be:

    1.0x to 1.5x channel diameter

Warning threshold:

    above 1.5x channel diameter

Fail threshold on cosmetic or critical conditions:

    above 2.5x channel diameter

This is a preliminary check for hot spots and cooling inefficiency.

### Local Thickness Jump

Local wall-thickness jumps should generally stay below:

    30 percent

Warning threshold:

    above 30 percent

Fail threshold on cosmetic or critical conditions:

    above 50 percent

This is a preliminary risk signal for sink, warp, cooling imbalance, and long cycle time.

### Cooling Balance Delta

Cooling should be reasonably balanced across circuits, regions, or cavities.

Recommended maximum relative delta:

    15 percent

Warning threshold:

    above 15 percent

Fail threshold on cosmetic or critical conditions:

    above 30 percent

This is a preliminary check for warp mismatch and uneven cooling.

### Relative Cooling Time

Cooling time scales approximately with wall thickness squared.

The engine exposes:

    relativeCoolingTimeRatio = (localWallThickness / nominalWallThickness)^2

Example:

    localWallThickness = 4.0 mm
    nominalWallThickness = 2.0 mm
    relativeCoolingTimeRatio = 4.0

Recommended maximum:

    2.25x

Warning threshold:

    above 2.25x

Critical review threshold:

    around 4.0x or higher

## Severity Behavior

PASS:

- value is inside the expert preliminary range.

WARNING:

- channel distance is outside the recommended range;
- local thickness jump exceeds 30 percent;
- cooling balance delta exceeds 15 percent;
- relative cooling time ratio exceeds the recommended range.

FAIL:

- cosmetic or critical condition exceeds the fail threshold;
- value is negative.

NEEDS_ENGINEER_REVIEW:

- explicit engineer override is present.

## Limitations

This is a preliminary engineering rule engine.

It does not replace:

- mold-flow analysis;
- thermal simulation;
- coolant-flow analysis;
- pressure-drop analysis;
- qualified mold engineer review;
- actual mold trial data.

Cooling validation depends on:

- plastic material;
- wall thickness;
- gate location;
- packing pressure;
- mold steel;
- coolant temperature;
- flow rate;
- channel diameter;
- channel length;
- baffles, bubblers, and conformal cooling strategy;
- number of cavities;
- cycle-time target.

## Next Steps

Future improvements should include:

- real cooling-channel geometry extraction;
- channel-to-cavity clearance from generated geometry;
- channel-to-ejector clearance;
- channel-to-runner/gate/sprue clearance;
- cooling-channel subtraction from mold halves;
- thermal hotspot scoring;
- integration into FinalProjectReport.json;
- combined interaction with wall, shrinkage, gate, and ejection rules.