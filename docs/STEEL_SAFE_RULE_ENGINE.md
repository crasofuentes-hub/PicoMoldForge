# SteelSafeRuleEngine v1

SteelSafeRuleEngine converts expert steel-safe allowance rules into machine-readable EngineeringIssue results.

## Inputs

- CheckType
- ActualValue
- IsCriticalToQuality
- IsCosmeticSurface
- HasEngineerOverride

## Supported Checks

- GeneralAllowanceMm
- CriticalDimensionAllowanceMm
- ShutoffAllowanceMm
- PartingLineAllowanceMm
- CosmeticReworkRiskScore

## Current Rules

Steel-safe allowance should commonly be:

    0.10 mm to 0.50 mm

Critical dimensions, shutoffs, and parting-line tuning require a steel-safe path.

Cosmetic rework risk behavior:

    pass <= 0.30
    warning > 0.30
    fail > 0.70 on cosmetic or critical conditions

## Output

The engine emits:

- EngineeringRuleResult
- EngineeringIssue
- PASS
- WARNING
- FAIL
- NEEDS_ENGINEER_REVIEW

## Limitation

This is preliminary engineering validation. It does not replace qualified mold engineer review, moldmaker feedback, trial tuning, final tool review, or cosmetic signoff.