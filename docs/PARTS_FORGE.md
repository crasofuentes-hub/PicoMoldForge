# PicoMoldForge Parts

PicoMoldForge Parts generates mechanical parts (CAD geometry) from JSON recipes: spur and helical
involute gears, a simplified piston, a simplified two-cylinder engine block ("monoblock") and generic
feature recipes. It combines two things:

- the **"modo malla" view**: a lattice of points that fills feature by feature while each 2D profile
  travels along its trajectory (it "drops" along an axis or "turns" about one) until the solid appears;
- the **PicoGK voxel kernel** used by the rest of PicoMoldForge, as a second, independent renderer.

Every part is described once, as an ordered list of features. The reference mesher, the PicoGK renderer,
the viewer outlines, the lattice animation and every verification read that same description.

Out of scope by design: tool paths and G-code for 5/6-axis machines (CAM), STEP export and any
certification. The geometry is for engineering review; it is not certified for manufacturing.

## Axes (ISO 841)

| Axis | Meaning |
|------|---------|
| X, Y, Z | linear axes (right-handed) |
| A | rotation about X |
| B | rotation about Y |
| C | rotation about Z |

Rotations follow the right-hand rule. An `orientation` applies A, then B, then C about the fixed axes:
`R = Rz(C) * Ry(B) * Rx(A)`. The order matters (see `AxisConventionTests`).

Profile frames for linear sweeps (profile coordinates `(u, v)`, sweep coordinate `w`):

| Sweep axis | u | v | w |
|-----------|---|---|---|
| Z | X | Y | Z |
| X | Y | Z | X |
| Y | Z | X | Y |

A rotary sweep about A, B or C uses the same frame of X, Y or Z: the profile lives in the
(radial, axial) plane and turns from `startDeg` to `endDeg`.

Combined motion is a linear sweep with a twist: a helical gear is its transverse profile dropping along
Z while it turns in C (right-hand helix: +C as Z grows).

## Projects

| Project | Target | Role |
|---------|--------|------|
| `src/PicoMoldForge.Parts.Core` | net8.0 | Geometry engine: signed distance fields, ISO 841 axes, profiles, sweeps, gears, templates, recipes, reference mesher (marching cubes + QEM simplification), verification, STL and viewer exports. No PicoGK dependency. |
| `src/PicoMoldForge.Parts.PicoGK` | net9.0 | PicoGK 2.0.0 adapter: renders the same distance field to voxels, mesh and STL (headless), and opens the PicoGK viewer. |
| `src/PicoMoldForge.Parts.Cli` | net9.0 | Command line, local viewer server and the embedded "modo malla" viewer page and sample recipes. |
| `tests/PicoMoldForge.Parts.Core.Tests` | net8.0 | Unit and pipeline tests. |
| `tests/PicoMoldForge.Parts.Cli.Tests` | net9.0 | Command line, output folder and viewer server tests. |
| `tests/PicoMoldForge.Parts.PicoGK.Tests` | net9.0 | PicoGK native tests (opt-in, see below). |

The existing mold projects are not modified and do not reference these projects.

## Quick start (Windows)

From the repository root:

    powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-parts-demo.ps1

The script builds the Parts projects (Release), runs their tests including the PicoGK native tests,
generates the five sample parts with the reference mesher and the PicoGK cross-check into
`parts-output\`, zips outputs and logs to the Desktop and opens the viewer at
`http://127.0.0.1:8765/`. Close the viewer with Ctrl+C. Options: `-SkipTests`, `-FullBaseline`,
`-NoViewer`, `-Port <n>`.

Manual commands (any OS with the .NET 9 SDK; PicoGK only on Windows x64 and macOS arm64):

    dotnet build src/PicoMoldForge.Parts.Cli -c Release
    dotnet src/PicoMoldForge.Parts.Cli/bin/Release/net9.0/PicoMoldForge.Parts.Cli.dll demo --output parts-output
    dotnet src/PicoMoldForge.Parts.Cli/bin/Release/net9.0/PicoMoldForge.Parts.Cli.dll view --output parts-output

## Commands

