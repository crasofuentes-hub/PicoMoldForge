# ShrinkageRuleEngine v1

## Purpose

ShrinkageRuleEngine converts expert shrinkage rules into machine-readable engineering issues.

It evaluates whether a configured shrinkage allowance is present, plausible for the selected material, and appropriate for critical dimensions.

The engine is based on:

    docs/EXPERT_INJECTION_MOLD_RULES_V1.md

## Current Inputs

ShrinkageRuleInput includes:

- Material
- ActualShrinkageRate
- IsCriticalDimension
- UsesDatasheetValue
- HasEngineerOverride

## Supported Materials

Current supported material groups:

- ABS
- PP
- PC
- Nylon/PA
- POM
- PE
- General

## Rule Pack Version

Current source rule pack:

    expert-injection-mold-rules.v1

## Current Shrinkage Ranges

| Material | Minimum | Maximum | Recommended |
|---|---:|---:|---:|
| ABS | 0.004 | 0.007 | 0.005 |
| PP | 0.010 | 0.025 | 0.018 |
| PC | 0.005 | 0.007 | 0.006 |
| Nylon/PA | 0.010 | 0.020 | 0.015 |
| POM | 0.015 | 0.025 | 0.020 |
| PE | 0.015 | 0.030 | 0.022 |

Values are represented as ratios.

Example:

    0.005 = 0.5 percent

## Severity Behavior

PASS:

- shrinkage is present;
- value is inside the expert range;
- value is marked as datasheet-based.

WARNING:

- shrinkage is missing for a non-critical dimension;
- shrinkage is zero or negative for a non-critical dimension;
- shrinkage is not marked as datasheet-based;
- shrinkage is below or above the expert typical range.

FAIL:

- critical dimension has no shrinkage model;
- critical dimension has zero or negative shrinkage allowance.

NEEDS_ENGINEER_REVIEW:

- explicit engineer override is present;
- critical non-datasheet shrinkage requires review;
- unusually high shrinkage requires review.

## Cavity Dimension Formula

The engine exposes:

    cavityDimension = nominalDimension * (1 + shrinkageRate)

Example:

    nominalDimension = 100 mm
    shrinkageRate = 0.005
    cavityDimension = 100.5 mm

## Limitations

This is a preliminary engineering rule engine.

It does not replace resin datasheets, mold-flow simulation, trial data, or qualified mold engineer review.

Shrinkage depends on:

- resin grade
- filler content
- fiber orientation
- moisture conditioning
- wall thickness
- gate location
- packing pressure
- process settings
- mold temperature
- part geometry

## Next Steps

Future improvements should include:

- filler-specific shrinkage rules;
- directional shrinkage;
- material grade presets;
- critical-to-quality dimension tagging;
- integration into FinalProjectReport.json;
- per-feature shrinkage compensation reporting.