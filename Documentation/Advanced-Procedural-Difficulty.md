# Advanced procedural difficulty (generation version 3)

New recipes use version 5. See [Opening moves and blocker controls](Opening-Moves-And-Blockers.md)
for self-clearance and controls that work with measured search disabled.

**Version 4 adds confirmed independent multi-blocker generation.** See
[Multi-blocker dependencies](MultiBlocker-Dependencies.md) for the new controls,
proofs, structural score modifiers, budgets, debug inspection and comparison data.
The version-3 behavior described below remains available for saved recipes.

The controls demo now starts with **Normal** and measured difficulty search. Select
the puzzle root's **Stage 3 Generation Demo** to assign an optional difficulty
profile asset, seed and independent exit switches. In Play mode use the controls
panel's preset button, master slider, and **Show / hide advanced settings**. Generation
runs on a worker thread; the panel displays progress and supports cancellation.
The existing level remains playable until the new layout has passed verification.

## Presets, master and Custom

The five built-in presets are **Easy, Normal, Hard, Expert, Extreme**, initially
targeting 10, 30, 50, 70 and 90 respectively, with a default tolerance of 10. These
are search targets, not a promise or a calibrated human rating. The panel shows the
requested target, an **unverified configuration estimate**, and the actual simulator-
measured score after generation. A target miss returns the best valid candidate
with a visible warning. Mandatory geometry/coverage bounds are never relaxed.

Normal through Extreme share an **8x8 grid and 2–12-cell path length limits**. They
change logical structure, gate sharing, turn and cross-face preferences, coverage,
and search budgets. Easy uses 6x6. The benchmark compares each new puzzle with a
version-2 puzzle at the **same seed, grid, path limits, coverage bounds and exit mode**.

The **master slider** explicitly applies a coherent configuration. An individual
Custom slider changes only its own field; it does not rerun the master mapping or
overwrite unrelated settings. **Reset to preset defaults** restores the selected
preset and both exit methods. Quality selection explicitly resets search budgets
only. Changing settings affects the next generation, not the current verified level.
Vertical swipes on slider rows scroll the panel without editing a setting;
horizontal drags and clean taps edit the slider. Hover or press a slider for help.

Create an editable asset with **Assets > Create > Arrow Cube > Difficulty Profile**.
Its Inspector provides presets, a master slider, independent logical sliders,
placement strategy, hard path-count cap, search budgets and scoring weights. Assign
the asset to **Custom Profile**. Runtime control edits use a private working copy;
they do not overwrite the asset on disk. Edit the original asset outside Play mode
to save tuning. Grid sides remain supported from 4 through 64; not every combination
of path count, lengths, coverage and gate cap is feasible.

Cube-edge exits and directional gates remain separate gameplay flags, both enabled
by default. Edge-only, gate-only and hybrid levels all use the same movement engine.
Gate area scaling still has an independent toggle. Hearts, attempts and retries are
not part of the difficulty system: **unlimited player attempts remain available**.

## What the analyzer measures

`PuzzleDifficultyAnalyzer` and `HeuristicPuzzleSolver` are pure C# with no Unity
references. The analyzer measures initial safe/blocked counts and head-face
distributions, body length/turn histograms, cross-face bodies, coverage, local
four-neighbor density and its deviation, and Pearson correlation between head edge
distance and initial removability.

For each path it simulates the unobstructed route, then finds the other path owners
on that route. These are **necessary blockers under the fixed geometry and static
exit rules**, not just dependencies observed in one chosen solution. Tests verify
each reported link using a two-path gameplay simulation. The dependency graph gives
exact longest chain, sequential parallel-removal layers, average depth, and a 6x6
head-face dependency matrix. Cycles or disagreement with the full gameplay simulator
are rejected. Cross-face dependency classification uses the two arrowhead faces;
cross-face body transitions are measured separately.

At sampled states along the nearest-edge solver's actual removal order the report
records remaining safe/blocked paths and newly unlocked moves. It records mean/max
unlocks, minimum/mean safe fraction, and changes to the safe set. Seeded alternative
random orders are also tested: Fast uses one, Balanced two, High Quality four, with
a Custom limit of eight. Their individual outcomes and means are exported.

There is a structural limit to these rules: **safe removals commute**. Removing a
safe path cannot add a blocker, and any order of safe removals solves a valid puzzle.
The analyzer reports this explicitly. Difficulty here means inspection and discovery
effort, not choosing a safe move that can later lose the puzzle. No move-order trap
or player-study claim is inferred from the score.

## Strategy tests and scoring

The four strategy solvers use nearest head edge distance, shortest projected exit
distance, outermost body layer, or seeded random choice. Rankings use visible
geometry/static exits, with seeded tie-breaking independent of the construction
order. A solver only learns success or failure by calling `PathMovementSimulator`.
It has no access to the stored solution, dependency graph, or hidden safe-move list.
It never retries the same failed path until the state changes.

All solvers run to completion or an actual no-progress state. Reports include
successful removals, failed/total attempts, success rate and failures per removal.
Attempt traces retain the first 4,096 entries to bound debug/export memory; a flag
marks truncation. Counts and complete successful-removal orders are not truncated.

The 0–100 weighted score defaults to 30% edge resistance, 25% dependencies,
15% safe scarcity, 15% spatial unpredictability, 10% cross-face structure and
5% visual complexity. All weights are editable and normalized. Raw grid size/path
count never adds score directly. Edge mistakes are normalized as
`2 * failedAttempts / (pathCount * (pathCount - 1))`; the resistance component scales
and clamps this rate. Depth is normalized against `log2(pathCount + 1)`. Visual
features use fractions/ratios, with a deliberately small weight. Final scores and
selection objectives have defined six-decimal precision for cross-runtime stability.
The JSON retains every component and the weights used.

