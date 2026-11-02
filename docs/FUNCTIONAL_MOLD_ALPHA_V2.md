# Functional Mold Alpha v2

Functional Mold Alpha v2 connects the alpha metrics of `FunctionalMoldAlphaReport` to the actual geometry of
the part instead of values derived from the configuration. It follows the priority order of the README.

| # | Priority | Status |
|---|----------|--------|
| 1 | Derive wall thickness from the part geometry | Done: measured on the input STL |
| 2 | Derive draft and undercut risk from actual mesh normals | Done: measured on the input STL for both mold halves |
| 3 | Emit real gate/runner/sprue geometry artifacts | Next |
| 4 | Emit real ejector geometry artifacts | Open |
| 5 | Subtract cooling/ejector/gate geometry from the mold halves | Open |
| 6 | Strengthen shutoff surfaces and parting-line geometry | Open |
| 7 | Publish a real alpha release package | Open |

## 1. Wall thickness measured on the input STL

Before v2 the `WallThickness` summary was built from three proxy samples taken from the DfAM configuration
(`minimumWallThicknessMm`, `recommendedMinimumWallThicknessMm` and the configured part height). It now comes
from the part itself (`PicoMoldForge.Core.Engineering.WallThickness`, pure .NET, no PicoGK runtime):

1. `BinaryStlTriangleReader` reads the input STL (the generator only accepts binary STL).
2. `MeshWallThicknessSampler` measures every triangle: the thickness is the diameter of the largest ball that
   touches the triangle centroid and stays inside the part (inscribed-ball or "rolling ball" method), found by
   bisection with a bounding volume hierarchy. A measurement counts as a wall only when the face that stops
   the ball faces the sample (normal dot product below -0.5). Edges, rims and end faces, where a neighbouring
   face stops the ball, are not walls. An inside-out STL (negative signed volume) is measured with its normals
   flipped.
3. The nominal wall is the area-weighted median of the wall measurements.
4. `WallThicknessRegionBuilder` groups wall triangles that share an edge and fall in the same band (thin,
   abrupt low, nominal, abrupt high, thick, with the limits of `VoxelWallThicknessAnalyzer`). Regions smaller
   than the thin limit squared are left out and counted.
5. Each region becomes one analyzer sample. The thin limit is still the smaller of the two DfAM values; the
   thick limit is three times the measured nominal; the abrupt-change ratio is still 0.5.

The summary gains `SampledSurfaceAreaMm2`. When it is positive, `OverallReadinessScore` uses
`1 - RiskySurfaceAreaMm2 / SampledSurfaceAreaMm2` for the wall part instead of the share of risky samples,
because the number of regions depends on the mesh and their area does not: the same spur gear design scored
0.950 with the Windows mesh and 0.917 with the Linux mesh when counting regions. Summaries without the area
keep the count formula.

The report warnings state what was measured: the wall share of the surface, the nominal wall, the regions
reported and left out, the thinnest region with its location, walls thinner than 0.1 mm (probably folded
slivers of the mesh) and an inside-out input.

Checked on STLs of the PicoMoldForge Parts samples generated on Windows (designed thicknesses in brackets):

| Part | Triangles | Wall area by measured thickness | Time (2 cores) |
|------|-----------|---------------------------------|----------------|
| Flange [10] | 90 602 | 10.0 mm: 94 % | 7.0 s |
| Piston [skirt 3.5, crown and ring belt 6, pin boss 4] | 70 994 | 3.5 mm: 58 %, 6.0 mm: 19 %, 4.0 mm: 16 % | 6.1 s |
| Monoblock [wall 6, liner 4, bulkhead 10] | 31 850 | 6.0 mm: 33 %, 4.0 mm: 30 %, 10.0 mm: 4 % | 3.7 s |

The monoblock also shows three thin regions (0.6 to 1.1 mm): the template leaves a web of block material where
the two water jackets (radius 29 mm around x = -26 and x = 26 mm) nearly meet, 0.99 mm wide at y = 13.8 mm.

Limitations:

