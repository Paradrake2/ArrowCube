# Independent multi-blocker verification

Verified 2026-11-15. Unity 6000.6.7f1; independent .NET 10 probe. All relationships arise from unchanged movement/collision rules.

## Full preset comparison

**1,050 new puzzles and 1,050 original version-3 puzzles**: 70 seeds x five presets x three exit modes, on 6x6/8x8 grids. Each pair uses the same seed, path/geometry limits, coverage targets and exits. New hard-tier presets include stronger logical targets and larger deterministic search budgets. No accepted puzzle failed complete solution replay.

| Preset | Confirmed blockers / blocked path | Independent multi-blocker paths (%) | Independent 3+ paths (%) | Dependency depth | Cross-face geometry links (%) | Initial safe (%) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Easy | 1.17 -> 1.18 | 2.6 -> 2.6 | 0.3 -> 0.3 | 2.19 -> 2.18 | 58.7 -> 57.1 | 84.5 -> 84.2 |
| Normal | 1.34 -> 1.56 | 6.3 -> 13.4 | 2.2 -> 3.8 | 3.60 -> 4.54 | 59.2 -> 52.8 | 69.2 -> 59.5 |
| Hard | 1.82 -> 1.96 | 25.2 -> 29.9 | 8.3 -> 8.5 | 6.25 -> 7.05 | 60.4 -> 56.9 | 37.6 -> 36.9 |
| Expert | 1.84 -> 1.96 | 27.6 -> 30.1 | 7.6 -> 7.8 | 8.19 -> 9.82 | 62.9 -> 62.3 | 29.1 -> 26.5 |
| Extreme | 1.90 -> 1.97 | 28.2 -> 30.7 | 8.3 -> 7.4 | 9.86 -> 11.63 | 62.5 -> 62.4 | 26.2 -> 23.7 |

All pairs above are old -> new. Independent counts exclude redundant blockers in the same prerequisite chain. Cross-face classification uses actual contact faces and blocker geometry; an unrelated cross-face target tail does not qualify.

| Preset | Edge mistakes / removal | Change | Normalized edge mistakes | Paired delta 95% interval | Mean paths | Coverage (%) | Mean generation ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Easy | 0.49 -> 0.50 | 2.4% | 0.0491 -> 0.0506 | -0.01 to 0.03 | 20.7 -> 20.6 | 59.2 -> 59.1 | 9.9 -> 10.4 |
| Normal | 3.01 -> 3.20 | 6.2% | 0.1640 -> 0.1811 | 0.08 to 0.29 | 37.8 -> 36.4 | 63.3 -> 63.3 | 15.3 -> 16.8 |
| Hard | 5.62 -> 6.82 | 21.5% | 0.3113 -> 0.3740 | 1.07 to 1.35 | 37.1 -> 37.5 | 68.4 -> 68.3 | 20.4 -> 52.0 |
| Expert | 7.06 -> 7.67 | 8.7% | 0.3961 -> 0.4328 | 0.44 to 0.79 | 36.8 -> 36.9 | 72.6 -> 72.2 | 24.2 -> 119.9 |
| Extreme | 7.98 -> 8.63 | 8.1% | 0.4203 -> 0.4547 | 0.48 to 0.82 | 39.5 -> 39.3 | 76.4 -> 75.6 | 51.1 -> 186.9 |

Higher mistakes mean lower effectiveness for the blind edge-first strategy. The exploratory 95% interval uses 70 seed-level mean differences, averaging the three exit modes within each seed before calculating mean +/- 1.96 standard errors. These are deterministic simulation measurements, not human-playtest validation. Normalized mistakes are `2 * failures / (N * (N - 1))`, controlling for the number of arrow choices. Timing includes validation, graph proofs, heuristic solves and optimization; local CPU timings are not a mobile-device benchmark.

## Feature-toggle control

A separate **300-pair** comparison holds the new logical settings and search budgets fixed, then toggles only multi-blocker construction. This prevents attributing all full-release gains to the toggle alone.

| Preset | Independent multi-blocker paths (%): off -> on | Edge mistakes / removal: off -> on |
| --- | ---: | ---: |
| Easy | 2.3 -> 2.3 | 0.52 -> 0.52 |
| Normal | 7.4 -> 13.9 | 3.09 -> 3.16 |
| Hard | 25.3 -> 30.0 | 5.78 -> 6.68 |
| Expert | 27.0 -> 29.2 | 7.82 -> 7.51 |
| Extreme | 29.9 -> 29.9 | 8.78 -> 8.73 |

The isolated toggle has mixed strategy effects, including some Expert/Extreme regressions, despite improved dependency distributions in several cases. The full version-4 preset/search package improves resistance against version 3; the data does **not** establish that enabling multi-blockers always makes every puzzle harder. Safe removals still commute. No universal human-difficulty claim is made.

## Solvability, compatibility and tests

- **1,050/1,050 version-3 reference fingerprints unchanged.** Versions 1 and 2 retain their golden tests and original code paths.
- EditMode: **224/224 passed**.
  - LargerCubesUseTheSameVerifiedGraphAndRespectTheFastBudget(16): 0.129386 seconds including generation and explicit replay.
  - LargerCubesUseTheSameVerifiedGraphAndRespectTheFastBudget(32): 2.268731 seconds including generation and explicit replay.
- PlayMode: **26/26 passed**.
- Tests cover full-body cells/segments, zero through three independent blockers, bounded 5/14-blocker verification, cross-face contacts, early gates, all exit modes, redundant chains, cycles, deep snapshots, disabled mode, cache invalidation, budgets, determinism, cross-runtime goldens, discarded mutations, independent Custom controls, debug click selection, JSON/DOT exports and existing desktop/touch controls.
- Accepted targeted replacements/insertions in the final corpus: **49**. Default replacement mode preserves path count in that pass; ordinary construction can produce different path counts within the same bounds.

## Limits and data

Preset blocker fractions/averages remain soft targets and are often missed, especially at Expert/Extreme. The UI reports actual values and target-miss warnings. Subset enumeration is exhaustive only through three blockers; larger sets have singleton/zero/full proofs under fixed-route rules. Independent width is exact through twelve blockers, then a verified conservative lower bound. Future gameplay with selectable alternate routes requires re-evaluating mandatory classifications.

Data: [final paired CSV](FinalVerified/benchmark.csv), [same-budget toggle CSV](ControlVerified/benchmark.csv), [empty final error log](FinalVerified/errors.txt), [empty control error log](ControlVerified/errors.txt), and [implementation guide](../../Documentation/MultiBlocker-Dependencies.md). Earlier calibration folders are diagnostic runs, not extra final samples. Recreate this summary with `Tools/SummarizeMultiBlockerBenchmark.ps1`.
