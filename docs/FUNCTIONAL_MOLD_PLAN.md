# Functional mold plan

The program is functional when a part (an STL, or a design from PicoMoldForge Parts) comes out, from one launcher,
as a two-half mold that can be manufactured and that is verified by measurements, or with the zone and the reason
why it cannot be molded in two halves. This plan, agreed on 3 Nov 2026, orders the work by dependency; it extends
the Functional Mold Alpha v2 priorities (docs/FUNCTIONAL_MOLD_ALPHA_V2.md).

| Phase | What | Acceptance test (input -> expected result) | Verified on | Status |
|-------|------|---------------------------------------------|-------------|--------|
| F0 | The measured part through the pipeline; line-ending policy | 20 x 10 x 5 mm box -> separation and scorer use 0-20 / 0-10 / 0-5; a block that does not contain the part -> FAIL "sticks out by N mm" | Linux | Done |
| F1 | Parting plane chosen by geometry (option, default once validated) | flange -> plane on the disc, trapped <= 0.6 %; box -> axis Z | Linux | Done |
| F2 | Shrinkage on the mold halves; resin separate from the mold steel in the config | box, s = 0.011 -> cavity 20.22 x 10.11 x 5.055 mm (+- 1 voxel) | Windows | Done in code; the halves need the Windows run |
| F3 | Feed system tied to the part (priority 3) | box -> the gate reaches the cavity; sprue and runner keep the clearance from the part and stay inside the block | Linux | Done in code; the diagnostics are drawn on the Windows run |
| F4 | Ejector pins on the moving half (priority 4) | box -> pins under the face of the moving half, inside its outline, at the minimum spacing | Linux | Done in code; as F3 |
| F5 | Cooling around the real cavity | channel-to-cavity distance >= the configured minimum; no channel crosses a pin or the feed | Linux | Done in code; as F3 |
| F6 | Subtract all features from the halves; centering features (priority 5) | removed volume = volume of each system (+- voxel); the halves locate and open without collision | Windows | Open |
| F7 | Verification of the final mold | PASS where the part leaves two halves (box, spur gear); elsewhere the zone and the mechanism it needs | Linux + Windows | Open |
| F8 | Package and link with Parts (priority 7), including the parked touching-faces fix | one launcher designs the part, generates its mold and leaves a ZIP of results | Windows | Open |

F1 comes before F3 to F5 because the gate, the ejectors and the channels are placed relative to the plane.

## F0. The measured part

`GeneratorMoldPlanner` (pure .NET) reads the input STL once into a `GeneratorMoldPlan` shared by the runner and the
report: the part bounds measured on the mesh, whether the part sits inside the mold block with the required margin
(`moldSystem.moldMarginMm`; strictly inside, a part touching a block face would leave the cavity open there), and the
split used for the halves.

- Separation and parting-plane scoring work on the measured part. Before, they used the configured part size centred
  in the block: on the end-to-end box (x 0 to 20 mm) the scorer selected X at 50 mm, outside the part; it now selects
  Z at 2.5 mm.
- `FunctionalMoldAlphaReport.MoldBlockFit` and the warnings state the bounds and the margin, or FAIL when the part
  touches or crosses a block face. The report still completes: separation then falls back to the configured size.
- `.gitattributes` makes text files LF everywhere (the solution file CRLF) and never converts STL files (a binary STL
  can lack the NUL byte Git uses to detect binaries).

## F1. Parting plane by geometry

`PartingPlaneSearch` tests every triangle once per half on X, Y and Z (the steel point 0.1 mm off the face, a ray
along each opening direction; the rules of `MeshMoldAccessAnalyzer`), so any offset is then scored by sums. Offsets
tried: the levels of faces lying across the axis, 32 evenly spaced levels, the bounding-box centre and the part ends.

- On each axis: least trapped area, then most flat area lying in the plane (a parting surface on a face instead of
  across walls), then least undrafted area, then the offset nearest the centre.
- Across axes: least trapped area, then least undrafted area (set by the walls parallel to the axis), then flat area.
- Areas within max(1 mm2, 0.1 % of the surface) of the best count as equal; undrafted areas within max(1 mm2, 1 %).

