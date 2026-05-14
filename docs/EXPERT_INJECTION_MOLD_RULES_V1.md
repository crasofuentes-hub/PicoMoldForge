# Expert Injection Mold Rules v1

## Purpose

This document captures expert-provided injection mold engineering rules for PicoMoldForge.

These rules are intended to become:

- software validations
- report warnings
- FAIL checks
- engineer-review prompts
- geometry-generation constraints
- future rule-pack JSON/CSV data

PicoMoldForge must treat these rules as preliminary engineering guidance, not as manufacturing certification.

## Severity Convention

- PASS: within acceptable range.
- WARNING: likely manufacturable, but risky or dependent on molder/engineer approval.
- FAIL: high risk of defect, mold damage, ejection failure, flash, short shot, excessive warpage, or practical infeasibility.
- NEEDS_ENGINEER_REVIEW: cannot be safely decided automatically.
- OVERRIDE_ALLOWED: engineer or molder may override with documented rationale.

## Unit Convention

Internal validation should normalize values to:

- millimeters for length
- degrees for draft
- percentages as decimal values in code when needed
- boolean flags for overrides and manual approvals

---

# 1. Draft Rules

## General Draft Convention

Draft angle is measured in degrees per side relative to the mold opening direction.

As feature depth and texture depth increase, required draft should increase.

Interior surfaces, core pins, shutoffs, textured surfaces, cosmetic surfaces, and deep features usually require more draft than shallow smooth exterior walls.

## Base Material Draft Rules

| Material | SurfaceType | FeatureType | MinimumDraftDeg | RecommendedDraftDeg | WarningBelowDeg | FailBelowDeg | Notes |
|---|---|---|---:|---|---:|---:|---|
| ABS | Smooth | Wall | 1.0 | 1.5-2.0 | 1.0 | 0.5 | General-purpose baseline. |
| ABS | TexturedLight | Wall | 3.0 | 3.0-4.0 | 3.0 | 2.0 | Light texture typically needs 3 degrees. |
| ABS | TexturedHeavy | Wall | 5.0 | 5.0-7.0 | 5.0 | 3.5 | Heavy texture may need 5 degrees or more. |
| PP | Smooth | Wall | 1.0 | 1.5-2.0 | 1.0 | 0.5 | Common baseline. |
| PP | TexturedLight | Wall | 3.0 | 3.0-4.5 | 3.0 | 2.0 | Texture dominates the requirement. |
| PP | TexturedHeavy | Wall | 5.0 | 5.0-7.0 | 5.0 | 3.5 | Heavy texture and deep walls need more draft. |
| PC | Smooth | Wall | 1.5 | 2.0 | 1.5 | 1.0 | More conservative for cosmetic surfaces. |
| PC | TexturedLight | Wall | 3.0 | 3.5-4.0 | 3.0 | 2.0 | Avoid drag marks. |
| PC | TexturedHeavy | Wall | 5.0 | 5.0-8.0 | 5.0 | 3.5 | Some finishes may need 10 degrees or more. |
| Nylon/PA | Smooth | Wall | 0.5 | 1.0-1.5 | 0.5 | 0.25 | More tolerant, but should not be zero. |
| Nylon/PA | TexturedLight | Wall | 2.5 | 3.0-4.0 | 2.5 | 1.5 | Texture increases ejection friction. |
| Nylon/PA | TexturedHeavy | Wall | 4.5 | 5.0-7.0 | 4.5 | 3.0 | High draft is safer. |
| POM | Smooth | Wall | 0.5 | 1.0-1.5 | 0.5 | 0.25 | Often molds well, but still needs draft. |
| PE | Smooth | Wall | 0.5 | 1.0-1.5 | 0.5 | 0.25 | Flexible material, but zero draft is still poor practice. |
| PVC | Smooth | Wall | 0.5 | 1.0-1.5 | 0.5 | 0.25 | Standard baseline. |
| PEEK | Smooth | Wall | 1.0 | 1.5-2.0 | 1.0 | 0.5 | Use conservative values for high-performance polymers. |

## Feature Draft Rules

| Material | SurfaceType | FeatureType | MinimumDraftDeg | RecommendedDraftDeg | WarningBelowDeg | FailBelowDeg | Notes |
|---|---|---|---:|---|---:|---:|---|
| Any | Smooth | RibSidewall | 0.5 | 0.5-1.0 | 0.5 | 0.25 | Typical rib draft. |
| Any | Smooth | BossOuterWall | 0.5 | 0.5-1.5 | 0.5 | 0.25 | Exterior boss draft. |
| Any | Smooth | BossInnerHoleCorePin | 0.25 | 0.5 | 0.25 | 0.1 | Core pin draft reduces sticking and pin damage. |
| Any | Smooth | HoleCorePin | 0.25 | 0.5-1.0 | 0.25 | 0.1 | Deeper holes need more taper. |
| Any | Smooth | DeepWallOver25mm | 1.0 | 1.5-2.0 | 1.0 | 0.5 | Increase draft with depth. |
| Any | Smooth | Shutoff | 3.0 | 3.0-5.0 | 3.0 | 2.0 | Metal-on-metal shutoffs require high draft. |

