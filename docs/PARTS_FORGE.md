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

The script first closes any PicoMoldForge Parts program still running (a viewer left open by an
earlier run keeps the build output locked, and the build then fails with MSB3027), then builds the
Parts projects (Release), runs their tests including the PicoGK native tests,
generates the five sample parts with the reference mesher and the PicoGK cross-check into
`parts-output\`, zips outputs and logs into one file on the Desktop (also when the build fails;
`parts-logs\<stamp>\launcher-summary.txt` inside it records the commit, any closed instances, every
step and the verdict),
prints a PASS/FAIL table and opens the viewer at `http://127.0.0.1:8765/` from its own window
(close that window to stop it). Its exit code is 0 only when every step passed. Options:
`-SkipTests`, `-FullBaseline`, `-NoViewer`, `-Port <n>`.

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
Desktop, Documents and system folders are refused. Every run writes a provisional manifest (status
`INCOMPLETE`) before the first part, so a run that dies half way leaves a folder the next run accepts.
`--clean-output` deletes only files that previous runs wrote (known file names in part folders that
hold a parts report, plus the root files) and removes folders only when they end up empty.

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
- The PicoGK cross-check compares an independent voxel rendering against the reference mesh: volume
  (tolerance max(3 %, a quarter of the area times one voxel)), bounding box (1.5 voxels) and topology
  (the PicoGK STL must be closed, manifold and oriented, with the same number of bodies and the same
  genus, after dropping the zero-area triangles PicoGK's mesher emits). Volume and box alone would
  accept a missing small hole: the flange without its grease hole differs by 0.35 % in volume and not
  at all in the box, but its genus is 7 instead of 8 (`CrossCheckTests`).

Measured with the default sample settings (Linux x64, 2 cores, Release, reference backend; distances
are upper bounds; triangle samples are centroid plus edge midpoints):

| Part | Resolution / tolerance | STL triangles | Genus | Max distance, vertices (sampled) | Max distance, triangles (sampled) |
|------|------------------------|---------------|-------|----------------------------------|-----------------------------------|
| spur-gear | 0.12 / 0.01 mm | 34 626 | 1 | 0.022 mm (all) | 0.065 mm (all) |
| helical-gear | 0.12 / 0.01 mm | 16 630 | 1 | 0.030 mm (all) | 0.064 mm (all) |
| piston | 0.18 / 0.015 mm | 70 994 | 2 | 0.064 mm (all) | 0.108 mm (every 2nd) |
| monoblock | 0.35 / 0.03 mm | 31 850 | 5 | 0.086 mm (all) | 0.233 mm (all) |
| flange | 0.2 / 0.02 mm | 90 754 | 8 | 0.040 mm (all) | 0.100 mm (every 2nd) |

First run on Windows x64 (29 Oct 2026, `run-parts-demo.ps1`, reference mesher plus PicoGK
cross-check): builds without warnings, Parts.Core 100, Parts.Cli 19 and PicoGK native 4 tests passed,
the five parts passed every check and the PicoGK cross-check:

| Part | STL triangles, Windows / Linux | PicoGK voxel | Volume difference | Box deviation | PicoGK genus | Zero-area triangles in the PicoGK STL |
|------|--------------------------------|--------------|-------------------|---------------|--------------|---------------------------------------|
| spur-gear | 34 116 / 34 626 | 0.24 mm | 0.106 % | 0.0034 mm | 1 | 288 |
| helical-gear | 16 342 / 16 630 | 0.24 mm | 0.097 % | 0.0076 mm | 1 | 390 |
| piston | 70 994 / 70 994 | 0.36 mm | 0.663 % | 0.0146 mm | 2 | 0 |
| monoblock | 31 850 / 31 850 | 0.7 mm | 0.054 % | 0.0166 mm | 5 | 0 |
| flange | 90 602 / 90 754 | 0.4 mm | 0.029 % | 0.0025 mm | 8 | 6 912 |

Results differ slightly between operating systems because their floating-point math libraries
differ: on Windows the gears and the flange came out with up to 1.7 % fewer triangles, volumes
differ by at most 0.006 %, and the largest triangle-sample distance of the helical gear was 0.083 mm
instead of 0.064 mm (limit 0.12 mm). Piston and monoblock were identical. Each run reports its own
values in `part-report.json`.

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

The PicoGK 2.0.0 native runtime ships for Windows x64 and macOS arm64 only. On Windows,
`picogk.26.1.dll` imports `MSVCP140.dll`, `VCRUNTIME140.dll` and `VCRUNTIME140_1.dll`, which the
package does not include: the Microsoft Visual C++ 2015-2022 Redistributable (x64) must be installed.
`--backend picogk` or `both` renders each part headless (`new Library(voxelSize)`,
`Voxels(Library, IImplicit, BBox3)`, `mshAsMesh`, `SaveToStlFile`) and writes the cross-check; when
the runtime cannot be loaded the cross-check fails with the reason (platform, missing file or the
loader's error). `picogk-view` opens the native viewer window (`Library.Go`).

The `<id>.picogk.stl` files exist only for the comparison. PicoGK's mesher writes some zero-area
triangles (288 to 6 912 in the samples), which some slicers report as errors; print `<id>.stl`, the
verified reference mesh, which has none.

The PicoGK native tests are opt-in so the default `dotnet test` never depends on the runtime:

    $env:PICOMOLDFORGE_PARTS_PICOGK_TESTS = "1"
    dotnet test tests\PicoMoldForge.Parts.PicoGK.Tests -c Release

`run-parts-demo.ps1` sets the variable for that test project.

## Limitations

- Piston and monoblock are simplified geometry for demonstration (no wrist-pin offset, no oil drain
  holes, no head, no ports, no casting draft).
- Gears: ISO 53 profile A rack generation with profile shift and thickness allowance; no tip relief,
  crowning or internal gears.
- Simplification runs on one thread: 5 to 14 s per sample part on the Windows machine of the first
  run, about 1.5 minutes for all five on a 2-core Linux machine.
- `picogk-view` (the native PicoGK window) was run on Windows on 29 Oct 2026 with the helical gear:
  PicoGK's log closed normally and the command exited with 0 after the window was closed. Like the
  headless rendering and the cross-check (first Windows run above), it is exercised by hand, not by
  the automated tests.
