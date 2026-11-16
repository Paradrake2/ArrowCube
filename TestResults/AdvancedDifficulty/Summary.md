# Advanced difficulty verification

Verified 2026-11-15 with Unity 6000.6.7f1 and the independent .NET 10 probe.

The original generator was benchmarked before changing construction. The final paired run uses 70 seeds, five presets and all three exit modes: **1,050 new puzzles and 1,050 matched version-2 puzzles**, with zero failed recipes or accepted solution-validation failures. Each pair uses the same seed, grid, path-length/count bounds, coverage bounds and exit flags. Difficulty analysis and actual gameplay simulations determine the measurements.

## Paired results

| Preset | Mean score | Score P10 / P50 / P90 | Edge failures/removal: old -> new | Change | Mean paths: old -> new | Mean / P95 generation ms | Within target tolerance |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Easy | 15.80 | 11.08 / 14.92 / 22.10 | 1.94 -> 0.49 | -74.9% | 19.2 -> 20.7 | 10.7 / 27.4 | 80.5% |
| Normal | 28.73 | 24.63 / 29.52 / 31.63 | 3.46 -> 3.01 | -12.9% | 37.0 -> 37.8 | 14.0 / 18.7 | 100.0% |
| Hard | 51.91 | 48.37 / 51.55 / 56.08 | 4.04 -> 5.62 | 39.1% | 41.0 -> 37.1 | 20.8 / 26.7 | 99.5% |
| Expert | 62.77 | 55.73 / 62.78 / 69.43 | 4.09 -> 7.06 | 72.4% | 41.6 -> 36.8 | 28.8 / 38.5 | 71.9% |
| Extreme | 66.95 | 57.79 / 66.88 / 75.38 | 4.37 -> 7.98 | 82.5% | 44.8 -> 39.5 | 46.4 / 50.8 | 3.3% |

All presets have 210 samples. Higher failures/removal means the blind strategy makes more mistakes. Scores are engineering heuristics, not validated human difficulty ratings. New generation time includes candidate analysis and optimization; original generation did not perform that work. Measurements are local CPU batch timings, not a mobile performance guarantee.

## Control for path count

Normalize edge mistakes as `2 * failedAttempts / (N * (N - 1))`. This controls for the number of remaining choices. Hard through Extreme improve resistance while averaging fewer arrows than their matched baselines.

| Preset | Normalized edge mistakes: old -> new | Change |
| --- | ---: | ---: |
| Easy | 0.2150 -> 0.0491 | -77.2% |
| Normal | 0.1923 -> 0.1640 | -14.7% |
| Hard | 0.2020 -> 0.3113 | 54.1% |
| Expert | 0.2054 -> 0.3961 | 92.9% |
| Extreme | 0.2053 -> 0.4203 | 104.8% |

## Other blind strategies

Failures per successful removal. Random values average the same seeded alternative-order count for each member of a pair.

| Preset | Projected exit distance: old -> new | Outermost body layer: old -> new | Random: old -> new |
| --- | ---: | ---: | ---: |
| Easy | 0.48 -> 0.18 | 1.94 -> 0.75 | 0.50 -> 0.16 |
| Normal | 0.90 -> 1.05 | 3.57 -> 2.76 | 0.62 -> 0.40 |
| Hard | 0.81 -> 1.12 | 4.34 -> 5.61 | 0.73 -> 1.29 |
| Expert | 0.76 -> 1.71 | 4.57 -> 6.94 | 0.76 -> 1.87 |
| Extreme | 0.74 -> 2.20 | 4.87 -> 7.83 | 0.78 -> 2.30 |

These solvers do not see the stored solution or hidden removability. They retry failed paths only after a state change. Their specific repeated-attempt behavior is not a claim about every human player. All safe removal orders remain valid under the unchanged monotonic gameplay rules.

## Regression evidence

- EditMode: **203/203 passed**.
- PlayMode: **25/25 passed**.
- **1,008 original fingerprints rechecked; zero differences.** Original and finalized-analyzer baselines are retained separately.
- Unity tests cover version-1/version-2 compatibility, a version-3 .NET golden, all three exit modes, all five placement strategies, exact cross-face blocker analysis, deterministic scores, target misses, bounded search, discarded mutations, independent Custom sliders, master/reset behavior, background cancellation/replacement, JSON export and existing desktop/touch controls.
- The original 1,008-puzzle stress suite and larger-grid regression tests also pass in Unity.

## Limits and reproduction

The initial Extreme target of 80-100 is not reliably reached. The UI exposes the actual measured score and an explicit target-miss warning; no failed target is reported as achieved. Human difficulty calibration and large-grid/mobile profiling remain empirical follow-up work. Difficulty never reduces player attempts or changes movement/exit rules.

Full data: [paired benchmark](FinalVerified/benchmark.csv), [final original baseline](BaselineFinal/baseline.csv), [original pre-change baseline](Baseline/baseline.csv), and the empty [benchmark error log](FinalVerified/errors.txt). Earlier tuning folders are diagnostic runs, not additional final samples. See the [implementation and reproduction guide](../../Documentation/Advanced-Procedural-Difficulty.md).
