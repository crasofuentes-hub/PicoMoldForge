# Engineering Issue Contracts

## Purpose

PicoMoldForge uses engineering issue contracts to convert expert mold-design rules into machine-readable validation results.

These contracts are the foundation for future rule engines such as:

- DraftRuleEngine
- ShrinkageRuleEngine
- WallFeatureRuleEngine
- CoolingRuleEngine
- GateRuleEngine
- EjectorRuleEngine
- VentingRuleEngine
- SteelSafeRuleEngine
- MoldBaseRuleEngine

The issue model allows the generator and reports to express engineering findings in a consistent format.

## Core Contracts

Current code contracts:

- EngineeringSeverity
- EngineeringIssue
- EngineeringRuleResult
- EngineeringIssueFactory

Location:

    src/PicoMoldForge.Core/Engineering/

Tests:

    tests/PicoMoldForge.Core.Tests/Engineering/

## EngineeringSeverity

Supported severity values:

| Severity | Meaning |
|---|---|
| Pass | The rule passes. |
| Info | Informational finding. |
| Warning | Risk exists, but the condition may be acceptable with review. |
| Fail | High-risk condition or rule violation. |
| NeedsEngineerReview | Automated evaluation is insufficient; qualified review is required. |

## EngineeringIssue Fields

Each issue includes:

| Field | Purpose |
|---|---|
| RuleId | Stable rule identifier. |
| Severity | PASS, INFO, WARNING, FAIL, or NEEDS_ENGINEER_REVIEW. |
| Category | Rule category such as Draft, Shrinkage, Cooling, Ejector, Venting, SteelSafe, WallFeature, or MoldBase. |
| Message | Human-readable explanation. |
| FeatureType | Optional feature context such as Wall, Rib, Boss, Hole, Shutoff, Gate, CoolingChannel. |
| Material | Optional material context such as ABS, PP, PC, PA, POM, PE, PEEK, or Any. |
| ActualValue | Optional measured or configured value. |
| RequiredValue | Optional minimum required value. |
| RecommendedValue | Optional recommended target value. |
| Unit | Unit for numeric values, such as deg, mm, percent, ratio, or count. |
| CorrectiveAction | Recommended action for the user or engineer. |
| RequiresEngineerReview | Whether qualified engineering review is required. |
| SourceRulePackVersion | Rule pack version, such as expert-injection-mold-rules.v1. |

## Validation Rules

EngineeringIssue.Validate enforces:

- RuleId is required.
- Category is required.
- Message is required.
- CorrectiveAction is required.
- SourceRulePackVersion is required.
- Unit is required when ActualValue, RequiredValue, or RecommendedValue is present.
- RequiresEngineerReview must be true when Severity is NeedsEngineerReview.

## EngineeringRuleResult

EngineeringRuleResult groups issues from one rule engine or rule pack.

It computes:

- PassCount
- InfoCount
- WarningCount
- FailureCount
- EngineerReviewCount
- HasFailures
- RequiresEngineerReview
- HighestSeverity

This lets reports summarize engineering validation results without each rule engine inventing its own status model.

## Example Draft Warning

    RuleId: draft.wall.abs.smooth.minimum
    Severity: Warning
    Category: Draft
    Message: ABS smooth wall draft is below the expert minimum.
    FeatureType: Wall
    Material: ABS
    ActualValue: 0.75
    RequiredValue: 1.0
    RecommendedValue: 1.5
    Unit: deg
    CorrectiveAction: Increase draft angle or document an engineer-approved override.
    RequiresEngineerReview: false
    SourceRulePackVersion: expert-injection-mold-rules.v1

## Example Draft Fail

    RuleId: draft.shutoff.minimum
    Severity: Fail
    Category: Draft
    Message: Shutoff draft is below the expert minimum.
    FeatureType: Shutoff
    Material: Any
    ActualValue: 0.0
    RequiredValue: 3.0
    RecommendedValue: 4.0
    Unit: deg
    CorrectiveAction: Increase shutoff draft to at least 3 degrees.
    RequiresEngineerReview: true
    SourceRulePackVersion: expert-injection-mold-rules.v1

## Relationship to Expert Rules

The expert rule pack is documented in:

    docs/EXPERT_INJECTION_MOLD_RULES_V1.md

That document provides codifiable mold engineering rules for:

- draft
- shrinkage
- gates
- cooling
- ejectors
- venting
- steel-safe allowance
- walls and features
- mold base assumptions

The engineering issue contracts are the common output format that future rule engines will use when applying those rules.

## Next Implementation Step

The next implementation phase should be:

    DraftRuleEngine v1

It should consume:

- material
- surface type
- feature type
- actual draft angle
- texture depth
- feature depth
- cosmetic criticality
- override flags

It should output:

- EngineeringRuleResult
- EngineeringIssue records
- PASS, WARNING, FAIL, or NEEDS_ENGINEER_REVIEW findings