## Texture Depth Increment Rules

Recommended software bands:

| TextureDepthMm | DraftIncrementDeg | Notes |
|---:|---:|---|
| 0.01-0.03 | 0.5 | Fine texture. |
| 0.03-0.06 | 1.0 | Light to medium texture. |
| 0.06-0.10 | 1.5 | Medium/heavy texture. |
| >0.10 | 2.0 | Requires manual engineering review. |

Conservative alternative:

    textureDraftAddDeg = 1.5 to 2.0 degrees per 0.025 mm texture depth

## Depth Increment Rule

For deep features:

    add approximately 1.0 degree per 25 mm to 50 mm of feature depth

Use conservative behavior for:

- deep walls
- deep holes
- rigid materials
- cosmetic parts
- textured features

## Draft Formula

    requiredDraftDeg =
        baseMaterialDraftDeg
      + textureIncrementDeg
      + depthIncrementDeg
      + featureIncrementDeg

Then enforce:

    requiredDraftDeg = max(requiredDraftDeg, featureMinimumDraftDeg)

For shutoffs:

    requiredDraftDeg = max(requiredDraftDeg, 3.0)

Failure rules:

    if actualDraftDeg <= 0 and feature is a standard mold-release feature:
        FAIL

    if actualDraftDeg < failBelowDeg:
        FAIL

    if actualDraftDeg < requiredDraftDeg:
        WARNING

---

# 2. Shrinkage Rules

## Shrinkage Table

| MaterialGroup | Process | TypicalShrinkageAllowance | DesignRule | SeverityIfViolated | Notes |
|---|---|---|---|---|---|
| ABS | InjectionMolding | 0.4%-0.7% | Use cavity oversize equal to expected shrinkage. | WARNING if no allowance; FAIL if 0% on critical dimensions. | Use data sheet first, then tune by trial. |
| PP | InjectionMolding | 1.0%-2.5% | Higher allowance than ABS; validate by moldflow or trial. | WARNING if not applied; FAIL if critical dimension ignored. | PP typically shrinks more than ABS. |
| PC | InjectionMolding | 0.5%-0.7% | Use conservative nominal plus steel-safe on CTQs. | WARNING if no process compensation. | Cosmetic and dimensional stability matter. |
| PA/Nylon | InjectionMolding | 1.0%-2.0% dry; higher if moisture/fiber variant. | Adjust for conditioning and glass content. | WARNING if resin conditioning ignored; FAIL if generic 0.5%. | Moisture and fiber change shrinkage significantly. |
| POM | InjectionMolding | 1.5%-2.5% | Large shrinkage compensation required. | WARNING if below resin data; FAIL if not compensated. | Use resin-specific supplier data. |
| PE | InjectionMolding | 1.5%-3.0% | Highest caution on dimensional control. | WARNING if generic low-shrink assumption used. | Flexible materials can move more after ejection. |
| General | Any | Per datasheet | Set cavity = nominal x (1 + shrinkage). | FAIL if critical dimensions have no shrink model. | Best practice is resin sheet plus trial tuning. |

## Shrinkage Formula

    cavityDimension = nominalDimension * (1 + shrinkageRate)

For steel-safe tuning:

    steelSafeExtraMm = 0.1 to 0.5 on critical trims/features

For tight CTQs:

    designShrink = datasheetShrink + 0.1% to 0.3%

unless otherwise specified by the engineer.

---

# 3. Gate Rules

## Gate Table

| GateType | Applicability | TypicalGateLandMm | RecommendedThicknessRatio | SeverityIfViolated | Notes |
|---|---|---:|---|---|---|
| EdgeGate | General parts | 0.5-0.75 | 40%-70% of adjacent wall | WARNING if too small/large; FAIL if burn/short shot risk. | Common for flat parts. |
| FanGate | Large or cosmetic flat parts | 0.5-0.8 | Wide, thin gate | WARNING if non-uniform fill; FAIL if too narrow. | Better flow-front control. |
| TabGate | Thin flat parts | 0.5-0.75 | Sacrificial tab | WARNING if no tab on fragile part. | Reduces shear at entry. |
| PinGate/Tunnel | Automatic degating | Resin-dependent | Small cross-section | WARNING if cosmetic scar visible. | Common in multi-cavity tooling. |
| SubGate | Hidden gate mark | Resin-dependent | Short and balanced | WARNING if weld line lands in stressed area. | Use for cosmetic hiding. |

