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
| F6 | Subtract all features from the halves; centering features (priority 5) | removed volume = volume of each system (+- voxel); the halves locate and open without collision | Windows | Done in code; needs the Windows run |
| F7 | Verification of the final mold | PASS where the part leaves two halves (box, spur gear); elsewhere the zone and the mechanism it needs | Linux + Windows | Done in code (Linux checks); the halves checks need the Windows run |
| F8 | Package and link with Parts (priority 7), including the parked touching-faces fix | one launcher designs the part, generates its mold and leaves a ZIP of results | Windows | Done in code (scripts/run-mold-verification.ps1); needs the Windows run |

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

## F6. Functional mold halves

The generator writes two more files, MovingHalf.stl and FixedHalf.stl (13 artifacts instead of 11): each half of the
block minus the cavity, minus the features placed in it, plus its guide pins (`PicoFunctionalMoldHalvesGenerator`,
voxel booleans). BooleanCoreSide.stl and BooleanCavitySide.stl stay the plain split.

- Cut in both halves: gate and runner (on the parting plane). Fixed half: sprue, guide bores. Moving half: ejector
  pin holes. Each half: its cooling channels.
- Holes run one voxel past the faces they open through, so the voxels leave no skin of steel over them.
- Guide pins (layout, `MoldLayoutOptions`): four 8 mm pins near the block corners, the block's edge clearance from
  its faces, standing on the moving half and entering 1.5 diameters into bores of the fixed half. The bores have a
  radial play of max(0.5 mm, one voxel), so the halves do not fuse at the voxel size. A corner whose bore would
  come within 3 mm of the cavity or another feature moves towards the centre of the block.
- `FunctionalMoldAlphaReport.MoldHalves` gives, per half, the steel each system removed or added (measured on the
  voxels), the steel both halves share and the extent of each half along the split axis.

## F7. Verification of the final mold

`MoldVerifier` (pure .NET) writes `FunctionalMoldAlphaReport.Verification` and the console line
"Mold verification: PASS|FAIL ...". Its checks:

| Check | PASS when |
|-------|-----------|
| cavity-in-block | the cavity lies strictly inside the block |
| two-halves | no face is trapped or needs a side action (within max(1 mm2, 0.1 %)); otherwise FAIL with each zone (faces sharing vertices), its area, bounding box and mechanism: a slide with its travel, or an undercut that needs a lifter, a collapsible core or a design change. Zones freed by less than the resolution of the analysis are reported, not counted (WARN when nothing else is held) |
| feed | the feed works (F3) |
| ejector-pins | at least one pin is placed |
| cooling | both halves have channels (WARN otherwise) |
| guide-pins | at least two guide pins (WARN otherwise) |
| clearances | no pair of features is closer than its clearance |
| halves-cut | each system removed (or added) the steel of a Monte Carlo estimate of the exact volume (40 000 samples), within half a voxel over its surface, and points on the axes of its features are empty (holes) or in steel (pins) in the half's STL; WARN when neither can tell the feature from no feature at the voxel size |
| halves-overlap | the halves share at most 0.05 voxel over the parting plane |
| halves-open | only the guide pins cross the parting plane, by their protrusion (+ one voxel) |
| cavity-shape | points of the cavity mesh lie within one voxel of the halves (90th percentile) |
| halves-mesh | each half's STL is a closed surface (every edge in two triangles) in one piece; edges in more than two triangles are a WARN |

The last five need the halves built by PicoGK: without them they are NOT RUN and do not change the verdict. A WARN does
not change the verdict either; the summary names the checks that passed with one.

The cavity-shape check is limited by the voxel size. At the 1 mm voxels of the samples it cannot see the 0.22 mm
the shrinkage adds to the end-to-end box; a finer voxelResolutionMm shows it.

Linux results without the halves:

- End-to-end box, split X = 10: PASS. The - half has no cooling, so that check is a WARN.
- T-shape split under its bar: FAIL, with two zones that need a slide (test
  `TShape_SplitUnderTheBar_FailsWithTheZonesThatNeedASlide`).
- Parts samples, molded with `--mold-stl` (pure .NET part):
  - spur gear: PASS;
  - flange: FAIL, one zone, the 4 mm cross hole in the hub (a slide, at least 9.96 mm of travel);
  - monoblock: FAIL, the main bearing bore in both end walls and the bulkhead (slides);
  - piston: FAIL, three ring grooves and the pin bores (slides);
  - helical gear: FAIL, 24 tooth flanks.

Rule refuted on the way: "a zone with any trapped face is an undercut". On the symmetric monoblock, one trapped
sliver of 0.1 mm2 made the 565 mm2 bore of one end wall an undercut, while the other end wall needed a slide.
Now:

- A zone is an undercut only when its trapped area exceeds max(1 mm2, 5 % of the zone).
- Zones under 1 mm2 are counted as slivers and not listed (test `Zone_IsAnUndercutOnlyWhenARealShareOfItIsTrapped`).
- Whether one slide releases a zone or it takes several (a groove all around the part) is not decided. A heuristic
  on the normals of the zone was refuted by the same bores: their end faces looked like a ring.

Second rule refuted: "every zone of 1 mm2 or more that a slide releases needs that slide". The monoblock listed a
4.6 mm2 strip of crankcase wall (X -6..-5.4, Y = -36, Z 12.9..28.7) as a slide with 0.05 mm of travel. Its faces are
vertical. They are held by a mesh vertex 0.125 mm proud of the wall, 18 mm below, at the corner of the wall, the
bulkhead and the bottom face. The template makes that corner sharp, and the mesh resolution is 0.35 mm. The steel
point of a face sits one resolution r (0.1 mm) off it. A face freed by moving it less than r is therefore held by a
step under 2r, which this analysis cannot tell from mesh noise. Now:

- A zone released by slides whose faces all need less than r of travel is reported in the two-halves detail and
  not counted. If it is the only thing held, the check is a WARN (test
  `Zone_FreedByLessThanTheResolution_IsReportedButNotCounted`).
- Cost: a real step under 2r (0.2 mm) is reported, not failed.
- On the six samples only that monoblock zone changed. The monoblock keeps three zones and still fails on its bores.

## F8. Parts to mold

`PicoMoldForge.Generator --mold-stl <part.stl> --output <folder>` writes a project for the part (split by geometry,
resin ABS unless given, a block with room for every feature) and generates its mold. On the end-to-end box the
project validates and the mold plan passes every check that runs without PicoGK, both halves cooled (test
`Write_ForTheBox_ValidatesAndLeavesRoomForEveryFeature`).

Rule refuted on the way: cooling placed before the guide pins. On that box a channel of the fixed half took the
corners, and two of the four guide pins found no room. Guide pins now come first: without them the halves do not
align, while a channel has other positions.

The launcher `scripts/run-mold-verification.ps1` (Windows PowerShell 5.1) runs the whole chain:

1. Rebuilds the solution from scratch (`--no-incremental`) and runs every test project (the PicoGK native tests
   of Parts included), reading each project's result from its TRX file.
2. Designs the five Parts samples and generates a mold for each of them and for the repository sample box.
3. Prints the verdict of every mold and leaves one ZIP on the Desktop: logs, TRX files, reports and the halves of
   the box and the spur gear. The other STL files stay in `mold-run/<date>/`.

First Windows run (commit 44378c7): the build failed with CS0246 in `PicoFunctionalMoldHalvesGenerator.cs` (the
types of `FunctionalMoldHalves.cs` were not found).

- Cause: the clock of that computer ran about 12 hours behind. The run is dated 06:03 on a commit merged at 17:39
  the same day.
- The sources git wrote looked older than the Core binaries of an earlier build, so MSBuild skipped Core and
  Core.Tests. The PicoGK project did compile, against the old Core.
- Reproduced on Linux by dating the Core outputs after the sources: the same four errors, at the same lines. A
  plain rebuild fails again. `--no-incremental` builds.
- The launcher now always rebuilds from scratch. Its summary has a "Reloj" row that warns when the commit is dated
  after the clock.

Second Windows run (commit b059c62, Windows 10.0.26200, .NET 9.0.22, PicoGK 26.1.1): every step ran, in under 7
minutes.

- All 559 tests pass, the 34 PicoGK-native ones included: Core 326, Generator 64, PicoGK 29, Parts.Core 111,
  Parts.Cli 21, Parts.PicoGK 4, Cli 4.
- The six molds were generated in 11 to 32 s each, with 13 files each.
- The sample box and the spur gear PASS every check, the four halves checks included. The flange, piston, monoblock
  and helical gear FAIL only on two halves, with the zones of the Linux preview.
- On the halves of the box and the gear, sections through the cavity, the feed, the ejector pins, the cooling and
  the guide pins show each feature where the layout puts it.

Three findings from that run:

1. The volume test of halves-cut could not tell some features from none.
   - Its tolerance is half a voxel over the whole capsule surface. For the gate and runner of the box (moving half),
     that came to 144.1 mm3 against an expected 141.3 mm3, so a missing cut would have passed.
   - The 41 measured volumes were within -10.8 % to +6.3 % of the expected ones. The tolerances were 14 % to 102 %.
   - The check now also tests points on the axes of the features against each half's STL: four per feature, half a
     radius into the half for the feed on the parting plane. Replayed on the Windows halves of the box and the gear,
     every point agrees: 4 to 16 per system, holes empty and pins in steel.
   - A system that neither the volume nor the axis can confirm is a WARN.