Rule refuted on the way: undrafted area before flat area on the same axis. The spur gear then split at its
mid-height instead of its face, because the tilts of the tessellated flanks change the undrafted area by 0.3 % of the
surface (24.5 mm2) between offsets; on one axis that area depends on the tessellation, not on the design.

The trapped and undrafted areas of a plane equal the risky areas of the undercut and draft summaries for that split
(test `Reference_ScoresLikeTheDraftAndUndercutSummaries`).

`"parting": { "mode": "Geometry" }` splits at the plane found; in Auto and Manual the warnings give it next to the
plane in use and recommend the mode when it traps less. Windows STLs of the Parts samples:

| Part | Plane by geometry | Trapped | Automatic plane (bounding box) | Trapped | Time |
|------|-------------------|---------|---------------------------------|---------|------|
| Spur gear | Z = 0 (bottom face) | 0.0 % | Y = 0 | 27.2 % | 1.0 s |
| Helical gear | Z = 10.7 | 28.0 % | Y = 0 | 29.4 % | 0.3 s |
| Flange | Z = 0 (bottom face) | 0.5 % | Y = 0 | 15.7 % | 1.3 s |
| Piston | Z = 52 (crown face) | 25.6 % | X = 0 | 38.5 % | 2.5 s |
| Monoblock | Z = 50 (face level) | 1.7 % | X = 0 | 57.8 % | 1.4 s |

The helical gear has no flat split that releases its helix; the piston keeps its ring grooves and pin bores trapped
(side actions) whatever the split.

## F2. Shrinkage on the mold halves

Before, the boolean cavity and both mold halves were cut around the part as designed: only the legacy Cavity.stl and
Core.stl were scaled, so a molded part would come out smaller by the shrinkage. Now:

- The shrinkage comes from the resin: optional `"resin": { "name": "ABS", "shrinkageRate": 0.006 }`. A known resin
  (ABS, PP, PC, PA, POM, PE) may omit the rate and gets the recommended rate of `ShrinkageRuleEngine`; a rate outside
  the resin's typical range is flagged. Without a resin section, `material.shrinkageRate` is kept as before and the
  warnings say so (`material` names the mold steel in the samples).
- The generator writes `ShrinkageScaledPart.stl`: the part scaled by 1 + shrinkage about its bounding-box centre.
  BooleanCavity.stl and both halves are cut around it, and the split moves with it (same relative height): the
  end-to-end box split on its bottom face (Z = 0) is split at Z = -0.0275 mm in the mold.
- The block fit (F0) is checked on the cavity. `FunctionalMoldAlphaReport.Shrinkage` gives the rate, its source, the
  scale factor, the cavity bounds and the split height in the mold. The artifact count goes from 10 to 11.
- Draft, undercut and wall thickness stay measured on the part as designed.

The cavity size in the generated halves (20.22 x 10.11 x 5.055 mm for the box, within one voxel) is measured by the
F7 verifier on the Windows run.


## F3 to F5. Feed, ejector pins and cooling around the cavity

`MoldLayoutPlanner` (pure .NET) places the three systems around the cavity of the mold plan, on its split, and the
report evaluators judge what it placed: gate/runner/sprue, ejector, cooling and clearance summaries now come from
measured positions instead of configured values. `FunctionalMoldAlphaReport.MoldLayout` lists every feature
(centreline and diameter); the warnings explain each choice. The features are not yet cut into the halves (F6).

Halves. The part stays on the half it shrinks onto: the half whose faces point towards the centre of the cavity
(cores, bores) with more area x inward distance; with no clear difference, the half holding more of the cavity
surface; otherwise the - half. That half moves and carries the ejector pins; the other carries the sprue. The
splitter writes the - side as BooleanCoreSide.stl and the + side as BooleanCavitySide.stl whatever moves, and the
warnings say which file is the moving half.

Rule refuted on the way: "the moving half holds more of the cavity surface". A 40 x 40 x 3 mm tray with a 36 x 36 x
1 mm recess underneath, split at the recess ceiling, has more surface in the + half, but the recess (a core of the -
half) holds the part (test `Tray_ShrinksOntoTheCoreHalf_EvenWithLessCavityArea`).

