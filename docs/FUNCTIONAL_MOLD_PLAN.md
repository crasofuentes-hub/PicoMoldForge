# Functional mold plan

The program is functional when a part (an STL, or a design from PicoMoldForge Parts) comes out, from one launcher,
as a two-half mold that can be manufactured and that is verified by measurements, or with the zone and the reason
why it cannot be molded in two halves. This plan, agreed on 3 Nov 2026, orders the work by dependency; it extends
the Functional Mold Alpha v2 priorities (docs/FUNCTIONAL_MOLD_ALPHA_V2.md).

| Phase | What | Acceptance test (input -> expected result) | Verified on | Status |
|-------|------|---------------------------------------------|-------------|--------|
| F0 | The measured part through the pipeline; line-ending policy | 20 x 10 x 5 mm box -> separation and scorer use 0-20 / 0-10 / 0-5; a block that does not contain the part -> FAIL "sticks out by N mm" | Linux | Done |
| F1 | Parting plane chosen by geometry (option, default once validated) | flange -> plane on the disc, trapped <= 0.6 %; box -> axis Z | Linux | Done |
| F2 | Shrinkage on the mold halves; resin separate from the mold steel in the config | box, s = 0.011 -> cavity 20.22 x 10.11 x 5.055 mm (+- 1 voxel) | Windows | Open |
| F3 | Feed system tied to the part (priority 3) | box -> the gate reaches the cavity; sprue and runner keep the clearance from the part and stay inside the block | Linux | Open |
| F4 | Ejector pins on the moving half (priority 4) | box -> pins under the face of the moving half, inside its outline, at the minimum spacing | Linux | Open |
| F5 | Cooling around the real cavity | channel-to-cavity distance >= the configured minimum; no channel crosses a pin or the feed | Linux | Open |
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
