# Startup and large custom grids

Verified 2026-11-15 with Unity 6000.6.7f1.

- EditMode: 190/190 passed, including the 1,008-puzzle built-in profile stress suite.
- PlayMode: 23/23 passed. Scene-style activation constructs the controls after all
  component Awakes; the sensitivity slider updates the initialized orbit controller.
- An impossible initial recipe leaves the renderer empty and the error visible,
  rather than loading the manual four-arrow puzzle. The same session then loads and
  renders a corrected 30x30 custom puzzle. Rejected regeneration retains an already
  playable generated level.
- Independent .NET stress: 300/300 puzzles generated and replayed, using 20 seeds
  for each of 29x29, 30x30, 32x32, 48x48 and 64x64, in each of edge-only, gate-only
  and hybrid modes. See `large-grid-stress.csv`.
- Mandatory coverage, bends, dependencies, six-face connectivity and exit-mode
  requirements remain enforced. The coverage target remains a soft objective.
- New recipes use generation version 2. Version 1 remains supported and its original
  golden fingerprint passes in Unity. Recipes contain the effective gate budget.

The baseline probe reproduced gate-only failures with the original fixed 48-gate
budget and 64x64 edge/hybrid failures with the original construction. Default custom
assets now scale their gate budget with area above 8x8; disabling the Inspector
toggle preserves an absolute cap. Edge-only puzzles never create gates.

`baseline.csv`, `scaled.csv` and `scaled2.csv` are diagnostic runs; the final stress
results are `large-grid-stress.csv`. XML results and Editor logs accompany this report.
CPU generation/replay checks and batch PlayMode checks are not a mobile performance
benchmark.