| Command | What it does |
|---------|--------------|
| `demo` | Generates and verifies the sample parts (`--only spur-gear,piston` for a subset). |
| `generate --recipe <file>` or `--sample <name>` | Generates and verifies one part. |
| `view` | Serves an output folder on 127.0.0.1 and opens the viewer (`--port 0` picks a free port, `--no-browser`). |
| `picogk-view --sample <name>` | Opens the part in the native PicoGK viewer window. |
| `samples` | Lists the embedded recipes; `--export <folder>` copies them for editing. |
| `selftest` | Quick internal check of the engine. |

Common options: `--output <folder>` (default `parts-output`), `--backend reference|picogk|both`,
`--resolution <mm>`, `--quick` (each part at twice its resolution), `--voxel <mm>` (PicoGK voxel size),
`--clean-output`.

Exit codes: 0 all checks passed, 1 some check failed, 2 usage or recipe error, 3 unexpected error.

Output folder safety: the output folder must be new, empty or a previous PicoMoldForge Parts output
(recognized by its `RunManifest.json` schema); filesystem roots, the current folder, the user profile,
Desktop, Documents and system folders are refused. `--clean-output` deletes only files that previous
runs wrote (known file names in part folders that hold a parts report, plus the root files) and removes
folders only when they end up empty.

## Recipes

Schema: `docs/schemas/picomoldforge.part-recipe.schema.json` (`picomoldforge.part-recipe.v1`). The
loader rejects unknown properties and reports every error with its JSON path. A recipe has exactly one of:

- `template`: `spurGear`, `helicalGear`, `piston` or `monoblock` with their parameters
  (see `samples/parts/*.json` for every parameter and its default);
- `features`: an ordered list where each feature is a **profile** plus a **trajectory** plus an
  operation (`add`, `subtract`, `intersect`; the first one must add):

```json
{
  "id": "bolt-holes",
  "op": "subtract",
  "sweep": { "kind": "linear", "axis": "Z", "from": 11, "to": -1 },
  "profile": { "type": "circle", "diameter": 9 },
  "origin": [38, 0, 0],
  "pattern": { "type": "circular", "axis": "C", "count": 6 }
}
```

Profiles: `circle`, `ring`, `rectangle` (optional `cornerRadius`), `polygon` (even-odd loops),
`halfSection` (a half profile that touches the rotation axis, mirrored automatically), `union`,
`difference`. Placement: `origin` or `positions`, optional `orientation` `{A, B, C}`, optional
`pattern` (`circular` about A/B/C or `linear` along X/Y/Z). Feature recipes may add `expect`
(`genus`, `boundingBox`, `surfacePoints`) and `mesh` settings.

## Outputs

Per part (`<output>/<id>/`):

| File | Content |
|------|---------|
| `<id>.stl` | Binary STL in millimeters (simplified mesh), deterministic bytes. |
| `part-report.json` | Parameters, mesh summaries, measured distances, every check with expected and actual values, timings. |
| `recipe.json` | The recipe used. |
| `preview.bin` | Viewer mesh (`PMFMESH1`: 16-bit quantized positions, 32-bit indices). |
| `features.json` | Feature list with frames, instances and profile outlines (viewer). |
| `lattice.bin` | "Modo malla" lattice (`PMFLAT01`): for each point, the feature step at which it becomes solid. |
| `picogk-crosscheck.json`, `<id>.picogk.stl` | With `--backend picogk` or `both`. |

Root: `index.html` (viewer), `parts.json` (index), `summary.txt` and `RunManifest.json` (size and
SHA-256 of every artifact).

## Verification

Each part is meshed from the distance field and then checked by measurements that do not trust the
mesher. What each check proves:

| Check | Meaning |
|-------|---------|
| `mesh.watertight` | Raw and STL meshes are closed, manifold, consistently oriented, normals outward. |
| `mesh.single-body` | One connected component. |
| `mesh.topology` | Genus (through holes) measured on the mesh equals the expected value. |
| `mesh.bounding-box` | Mesh extents within one mesh resolution of the expected box. |
| `mesh.volume-crosscheck` | Mesh volume against an independent voxel count of the field's sign. |
| `mesh.simplification-volume` | Simplification changes the volume by at most 0.5 %. |
| `mesh.simplification-fidelity` | Field value at every STL vertex within the simplification tolerance. This is what the simplifier guarantees; it is not a geometric distance. |
| `mesh.surface-distance` | Geometric distance from the STL to the ideal surface, measured as an upper bound at vertices and at the centroid and edge midpoints of triangles, at most one mesh resolution. |
| `viewer.lattice-replay` | The lattice animation, replayed feature by feature, reproduces the solid exactly. |
| template checks | Gears: pitch tooth thickness, involute flank, tip and root diameters, tooth count, tooth phase at mid height, bore, keyway and helix twist, measured on the 3D solid. Piston, block and feature recipes: surface points of the key dimensions. |