- A wall whose two faces are more than 60 degrees from facing each other is not measured.
- Thin features smaller than the thin limit squared per face (1.44 mm2 for 1.2 mm) are not reported as
  regions; the warnings give their number and area.
- The measurement is faithful to the mesh: folded slivers in the STL appear as walls of a few hundredths of a
  millimetre and are named in the warnings.
- The end-to-end generator test needs the native PicoGK runtime (Windows x64 or macOS arm64). The measurement
  itself is covered by `MeshWallThicknessSamplerTests` on any platform, including the 20 x 10 x 5 mm box of the
  end-to-end test (2 walls of 5.0 mm).

## 2. Draft and undercut measured on the input STL for both mold halves

Before v2 the draft summary took every triangle against one pull direction (the automatic opening direction,
even when the configuration overrides the parting axis), so the faces of the lower half showed negative draft. The
undercut summary was a ratio of opposing normals reported as side-action candidates with a fixed 1 mm trap depth.
Both now come from `PicoMoldForge.Core.Engineering.MoldAccess` (pure .NET, no PicoGK runtime), for the split that
is actually used: the axis and offset of the separation summary.

1. Each triangle belongs to the half that holds its centroid; within the resolution r = 0.1 mm of the plane, to
   the half its normal faces (a face lying in the plane, up to mesh noise). Each half opens away from the plane.
2. Draft: each triangle is taken against the opening direction of its own half and classified by the existing
   `DraftBasicGeometryAnalyzer` (negative below a dot product of -0.0001, none up to 0.1 deg, low below 1 deg).
3. Access (`MeshMoldAccessAnalyzer`): the steel next to a face is the point r off it. The face is clear when the ray
   from that point along the opening direction leaves the part. Otherwise it is trapped: a side-action candidate
   when the steel point can move sideways (16 directions in the parting plane and the projected normal) without
   meeting the part and then open, the shortest such move being its trap depth; an undercut when no direction
   frees it. The sideways search reads a height field of the part seen along the axis (cells of r/2), and every
   escape it finds is confirmed by a ray.
4. A steel point inside the part (a folded sliver of the mesh, or a gap narrower than r) makes the triangle an
   invalid normal in both summaries.
5. `FaceCount` is the triangle count. The undercut summary counts clear, side-action and undercut triangles; low
   pull clearance stays 0 because low draft is reported by the draft summary. `RiskySurfaceAreaMm2` is the area
   that is not clear and `MaximumTrapDepthMm` the longest side travel.
6. Both summaries gain `SampledSurfaceAreaMm2`, and `OverallReadinessScore` uses `1 - risky area / sampled area`
   for them, as for the wall thickness. `DraftBasicGeometryAnalyzer` and `UndercutRiskAnalyzer` fill it as well.
7. The warnings state the split, the draft shares by area (and the negative share steeper than -1 deg), the
   trapped area, the side-action area with its longest travel and where, the undercut area and its extent.

Rules refuted on the way:

- Trapped when the ray from the face itself is blocked: the spur gear split across its axis had 31.7 % of its
  surface trapped by tessellation tilts of its vertical flanks, all freed by less than 0.05 mm. Testing the steel
  0.1 mm off the face removes them; the gear has no trapped face.
- Centroid side with exact ties only: split at its disc face (z = 10 mm), the flange came out 26.3 % trapped
  against 1.1 % at z = 9.99 mm, because the mesh puts that face 0.01 mm either side of the plane; with the r band,
  z = 10 and z = 9.99 mm both give 0.5 %.
- One ray per crossed triangle for the side travel: up to 82 s on the monoblock and false undercuts where the
  search hit its step limit (piston). The height field takes 0.2 to 4.5 s.

Checked on the STLs of the PicoMoldForge Parts samples generated on Windows, split at the bounding-box centre
(the automatic offset); the generator picks the axis of the longest side, marked with *:

