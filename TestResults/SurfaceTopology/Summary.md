# Surface topology refactor verification

- Engine-independent .NET build: passed, zero warnings/errors.
- Unity 6000.6.7f1 EditMode: 304 passed, zero failures/skips.
- Unity 6000.6.7f1 PlayMode: 45 passed, zero failures/skips.
- Direct before/after comparison: 54 recipes across all six profiles and three
  exit modes; identical fingerprints and deterministic CSV fields. Timings excluded.
- Existing stress suite: generated and replayed 1,008 puzzles successfully.
- Historical comparison: all 1,008 fingerprints match
  `TestResults/Stage5/Final/stage3-generation-1008.csv`.
- Existing historical recipe golden tests and six new pre-refactor version-5
  fingerprints passed.
- No detected regressions. Production generation code, recipe versions, gate
  storage, save formats, rendering, and dependency scoring were not changed.

`before.csv` and `after.csv` were captured around the refactor using the existing
GenerationProbe tool. `Unity/` contains test XML results, logs, and the full
generation regression CSV. See `Documentation/SurfaceTopology.md` for the architecture,
file list, and remaining cube-specific systems.
