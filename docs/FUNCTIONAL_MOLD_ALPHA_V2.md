# Functional Mold Alpha v2

Functional Mold Alpha v2 connects the alpha metrics of `FunctionalMoldAlphaReport` to the actual geometry of
the part instead of values derived from the configuration. It follows the priority order of the README.

| # | Priority | Status |
|---|----------|--------|
| 1 | Derive wall thickness from the part geometry | Done: measured on the input STL |
| 2 | Derive draft and undercut risk from actual mesh normals | Next |
| 3 | Emit real gate/runner/sprue geometry artifacts | Open |
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
