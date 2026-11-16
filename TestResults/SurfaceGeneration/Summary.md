# Phase 4 verification

Unity 6000.6.7f1, Windows, 2026-11-16.

- Full EditMode suite: **352/352 passed**.
- Full PlayMode suite: **53/53 passed**.
- Final Mobile URP procedural surface gameplay/UI suite: **2/2 passed**.
- Historical cube corpus: **1,008/1,008** fingerprints and every deterministic CSV field match Phase 3. Elapsed time is excluded. See `cube-regression-comparison.json`.
- Initial before/after .NET sample: **54/54** cube puzzles match in all deterministic fields.
- .NET generation probe compiles with zero warnings/errors.
- `git diff --check` passes.

`Unity/results-EditMode.xml` and `Unity/results-PlayMode.xml` are the final full-suite results. `GPU/results-PlayMode.xml` verifies the final responsive settings panel and procedural scene under Mobile URP. The earlier initial test pass is retained in `Initial/` as development evidence; the final results above supersede it.

The new EditMode cases cover nine controlled shape/exit-mode combinations, independent blockers, hard opening caps, complete removal replay, valid solo routes, coverage, repeated generation, binary recipe snapshots, saved-session and geometry restoration, shape identity/cell enumeration, measured search/mutation/tuning, gate-only and hybrid generation without eligible edges, explicit cycle rejection, single coplanar regions, 324-region evidence, progress, cancellation, impossible constraints and three surface resolutions.

All nine controlled examples have independent multiple blockers, verified dependency depth 6?14, and 1?3 free openings. Their exact fingerprints and metrics are in `generic-generation-samples.csv`.

The new PlayMode tests use the delivered `SurfaceGeneration` scene and the standard gameplay controller. They test cooperative generation, actual arrow picking after camera focus, blocked attempts without occupancy changes, removal animations through the verified solution, victory, runtime seed/shape controls, measured search, sandbox save restoration and cancellation preserving the old puzzle.

Six front/rear Mobile URP captures were visually inspected. They show continuous arrows on the L's concave surfaces, oblique prism folds, and both T arms/stem with distinguishable face colors. See `GPU/contact-sheet.png` and the six original `GPU/phase4-*.png` images. These are actual depth-tested render captures, not mockups.

The full suite initially caught two compatibility regressions: early rejection changed an old cube failure report's counters, and the cube debug inspector's label changed from ?Faces? to ?Regions?. Both were corrected while retaining generic behavior; final full suites pass. Expanded generic tests also prompted filtering targeted mutations that have no free room on their candidate trajectory. The responsive settings-panel width check covers horizontal clipping.

Architecture, file list, recipe contract, metrics, lab instructions, representative performance timings and Phase 5 recommendations: `Documentation/ArbitrarySurface-Phase4.md`.