## Gate Placement Rules

PicoMoldForge can enforce:

- Prefer gates at the thickest section of the part.
- Avoid critical cosmetic surfaces.
- Avoid feeding a thin dead-end.
- Use wider gates for thin-wall parts to reduce shear.
- Keep gate land short enough to freeze quickly, but not so small that fill pressure spikes.

FAIL if:

- gate feeds into a thin dead-end likely to short-shot
- gate lands on a critical cosmetic surface with no approval
- gate geometry creates high shear/burn risk

---

# 4. Cooling Rules

## Cooling Table

| Rule | Value | SeverityIfViolated | Notes |
|---|---|---|---|
| Cooling channel distance from cavity surface | 1.0-1.5x channel diameter | WARNING if >1.5x; FAIL if hot spots likely. | Even cooling reduces warpage. |
| Wall thickness target | 1.2-3.0 mm for many thermoplastics | WARNING outside material norm; FAIL if local jumps >50% without core-out. | Uniform thickness stabilizes cooling. |
| Local thickness jump | <=50% between adjacent sections | WARNING at 30%-50%; FAIL >50% on cosmetic/CTQ areas. | Avoid sink and warp. |
| Cooling balance across cavities | Symmetric/matched | WARNING if asymmetrical; FAIL if imbalance causes visible warp. | Use equal thermal path. |
| Cooling time | Material and wall dependent | WARNING if not computed; FAIL if cycle forces soft ejection. | Ejection before full set causes deformation. |

## Cooling Formula

    coolingTime is proportional to wallThickness^2

Software rule:

    if wallThickness doubles, cooling time increases about 4x as first-order estimate

Add warning if:

    thickSectionRatio > 1.5 relative to nominal wall

---

# 5. Ejector Rules

## Ejector Table

| Rule | TypicalValue | SeverityIfViolated | Notes |
|---|---|---|---|
| Ejector pin land clearance | 0.02-0.05 mm | WARNING if too tight or too loose. | Prevents galling and flash. |
| Ejector pin placement | Stiff, non-cosmetic, thick-supported areas | WARNING if thin wall; FAIL if visible cosmetic face without approval. | Avoid part deformation. |
| Ejector mark risk area | Flat, unsupported areas | WARNING if ejector concentration is high. | Spread pins or use sleeves/plates. |
| Ejection force sensitivity | Higher for deep, textured, or low-draft parts | WARNING if estimated force exceeds threshold. | Low draft increases sticking risk. |

## Ejector Enforcement Rules

Disallow or warn against ejector pins on:

- thin ribs
- sharp cosmetic corners
- areas with draft below minimum
- visible cosmetic faces without approval

Increase ejector count or area if part has:

- deep walls
- texture
- high-shrinkage material

FAIL if predicted ejection force is too high and draft is below minimum.

---

# 6. Venting Rules

## Venting Table

| Rule | TypicalValue | SeverityIfViolated | Notes |
|---|---|---|---|
| Vent depth | 0.02-0.05 mm | WARNING if outside range; FAIL if too shallow for trapped air or too deep causing flash. | Common practical vent range. |
| Vent location | End of fill, parting line, around cores, ejector pin land | WARNING if vents absent at air traps. | Essential for burn prevention. |
| Vent width | Resin/tool dependent | WARNING if too narrow for trapped air. | Use process-specific library. |
| Tight-tolerance parts | Micro-vents / vacuum assist | WARNING if omitted on long flow paths. | Helps prevent short shots and burns. |

## Venting Logic

Add venting at:

- farthest end of fill
- around bosses and ribs
- near shutoff corners
- under inserts
- trapped-air zones

FAIL if long flow path has no vent candidate and air-trap probability is high.

---

# 7. Steel-Safe Rules

## Steel-Safe Table

| Rule | TypicalValue | SeverityIfViolated | Notes |
|---|---|---|---|
| Steel-safe allowance | 0.1-0.5 mm on critical features | WARNING if none; FAIL if critical feature cannot be tuned. | Lets moldmaker remove steel after trials. |
| CTQ dimensions | Steel-safe required | WARNING if absent; FAIL if critical and no tuning path. | Best on holes, snaps, fits, trims. |
| Cosmetic boundaries | Limited steel-safe; review carefully | WARNING if aggressive removal damages appearance. | Keep rework minimal. |
| Parting-line and shutoff tuning | Steel-safe recommended | WARNING if no tuning margin. | Helps correct flash or mismatch. |

## Steel-Safe Rule

    if CTQ and no steelSafeMargin:
        FAIL

    if nonCTQ and no steelSafeMargin:
        WARNING

---

# 8. Wall and Feature Rules

## Wall/Feature Table

