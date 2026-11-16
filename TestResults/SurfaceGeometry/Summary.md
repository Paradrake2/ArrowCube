# Phase 3 verification

- Full EditMode suite: **330 passed**, zero failures/skips.
- Final full PlayMode suite: **51 passed**, zero failures/skips.
- Additional geometry run: **11 passed**, including the extra oblique concave fold.
- Graphics-enabled Mobile URP PlayMode animation/capture test: **1 passed**.
- Existing 1,008-puzzle cube regression: every fingerprint and deterministic CSV
  field matches `TestResults/SurfacePuzzle/Unity/stage3-generation-1008.csv`.
- Phase 2's engine-independent non-cube logical demo still completes successfully.

The first full PlayMode run passed 50/51. Its single failure was picking an exact
arrow tip on the nonuniformly scaled, rotated oblique prism. Picking now uses
perspective-correct barycentric reconstruction after its screen-space hit, with
actual surface occlusion. The entire 51-test PlayMode suite was rerun and passed.
No failures remain.

## Delivered scenes

- `Assets/Scenes/SurfaceL.unity`: closed 14-cell L-shaped solid with a concave notch,
  convex folds, a blocker and directional gates.
- `Assets/Scenes/SurfacePrism.unity`: closed 12-cell oblique prism, with quadrilateral
  cap partitions, unequal/skew cells and non-right-angle folds.

Both scenes load directly into the standard gameplay controller/HUD. The selector
switches geometry and toggles ordinary edge exits. Default gate-only solutions are
20, 10, 30. Tests load both scene assets and complete the puzzles, as well as
performing real Input System mouse/touch activations in the non-cube controller.

## Artifacts

- `Unity/results-EditMode.xml`: full EditMode results.
- `Unity/results-PlayMode.xml`: final full PlayMode results after the precision fix.
- `Unity/stage3-generation-1008.csv`: unchanged cube generation dataset.
- `Additional/geometry-extra.xml`: focused run including oblique concave geometry.
- `GPU/results-PlayMode.xml`: actual Mobile URP animated rendering result.
- `Visuals/urp-L-*.png`, `Visuals/urp-prism-*.png`: front, rear, blocked contact,
  and moving-fold captures; visually inspected.
- `logical-demo.txt`: retained Phase 2 logical non-cube verification.

GPU captures used the project's Mobile RP asset, opaque/depth-writing materials,
and actual frame-by-frame animation. This does not claim a new WebGL deployment or
testing on physical mobile devices. No procedural shape/puzzle generator, mesh
conversion or difficulty-balancing changes were introduced in this phase.