| Part | Triangles | Axis | Draft below 1 deg | Trapped | Without escape | Longest side travel | Time (2 cores) |
|------|-----------|------|-------------------|---------|----------------|---------------------|----------------|
| Spur gear | 34 116 | X | 66.1 % | 27.3 % | 0 mm2 | 11.9 mm | 1.3 s |
| | | Y* | 65.9 % | 27.2 % | 0.0 mm2 | 11.9 mm | 0.6 s |
| | | Z | 52.9 % | 0 % | 0 mm2 | - | 0.4 s |
| Helical gear | 16 342 | X | 62.3 % | 29.3 % | 0.1 mm2 | 15.6 mm | 0.4 s |
| | | Y* | 62.4 % | 29.4 % | 0.3 mm2 | 11.3 mm | 0.3 s |
| | | Z | 37.9 % | 28.1 % | 0 mm2 | 3.2 mm | 0.2 s |
| Flange | 90 602 | X | 72.8 % | 15.2 % | 0.1 mm2 | 26.4 mm | 0.9 s |
| | | Y* | 74.0 % | 15.7 % | 11.3 mm2 | 29.6 mm | 0.9 s |
| | | Z | 64.2 % | 28.9 % | 0.0 mm2 | 43.5 mm | 1.1 s |
| Piston | 70 994 | X* | 65.0 % | 38.5 % | 2 296 mm2 | 46.5 mm | 1.9 s |
| | | Y | 60.7 % | 43.9 % | 1 531 mm2 | 63.7 mm | 2.0 s |
| | | Z | 74.7 % | 37.6 % | 4 547 mm2 | 49.4 mm | 4.5 s |
| Monoblock | 31 850 | X* | 65.2 % | 57.8 % | 9 932 mm2 | 65.0 mm | 2.4 s |
| | | Y | 67.1 % | 59.4 % | 8 451 mm2 | 118.0 mm | 2.9 s |
| | | Z | 86.8 % | 1.7 % | 0 mm2 | 9.5 mm | 0.7 s |

What the numbers show:

- Spur gear: split across its axis, nothing is trapped; split along it, the tooth flanks of each half are.
- Helical gear: even split across its axis, 28 % of the surface is trapped (up to 3.2 mm of sideways travel); a
  straight pull cannot release a helix.
- Piston split across its axis (z = 26 mm): the 20 106 triangles 2.4 to 2.8 mm under the outer radius (by their
  radius, the ring groove bottoms) need a median of 2.52 mm of sideways travel, from 2.38 mm: the 2.6 mm groove
  depth less the 0.1 mm resolution.
- Flange: the split at the bounding-box centre (z = 15 mm, through the hub) puts the disc top face in the lower
  half and traps 28.9 % of the surface; split at the disc face (z = 10 mm) or through the disc (z = 5 mm) only
  0.5 % is trapped. The automatic plane is the main source of undercut here: input for priority 6.
- Same part, two meshes: the trapped area of the monoblock split across X is 73 995 mm2 on the Parts mesh
  (31 850 triangles) and 73 837 mm2 on the PicoGK mesh (579 368 triangles, 17 s); flange: 3 596 and 3 581 mm2.
  How that area divides between side action and undercut differs between the two monoblock meshes (9 932 mm2
  and 0 mm2 without escape).

Limitations:

- The steel is a point: the side travel is a lower bound of the travel of a real slide, and an escape through an
  opening narrower than a slide (the steel above the flange disc dropping through a bolt hole) counts as one.
- Undercuts, gaps and steps smaller than 0.1 mm are not resolved.
- The draft thresholds are those of `DraftBasicGeometryAnalyzer`: tessellation tilts of a few hundredths of a
  degree count as negative draft; the warning gives the share steeper than -1 deg apart.
- Only flat two-half splits along X, Y or Z; a triangle cut by the parting plane is assigned whole.

Contradictions left in place and reported:

- The part analysis still adds `UNDERCUT_HEURISTIC_RISK` from the opposing-normal heuristic with +Z opening
  (2 triangles on the 20 x 10 x 5 mm box), while the alpha undercut summary of the same box reports none.
- "Parting plane scorer selected X at 50 mm" on that box: the scorer works on approximate part bounds taken from
  the configuration and centred in the mold block, while the part spans x = 0 to 20 mm and the split used is
  X = 10 mm.