How to read the numbers:

- The distance field is a bound, not a distance: it may underestimate near feature edges and inside
  twisted (helical) sweeps. Geometric distances in the report come from `SurfaceDistanceProbe`, which
  walks from each sample until the exact sign of the field changes and bisects; every reported value
  is a distance at which a real surface point was found (an upper bound).
- `mesh.surface-distance` samples points; it is not a full Hausdorff bound over every triangle.
  Production meshes are sampled completely or with stride 2 (50 000 samples per kind).
- Marching cubes flattens sharp edges by up to about half a resolution step. For sharp-edge accuracy
  use a finer `resolutionMm`.
- The PicoGK cross-check compares volume (tolerance max(3 %, a quarter of the area times one voxel))
  and bounding box (1.5 voxels) of an independent voxel rendering against the reference mesh.

Measured with the default sample settings (Linux x64, 2 cores, Release, reference backend; distances
are upper bounds; triangle samples are centroid plus edge midpoints):

| Part | Resolution / tolerance | STL triangles | Genus | Max distance, vertices (sampled) | Max distance, triangles (sampled) |
|------|------------------------|---------------|-------|----------------------------------|-----------------------------------|
| spur-gear | 0.12 / 0.01 mm | 34 626 | 1 | 0.022 mm (all) | 0.065 mm (all) |
| helical-gear | 0.12 / 0.01 mm | 16 630 | 1 | 0.030 mm (all) | 0.064 mm (all) |
| piston | 0.18 / 0.015 mm | 70 994 | 2 | 0.064 mm (all) | 0.108 mm (every 2nd) |
| monoblock | 0.35 / 0.03 mm | 31 850 | 5 | 0.086 mm (all) | 0.233 mm (all) |
| flange | 0.2 / 0.02 mm | 90 754 | 8 | 0.040 mm (all) | 0.100 mm (every 2nd) |

Numbers on other machines may differ in the last digits (floating-point math libraries differ between
operating systems); each run reports its own values in `part-report.json`.

## Viewer ("modo malla")

`view` serves the output folder on `127.0.0.1` (browsers block `fetch()` from `file://` pages) with
`Cache-Control: no-store`. For each part the page shows:

- the lattice points, which turn solid in feature order;
- the feature profiles travelling along their trajectory (linear drops with gravity easing, twists,
  rotary turns) with a short trail;
- the final solid (the same mesh as the STL unless it exceeds the preview budget);
- construction steps, measurements, parameters, every check, the ISO 841 axes with A/B/C arcs, a cut
  plane along X, Y or Z and standard views. The STL can be downloaded from the page.

The page loads three.js r160 from jsDelivr, so the first load needs internet access.

## PicoGK

The PicoGK 2.0.0 native runtime ships for Windows x64 and macOS arm64 only. `--backend picogk` or
`both` renders each part headless (`new Library(voxelSize)`, `Voxels(Library, IImplicit, BBox3)`,
`mshAsMesh`, `SaveToStlFile`) and writes the cross-check; on other platforms the cross-check reports
that the runtime is not available. `picogk-view` opens the native viewer window (`Library.Go`).

The PicoGK native tests are opt-in so the default `dotnet test` never depends on the runtime:

    $env:PICOMOLDFORGE_PARTS_PICOGK_TESTS = "1"
    dotnet test tests\PicoMoldForge.Parts.PicoGK.Tests -c Release

`run-parts-demo.ps1` sets the variable for that test project.

## Limitations

- Piston and monoblock are simplified geometry for demonstration (no wrist-pin offset, no oil drain
  holes, no head, no ports, no casting draft).
- Gears: ISO 53 profile A rack generation with profile shift and thickness allowance; no tip relief,
  crowning or internal gears.
- Simplification runs on one thread; on 2 cores the five samples take about 1.5 minutes in Release.
- The PicoGK path compiles against the real PicoGK 2.0.0 package but was not executed in the
  environment where this module was written (Linux); run the launcher on Windows to exercise it.