2. The halves are closed and each is one piece, but some edges lie in four, six or ten triangles.
   - Box: 52 such edges in the moving half and 388 in the fixed half. Gear: 48 and 160.
   - Every one of them is on a guide pin or a guide bore, at the grid nodes where the circle of the pin (radius 4 mm)
     or bore (radius 5 mm) passes through. These are the only features with whole-millimetre centres and radii.
   - Hypothesis: the voxel field is exactly zero at those nodes, and the mesher puts several vertices on them.
   - Change: the guide pins are now planned a quarter voxel inside the grid. With radii a multiple of a quarter
     voxel, no node of the grid then lies on their circles.
   - Test: the next Windows run. The new halves-mesh check reports these edges (WARN), so the run says whether they
     are gone.
3. Parts does not mesh the gears and the flange the same way on Windows and on Linux.
   - Triangles: spur gear 38 158 against 38 882; helical gear 19 656 against 19 382; flange 35 492 against 35 778.
     The piston and the monoblock are identical.
   - Each mesh passes every Parts check, and the volumes agree within 0.004 %.
   - The verdicts did not change, but the numbers did. The slide the flange's cross hole needs reads at least
     7.02 mm on Windows against 9.96 mm on Linux. That figure is a lower bound found by searching directions, and it
     depends on the mesh.

Third Windows run (commit 1636428): every step ran.

- All 579 tests pass.
- The six molds keep their verdicts: box and spur gear PASS (the box with its cooling warning), the other four FAIL
  only on two halves.
- The guide-pin hypothesis held: no edge lies in more than two triangles in any of the twelve halves. Before the
  quarter-voxel offset there were 52/388 in the box and 48/160 in the gear. A separate Python count of the box and
  gear halves agrees.
- Every point on the axes of every system was as planned in all six molds: holes empty, pins in steel.
- New finding: the monoblock's moving half came out in 4 pieces (0 open edges).
  - Cause, measured on the same cavity mesh here: the crankcase ceiling of the cavity wanders from Z 49.91 to
    50.07 mm (mesh noise) about the parting plane at 49.978 mm.
  - Where the ceiling rises above the voxel layer at Z = 50, that node is steel of the moving half, cut off from the
    rest.
  - A voxel model of that half finds such single nodes, under ceiling heights of 50.003, 50.008 and 50.013 mm. That
    makes three islands; a fourth candidate at 50.0004 mm is marginal. This matches the three extra pieces of the
    Windows run.
  - Change: the halves are written without islands smaller than one voxel. The result counts them
    (`IslandsRemoved`, `IslandVolumeMm3`), a warning names them, and halves-mesh reports them. Any larger extra piece
    stays, and halves-mesh now names each one with its volume and box (a void if it is wound against the solid).
  - Checked here on real halves from the Windows run, with three 0.05 mm islands added: the islands go and every
    triangle of the half stays.

Fourth Windows run (commit 8332b78): every step ran.

- All 583 tests pass.
- Each of the twelve halves is one closed piece, with no edge in more than two triangles.
- The monoblock's moving half left out 3 islands under one voxel (0.0011 mm3 in all), the three the voxel model
  predicted.
- In all six molds, every one of the 508 axis points agrees with the plan.
- Verdicts: the box and the spur gear PASS (the box with its cooling warning). The other four FAIL only on two halves.

Rule refuted on that run: "the slide travel of a zone is the longest travel of its faces".

- On the piston, a ring groove 2.6 mm deep is 99 % freed by 2.91 mm, but one face of 0.18 mm2 read 11.65 mm.
- On the monoblock, the bore through the 10 mm bulkhead reads 5.06 mm for 95 % of its area (half the bulkhead) and
  9.5 mm at its longest face.
- The flange's cross hole read 9.96 mm on Linux and 7.02 mm on Windows: the longest face changes with the mesh.
- An exact turned groove of the same size gives at most 2.57 mm on every face. The outliers come from single faces of
  the real meshes.
- Now: a zone quotes the travel that frees 95 % of its area, the share the trapped area is judged by. It adds the
  longest when that is more than 10 % (+ 0.1 mm) larger, for example "at least 2.55 mm of travel for 95 % of its area
  (up to 11.65 mm for the rest)". `MoldTrappedZone.SideTravelMm` is that 95 % figure and `MaximumSideTravelMm` the
  longest. A zone is below the resolution only when its longest face is (test
  `Zone_QuotesTheTravelThatFreesNinetyFivePercentOfIt`).

The touching-faces fix of Parts (field zeros classified by a nudge, so touching features join) is merged with an
edge-midpoint guard in the simplifier. Without the guard, the flange left rim chords 0.2675 mm off the surface;
with it, the flange stays within 0.1002 mm and all five samples pass every check.
