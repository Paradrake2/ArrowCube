# Phase 5 verification

Unity 6000.6.7f1, Windows, 2026-11-16.

- Full EditMode suite: **379/379 passed**.
- Full PlayMode suite: **56/56 passed**.
- Final focused conversion suite after numerical reader/asset checks: **27/27 passed**.
- Final Mobile URP converted-mesh gameplay suite: **3/3 passed**.
- Historical cube corpus: **1,008/1,008** fingerprints and all deterministic CSV fields match Phase 4. Only elapsed time is excluded. See `cube-regression-comparison.json`.
- Final isolated editor compilation/example-authoring check completed with exit **0**; see `authoring-final.log`.
- `git diff --check` passes.

`Unity/` contains the final full-suite XML, editor logs, 1,008-puzzle CSV and the empty generation-errors report. `Focused/` contains the final 27 conversion tests. `GPU/` contains the final three Mobile URP tests and twelve actual render captures. `Initial/` retains early development evidence; final results supersede it.

The converted scene was loaded from the delivered assets. Tests used Input System event injection for mouse and touch to solve all four models through the normal gameplay controller, including a blocked attempt, fold-crossing animation, complete removal, victory, undo and saved-geometry restoration. They also test the runtime shape selector, measured difficulty, and an undersized converted mesh whose conflicting recipe is reported without losing the old puzzle.

The four native models, uniquely paired triangles and explicit companion path all convert. Tests deliberately reject ambiguous matching, degenerate/warped faces, mixed/inward winding, open edges, disconnected and non-manifold shells, a genuinely self-intersecting shell, incompatible subdivisions, excessive cell counts/density, too-small cells, severe folds, ambiguous welding, bad OBJ data and nonfinite coordinates. They verify canonical ordering, shared seam positions, reversible directions, fold/exit flags, exact asset/recipe round-trips, and baked examples matching conversion of their source buffers.

All 1,008 historical cube deterministic records match. No generator, movement simulator or historical recipe/save code was changed. Existing authored-surface and cube gameplay/rendering regressions also pass, including the cube's six distinct face materials.

`generated-samples.csv` records the four controlled examples' topology and generation metrics. They have 7?21 arrows, 8?37 dependency links and verified depth 4?8. `conversion-performance.csv` records 12 model/resolution samples with final validation time, net managed-memory delta, generation time and candidate/rejection counts. `render-observations.csv` records CPU render-request submission timings and the three/four batched surface material groups. These are machine observations, not peak-memory or sustained-GPU-FPS guarantees; first-render shader warm-up is included.

Representative final n=8 samples:

| Model | Cells | Conversion ms | Final validation ms | Generation ms | Candidates / rejected |
|---|---:|---:|---:|---:|---:|
| RectangularPrism | 384 | 8.20 | 1.43 | 127.99 | 648 / 297 |
| L | 896 | 35.47 | 4.04 | 357.87 | 1536 / 727 |
| T | 1152 | 49.44 | 6.01 | 488.70 | 1944 / 985 |
| AngularPrism | 768 | 31.92 | 12.31 | 303.28 | 1440 / 696 |

All twelve Mobile URP images were visually inspected. Front/rear views show the rectangular prism, L, T and oblique prism; the third image of each model is captured during an actual removal animation. Arrows follow colored surface regions, including inside corners and non-right-angle faces, with opaque depth-tested rendering. See `GPU/contact-sheet.png` and the originals.

During development, the numerical intersection test was tightened for rotated nearly coplanar triangles. The input test was corrected to respect the existing 0.25-second touch-to-mouse synthetic-event suppression rather than changing controls. The self-intersection fixture was adjusted to actually cross its shell; both malformed conversion and correct geometry now have explicit coverage.

Architecture, complete file list, authoring contract, tool instructions, input limitations, persistence and future work: [Phase 5 guide](../../Documentation/ArbitrarySurface-Phase5.md).
