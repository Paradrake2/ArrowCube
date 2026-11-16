# Phase 2 verification

- Engine-independent .NET build and authored non-cube demo: passed.
- Final Unity 6000.6.7f1 EditMode suite: 320 passed, zero failures/skips.
- Final Unity 6000.6.7f1 PlayMode suite: 45 passed, zero failures/skips.
- New generic puzzle/save tests: 16 passed, included in the EditMode total.
- Direct before/after comparison: 54 recipes, all six profiles and three exit
  modes; fingerprints and deterministic CSV fields unchanged. Timings excluded.
- Existing cube stress suite: 1,008 generated puzzles successfully replayed.
- All 1,008 fingerprints and deterministic CSV fields match
  `TestResults/Stage5/Final/stage3-generation-1008.csv`.
- Existing recipe goldens, legacy JSON/session tests, cube geometry, input,
  controls, blocked-arrow animation, scene handoff and Web-generation operation
  tests passed.

The first full EditMode run passed 318/319 tests. Its single failure was an
exception-type mismatch for a malformed generic save heading. Saved-data validation
was corrected and the final full suites rerun; explicit parallel-body connections
and their serialization are included in the final results. No failures remain.

## Authored demonstration

`ManualSurfacePuzzle.Create()` returns an immutable 16-cell non-cube logical puzzle.
Arrow 10 is blocked by 20 at cell 115. Arrow 20 is blocked by 30 at cell 317.
Only arrow 30 is initially free. The verified removal order is 30, 20, 10, using
directional gates. Final path count, cell occupancy and segment occupancy are zero.
See `demo.txt` for the executable trace, including transported head routes.

The same fixture is verified in edge-only and hybrid modes. Tests also exercise
concave/convex traversal, self-collision, tail vacating, no-exit cycles, stale
commits, foreign topology rejection, parallel segments, cache identity/cancellation,
generic saves, undo history and backup recovery.

## Artifacts

- `before.csv`, `after.csv`: direct deterministic comparison around the refactor.
- `core.csv`: intermediate shared-simulator comparison.
- `demo.txt`: successful headless non-cube puzzle trace.
- `Unity/results-EditMode.xml`, `Unity/results-PlayMode.xml`: final full results.
- `Unity/stage3-generation-1008.csv`: current full cube regression.
- `Unity/editor-*.log`: full Unity logs (local artifacts ignored by Git).

Rendering and procedural puzzle-generation algorithms remain cube-based through
adapters. No non-cube rendering, mesh conversion, procedural shapes, recipe-version
changes or difficulty-balancing changes were introduced.