These are configurable engineering heuristics. Actual heuristic mistakes and
matched benchmark results, rather than score increases alone, establish the observed
improvement. Repeated blind attempts after unrelated removals are part of these
specific solver strategies; they are not a model of every human player's behavior.

## Generation and optimization

1. Reverse-construct candidates using the shared surface topology and snake simulator.
2. Compare a bounded pool of candidate paths. Prefer useful new blockers, multiple
   blockers, cross-face dependencies, interior-to-edge blocking and suitable leading
   route lengths. New gates are evaluated for their effect on older routes as well.
3. Complete the verified tail-fill pass and check every mandatory quality bound.
4. Analyze several complete, valid candidates and select the closest match to the
   requested score and logical targets.
5. Try bounded orientation reversals, local tail reroutes and gate relocations. Each
   mutation is built separately, revalidated, and reanalyzed; only an improvement is
   retained. Failed mutations never modify the accepted puzzle.
6. Replay the final stored solution again before exposing the result to Unity.

Uniform random, interior-biased, edge-biased, mixed and difficulty-optimized placement
are configurable. Normal and above default to difficulty-optimized. The old forced
nearest-edge heading for large grids is confined to the compatible basic/legacy
construction path; advanced placement keeps varied headings.

Budgets are deterministic counts, not wall-clock deadlines: `MaxCandidates` complete
construction attempts; up to the recipe's `MaxRestarts` per candidate; at most
`min(cellCount * AttemptsPerCell + 2000, MaxConstructionAttempts)` path proposals per
restart; and at most `OptimizationIterations` validated mutation attempts afterward.
The proposal-pool size is charged against the same construction-attempt limit. Fast,
Balanced and High Quality supply editable budgets. Cancellation is checked during
construction, validation and analysis. Workers receive immutable recipe snapshots
and never read ScriptableObjects, cameras, materials or UI.

## Developer tools and API

Advanced settings include safe/blocked material colors, approximate dependency lines
between heads, and the first edge-solver selections. All are off by default. Lines
and selection markers use Unity Gizmos; enable Gizmos in the Game or Scene view.
The colors update against the current remaining grid, while the report describes
the original generated puzzle. The JSON export button writes the complete report
under `Application.persistentDataPath` and displays the path.

```csharp
var preset = DifficultyPresets.All[2]; // Hard
var recipe = preset.CreateRecipe(seed: 123, rules: ExitRules.Hybrid);
var puzzle = new PuzzleGenerator().Generate(recipe, progress, cancellationToken);
var report = puzzle.Difficulty;

// Independent Custom values: recipe construction takes a deep copy.
var settings = preset.CreateSettings();
settings.Placement = PlacementStrategy.Mixed;
settings.DesiredSafeFraction = 0.2;
var custom = new PuzzleRecipe(123, preset.CreateProfile(),
    ExitRules.GateOnly, maxRestarts: settings.MaxRestarts, difficulty: settings);
```

The low-level `PuzzleRecipe` overload without `difficulty` retains basic packing
without the expensive search/analyzer. The six original `DifficultyProfiles` remain
for compatibility and regression coverage; use `DifficultyPresets` for the new five
tiers. `DifficultyPresets.ForLevel` supplies suggested progression bands without
constraining Custom assets or gameplay retries.

The original advanced system uses **generation version 3**. Save all recipe fields, including effective
gate cap, path-count cap, difficulty options, weights, seed and budgets. Versions
**1 and 2** retain their original geometry and fingerprints when reconstructed with
their saved inputs. The original 1,008 fingerprints were rechecked with zero changes,
and Unity tests cover both legacy versions and a version-3 .NET golden fingerprint.

## Reproducing the benchmarks

The original generator was benchmarked before changing its construction. The original
results remain under `TestResults/AdvancedDifficulty/Baseline`. `BaselineFinal` repeats
those same version-2 inputs with the finalized analyzer; all 1,008 geometry fingerprints
match. Its scores can differ from the first analyzer revision without changing any
original puzzle.

```powershell
dotnet run --project Tools/GenerationProbe/GenerationProbe.csproj --configuration Release -- --difficulty-baseline 56 TestResults/AdvancedDifficulty/BaselineFinal
dotnet run --project Tools/GenerationProbe/GenerationProbe.csproj --configuration Release -- --difficulty-benchmark 70 TestResults/AdvancedDifficulty/FinalVerified Balanced
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\RunUnityTests.ps1 -TestPlatform All -OutputDirectory .\Temp\Adv -ArtifactDirectory .\TestResults\AdvancedDifficulty\UnityFinal
```

The paired benchmark covers **1,050 new puzzles**: 70 seeds × five presets × three
exit modes, each compared with an original version-2 puzzle using matching geometry
bounds. CSV data includes actual strategy failures, score distributions, path counts,
generation time, budgets, target attainment, accepted validation failures and fingerprints.
See [benchmark summary](../TestResults/AdvancedDifficulty/Summary.md) for measurements
and limitations. Early tuning directories are diagnostic runs, not extra benchmark
samples. CPU batch results are not a mobile rendering/performance guarantee; large
Custom grids need larger budgets and can make full heuristic analysis expensive.

The 80–100 Extreme range remains an aspirational search target. Current measured
results do not justify promising that every requested high score is achievable;
the UI reports the actual score and every target miss.