Feed (rules of thumb, `MoldLayoutOptions`):

- An edge gate on the parting plane, at the middle of the stretch of cavity outline that holds the extreme point along
  one of 16 directions, entering along the outline normal. Its diameter is 0.6 x the section it enters (the largest
  ball in the part within reach of the gate, at most the thickest wall), at least 0.5 mm; land 1 mm.
- Runner: diameter = thickest wall + 1.5 mm (at least 3 mm), straight out on the plane, one gate land from the cavity.
- Sprue: runner diameter + 1 mm, from the end of the runner through the fixed half to its outer face; the runner is
  lengthened until the sprue keeps 0.5 mm of steel from the cavity.
- Among directions that work: the thickest entry (within 5 %), then the sprue nearest the centre of the block.

Rules refuted on the way: the thickness of the entry measured along the three axes only (a slanted direction on a
box read 5.9 mm instead of 5.06 mm, and the monoblock 85 mm); the gate checked along its axis only (a cap 1 mm over
the plane cuts the 3 mm gate; test `Mushroom_CapOverTheGate_GateMeetsTheCavityAgain`).

Ejector pins: on the faces of the moving half across the axis that face its back plate, grouped by level; a pin fits
where its disc plus 0.5 mm lies inside the face (exact distance to the face outline) and nothing of the part lies in
its column up to the back plate (test `CShapeWithARidgedTop_TakesNoPinUnderTheOverhang`). Pins are at least 2 x
their diameter apart, spread by farthest-point selection; the stroke is the depth of the cavity in the moving half.

Cooling: straight channels drilled through the block along the longer in-plane side of the cavity, in each half
beyond the cavity, `cooling.channelCount` per half, spaced `cooling.channelSpacingMm` and centred on the cavity. A
channel keeps `cooling.minimumClearanceMm` of steel from the cavity (measured from the channel wall) and from the
block faces it does not pass through, and 3 mm from the pins and the feed; a position that does not is skipped and
named in the warnings.

Distances are exact between the straight centrelines and from a centreline to the cavity triangles (segment to
triangle in `TriangleBvh.SegmentDistance`); the clearance matrix leaves out the pairs within the feed, which touch by
design.

End-to-end box (block -25..125 x -25..85 x -25..55 mm, split X = 10): the - half moves, its 10.11 x 5.055 mm end face
takes one 4 mm pin; the feed works; the + half takes two channels (the third would cross the sprue) and the - half
none, since 24.89 mm of steel behind the cavity is less than a channel needs (10 + 6 + 0.1 + 10 mm).

Windows STLs of the Parts samples, split by geometry, block = cavity bounds + 30 mm, 4 pins of 4 mm, 3 channels of 6 mm
per half spaced 15 mm, 10 mm clearance:

| Part | Moving half | Gate: section -> diameter | Feed | Pins | Channels + / - | Time |
|------|-------------|---------------------------|------|------|----------------|------|
| Spur gear (Z = 0) | + (bore) | tooth tip 2.42 -> 1.45 mm | works | 4 | 3 / 3 | 0.7 s |
| Flange (Z = 0) | + (bore) | disc rim 10.06 -> 6.03 mm | works | 4 | 3 / 3 | 1.4 s |
| Piston (Z = 52) | - (inside of the skirt) | crown edge 5.05 -> 3.03 mm | works | 4, under the crown | 3 / 3 | 0.9 s |
| Helical gear (Z = 10.7) | - | tooth 2.25 -> 1.35 mm | works | 4 | 3 / 2 | 0.8 s |
| Monoblock (Z = 50) | + | 20.94 -> 12.56 mm | FAIL with 30 mm (no room for the 23 mm sprue), works with 60 mm | 4 | 3 / 3 | 1.0 s |

Known limits: only an edge gate (round parts such as gears are normally gated at the centre); straight channels
above and below the cavity only; vents and insert pockets still come from the configured part size;
MoldSystemDiagnostic.stl draws the pins only when the split is across Z (their positions are in the report).