| Rule | TypicalValue | SeverityIfViolated | Notes |
|---|---|---|---|
| Nominal wall, ABS/PC | 2.0-3.0 mm | WARNING if outside; FAIL if abrupt jump >50%. | Material dependent. |
| Nominal wall, PP/PA | 1.0-2.5 mm | WARNING if outside. | Match resin flow and stiffness. |
| Rib thickness | 40%-60% of parent wall | WARNING if >60%; FAIL if >70% cosmetic. | Reduces sink risk. |
| Rib height | About 3x wall thickness | WARNING if >4x; FAIL if too tall without draft/radius. | Tall ribs stick and warp. |
| Boss wall thickness | About 60% of nominal wall | WARNING if thicker. | Avoid sink and voids. |
| Internal radius | >=50% of wall thickness | WARNING if lower; FAIL if sharp inside corner on CTQ. | Improves flow and shrink balance. |
| External radius | Internal radius + wall thickness | WARNING if not derived accordingly. | Geometric consistency. |

## Wall Logic

Prefer:

- uniform walls
- cored-out thick sections
- smooth transitions
- generous radii

FAIL if:

- cosmetic wall thickness jumps abruptly
- no core-out or transition is provided
- CTQ inside corner is sharp and high stress/sink risk

---

# 9. Mold Base Rules

## Mold Base Table

| Rule | TypicalValue | SeverityIfViolated | Notes |
|---|---|---|---|
| Mold base steel | P20/prehardened or project-specific | INFO/WARNING | Depends on volume and wear. |
| Base plate thickness | Project-specific, sized for deflection control | WARNING if undersized. | Must resist clamp and injection load. |
| Ejector plate gap | Sufficient for full stroke and part drop | FAIL if part cannot clear. | Ensure full ejection travel. |
| Core/cavity support | Adequate backing and alignment | FAIL if unsupported thin steel. | Prevents deflection and mismatch. |
| Cooling access in base | Drilled paths near hot zones | WARNING if inaccessible. | Shorter cycles need accessible water lines. |

## Mold Base Rule

The mold base must support:

- clamp force
- cooling channels
- ejector stroke
- alignment
- steel-safe rework

FAIL if base geometry prevents full ejection or cooling routing.

---

# 10. Decision Tree

1. Is the dimension CTQ?
   - Require shrinkage compensation and steel-safe margin.

2. Is wall thickness uniform?
   - If no, warn or fail depending on jump size.

3. Is the gate in or near the thickest section?
   - If no, warn; if fill risk is high, fail.

4. Are vents located at end-of-fill and likely air traps?
   - If no, warn or fail based on flow length and trap probability.

5. Are ejectors on safe surfaces?
   - If no, warn or fail if cosmetic or thin-wall.

6. Is cooling balanced and reachable?
   - If no, warn; if likely to warp, fail.

7. Is steel-safe available where needed?
   - If CTQ and no margin, fail.

---

# 11. Good and Bad Examples

## Shrinkage

Good:

- ABS part with 0.5% shrink allowance on all critical dimensions.

Bad:

- PP part modeled as 0.5% when resin datasheet indicates approximately 2%.

## Gates

Good:

- Edge gate located at thickest wall, short land, balanced fill.

Bad:

- Small gate into a thin cosmetic tip causing short shot and weld line.

## Cooling

Good:

- Channels balanced and placed near hot spots with uniform wall.

Bad:

- One cavity with long cooling path and another with short path causing warp mismatch.

## Ejectors

Good:

- Pins placed on underside thick pads.

Bad:

- Pins concentrated on a thin decorative face.

## Venting

Good:

- Vents at end of fill and around boss/core features.

Bad:

- No vent at far end of flow path, causing burn mark.

## Steel-Safe

Good:

- Extra steel left on a critical snap fit for T1 tuning.

Bad:

- CTQ hole cut to exact size with no rework margin.

## Walls/Features

Good:

- 2.5 mm wall with ribs at 50% wall thickness and generous radii.

Bad:

- 5 mm abrupt wall thickening with sharp inside corners.

## Mold Base

Good:

- Base sized for full ejector stroke and cooling access.

Bad:

- Base too thin to support core and cooling lines.

---

# 12. Recommended Rule Engine Direction

Create rule packs before geometry automation:

- DraftRulePack
- ShrinkageRulePack
- GateRulePack
- CoolingRulePack
- EjectorRulePack
- VentingRulePack
- SteelSafeRulePack
- WallFeatureRulePack
- MoldBaseRulePack

Initial rule engine output should use:

- RuleId
- Category
- Severity
- Message
- ActualValue
- RequiredValue
- RecommendedValue
- Unit
- CorrectiveAction
- RequiresEngineerReview
- SourceRulePackVersion