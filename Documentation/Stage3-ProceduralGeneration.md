# Stage 3: verified procedural puzzles

Current generation is **version 5** with [self-clearance and opening/blocker controls](Opening-Moves-And-Blockers.md).
It includes version 4's [independent multi-blocker dependencies](MultiBlocker-Dependencies.md).
Versions 1–4 remain supported; the implementation notes below include historical defaults.

**Advanced difficulty is now available:** see [Advanced procedural difficulty](Advanced-Procedural-Difficulty.md)
for the five new presets, master/Custom sliders, measured strategy resistance,
background generation, budgets and reports. The original six profiles below remain
available for compatibility. New recipes use version 5; earlier versions are preserved.

Stage 3 implements seeded path construction, connected cross-face puzzles, three
independent exit configurations, difficulty profiles, solution validation, deterministic
restart inputs, a generation lab, and a 1,008-puzzle stress suite. It uses the existing
stage 2 simulator for every candidate and for final solution verification.

## Editor setup

1. Open Unity **6000.6.7f1** and let scripts import.
2. Select **Arrow Cube > Stage 3 > Create Generation Demo**. Use one lab per scene;
   remove or disable other movement/generation labs so their cameras and UI do not compete.
3. Enter **Play** and open the **Game** view. The lab starts with Normal, seed 1, Hybrid.
4. Enter a signed integer seed, choose a difficulty, and set the two exit checkboxes.
   **Generate / regenerate this seed** creates and validates the requested puzzle.
   **Next seed** creates a different one. Both exit switches default to enabled.
5. Activate paths using the scrollable list, or use **Play next verified move** /
   **Play complete verified solution** to inspect the generated removal sequence.
   These are development controls; path clicking and orbit gestures remain stage 4.
6. Use the six face buttons to inspect the whole cube. The paths remain black and
   gates cyan, using the stage 2 rendering and snake animation.
7. **Restart exact puzzle / cancel playback** restores the immutable generated layout,
   including its gates and rules. It does not generate a replacement or consume a seed.

The lab shows cell coverage, path count, cross-face path count, initially safe moves,
dependency count/depth, generation version, elapsed generation time, and a shortened
fingerprint. New generation replaces the current level only after validation succeeds.
A rejected configuration leaves the previous level playable. If the first generation
fails, the lab displays the error and waits for a corrected recipe; it does not load
the manual four-arrow test cube. Restart/regeneration
cancels previous movement transactions before loading another grid.

Exit checkboxes configure the **next generation**. They do not silently change the
rules of an existing verified puzzle. Edge-only puzzles contain no gates; gate-only
puzzles use only compatible gates and retain head wrapping; accepted hybrid puzzles
include actual edge and gate removals in their verified solution. All three retain
continuous cross-face bodies and the stage 2 collision rules.

## Editable difficulty profiles

Create an asset with **Assets > Create > Arrow Cube > Difficulty Profile**, edit its
fields in the Inspector, and assign it to **Custom Profile** on the generation lab.
The asset overrides the six built-in profile buttons. Remove the assignment to return
to the built-in profiles. Each generation takes an immutable snapshot; editing an
asset afterward cannot change the current puzzle or its recipe.

**Grid Size** supports 4 through 64 cells per face side. **Scale Gate Budget With
Grid Size** defaults to enabled: above 8x8, **Max Gates** is multiplied by
`gridSize * gridSize / 64`, rounded up. A budget of 48 becomes 675 at 30x30 and
3,072 at 64x64. Disable scaling to use Max Gates as an absolute cap. The effective
budget is saved in the immutable recipe; it is unused in edge-only puzzles. A small
fixed gate cap can prevent a large gate-only puzzle from meeting its coverage floor.

These are the built-in tuning values, not topology restrictions:

| Profile | Suggested levels | Grid per face | Coverage floor / target | Path lengths | Minimum cross-face paths | Minimum initially blocked | Minimum average bends |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Tutorial | 1–5 | 6×6 | 52% / 68% | 3–10 | 6 | 10% | 0.6 |
| Easy | 6–15 | 8×8 | 60% / 75% | 3–12 | 8 | 20% | 1.0 |
| Medium | 16–30 | 10×10 | 66% / 81% | 4–14 | 10 | 30% | 1.4 |
| Hard | 31–60 | 12×12 | 72% / 86% | 2–16 | 12 | 38% | 1.8 |
| Very Hard | 61–100 | 16×16 | 76% / 90% | 2–18 | 16 | 44% | 2.1 |
| Expert | 101+ | 20×20 | 80% / 93% | 2–20 | 20 | 50% | 2.4 |

High-density profiles allow short filler paths alongside longer bent paths. Difficulty
does not require every individual path to be longer. Profiles also configure minimum
path count, turn probability, and a gate budget. Dependency count, dependency depth,
actual safe-move count, and average length are measured for tuning.

The coverage **floor is mandatory**. The target is a packing objective, not a promise:
bounded construction can stop below target if its accepted quality bounds still pass.
This is explicit in `GenerationReport.TargetCoverageReached` and the CSV. Expert's
93% target is not consistently reached, particularly in edge-only and gate-only modes.
Custom generation supports grid sides 4–64; the underlying topology supports a wider
range. Invalid or infeasible configurations fail explicitly rather than relaxing bounds.

## Construction and solvability

`ArrowCube.Generation` has no Unity engine references. `PuzzleGenerator`,
`PuzzleValidator`, `PuzzleRecipe`, immutable profiles, statistics, and logging can run
outside Unity; the optional .NET probe compiles these same source files.

### Reverse construction

The generator starts with one empty six-face `SurfaceGrid` and adds paths in reverse
removal order:

1. Select a free head cell and a face-local heading. Early choices force the neck
   over a cube seam. A six-face component graph prioritizes seams that connect
   previously separate components, rather than merely hoping random paths cross edges.
2. Trace the head's prospective surface route using the level's exit rules. It must
   reach an ordinary edge or a compatible gate through cells free of existing paths.
   Where gates are enabled, a bounded number of additional directional gates can be
   proposed ahead of the head. Gates are not automatically placed at every arrowhead.
3. Grow the body backward, taking seeded turns, varying length, and traversing the
   same cube topology. Avoid existing occupancy, repeated body cells, and the reserved
   leading corridor. Folded straight seams are not counted as body bends.
4. Add the candidate temporarily to the central occupancy grid, then call the actual
   `PathMovementSimulator` with that same exit map and rule configuration. Reject a
   failed prediction; retain only completely removable candidates.
5. Repeat toward the coverage target, subject to deterministic attempt/stall budgets.

A newly retained path can block older paths, creating dependencies. It is itself
removable while all those older paths remain. Removing paths in reverse construction
order therefore clears those dependencies. Added gates can shorten older leading
routes by offering an earlier departure; they cannot introduce additional surface
cells into those routes. Existing gate direction and edge/gate tie rules still apply.

### Filling gaps without breaking the solution

After placement, a tail-extension pass visits paths from newest to oldest. It protects
the future leading cells of every path that will be removed earlier. A tail can grow
into a free neighboring cell only if it does not enter those protected cells or its
own leading corridor, and only up to the profile's maximum length. This improves
coverage without turning a previously clear newer route into a blocked one. Extensions
may block still older paths, which is consistent with reverse removal order.

The proof guides construction; it does not replace validation. Every finished layout
replays the complete proposed solution through the gameplay simulator and transactional
occupancy updates. It is then checked against the profile's quality bounds. Unsolvable
or insufficient-quality layouts are discarded and deterministically retried. Exhausting
the configured retry budget throws `PuzzleGenerationException`; there is no empty,
unsolved, lower-density, or gate-only fallback substituted for the requested level.

Accepted puzzles have all six faces connected by cross-face **path geometry**, at least
the configured number of cross-face paths, and at least one safe opening move. They
are not six independent face generators assembled into a cube.

## Definition, validation, and solution data

`PuzzleDefinition` is the shared immutable layout used by both `ManualPuzzle` and
`GeneratedPuzzle`. It contains the topology, ordered paths, exit map, rules, and name.
The original stage 2 manual layouts and their gate-only/edge-only/hybrid choices remain
available and are regression-tested. Rendering still consumes the same definition
and movement plans; there is no second movement implementation for generated puzzles.

`GeneratedPuzzle` additionally contains:

- `Recipe`: seed, generation version, full immutable profile, both exit flags, retry
  budget, and candidate-attempt budget.
- `VerifiedSolution`: the exact path IDs replayed successfully, in removal order.
- `Statistics`: coverage, lengths, bends, cross-face transitions/connectivity, initial
  safe/blocked paths, full blocker dependencies/depth, and actual solution exit types.
- `Report`: elapsed time, attempts, rejected candidates/layouts, restarts, validation
  failures, and whether the soft coverage target was reached.
- `Fingerprint`: SHA-256 over canonical recipe values, gates, geometry, and solution.
  Timing is intentionally excluded.

`PuzzleValidator.Validate(definition, proposedSolution)` checks the supplied complete
sequence and rejects blocked moves, unknown/duplicate IDs, incomplete sequences, empty
layouts, invalid occupancy, and unsolvable puzzles. Without a proposed solution it
independently finds a solution by repeatedly removing safe paths in canonical path
order. Removing a safe path cannot add a blocker, so this monotonic search does not
need combinatorial backtracking for the current movement rules.

Dependency measurements use each path's unobstructed simulator route to collect all
other owners on that route, rather than reporting only the first current collision.
The initial safe count uses the full actual puzzle. All validation creates its own
grid and leaves the immutable definition untouched.

```csharp
var recipe = new PuzzleRecipe(
    seed: 12345,
    profile: DifficultyProfiles.ForLevel(25),
    rules: ExitRules.Hybrid);
var puzzle = new PuzzleGenerator().Generate(recipe);
var verification = new PuzzleValidator().Validate(puzzle, puzzle.VerifiedSolution);
var grid = puzzle.CreateGrid();
```

`ExitRules.EdgeOnly` and `ExitRules.GateOnly` select the other two level types. Both
disabled is rejected by generation. The APIs accept cancellation tokens and check
them during construction, tail filling, and validation.

## Determinism and later saving

New recipes use generation version **2**. Its packing rejection budget scales with
surface area, and edge-only grids above 20 favor interior construction before the
perimeter so early boundary paths do not cut off the remaining free cells. Version
**1** remains available through `generationVersion: 1` for saved recipes, with its
original construction and fingerprints preserved.

Both versions use a specified PCG integer PRNG. They never use Unity's
global random state, runtime-dependent `System.Random`, dictionary enumeration order,
frame time, or wall-clock deadlines to decide a layout. Attempts are bounded by
counts. The same recipe therefore reproduces the same geometry, gates, solution,
and fingerprint. A golden test compares Unity's result with the independent .NET
probe's result.

Persist **all recipe fields**, not just seed and level number. Profile tuning, exit
flags, version, and search budgets can change a result. Tests reconstruct a fresh
recipe from these values and verify identical output. Unsupported generation versions
are rejected explicitly; future algorithm changes must bump the version and preserve
an older implementation or migrate/save the actual older layout. Local progress/save
files and their serialization remain stage 5 work.

Restart in the lab restores the same immutable definition. Regenerating with its
complete unchanged recipe independently reproduces it. Selecting another profile,
exit mode, or seed intentionally creates another recipe.

## Tests and reports

Run EditMode and PlayMode in **Window > General > Test Runner**, or run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\RunStage3Tests.ps1
```

The wrapper creates an isolated temporary Unity project and runs the full suites,
including earlier stages. It exports XML results, Editor logs, the 1,008-row stress
CSV, and an error log to **TestResults/Stage3**. The stress test generates 56 seeds
for each of six profiles and three exit configurations, then replays every stored
solution in a fresh grid. It does not count rejected candidates as tested puzzles.

The CSV records generation time, paths, coverage, cross-face paths, immediately safe
paths, blocked fraction, average turns, dependencies/depth, edge/gate removals, gate
count, restarts, attempts, target attainment, validation failures, seed/version, and
fingerprint. The error log records any failed recipe. Timing is measured in the test
environment and is not a mobile performance guarantee.

An optional CLI probe requires the locally installed .NET 10 SDK:

```powershell
dotnet run --project Tools/GenerationProbe/GenerationProbe.csproj --configuration Release -- 56 TestResults/probe.csv
```

This also generates/replays 1,008 puzzles from the same engine-independent source.
Use count `1` or `3` for a smaller diagnostic run. The CLI does not replace Unity
compilation or PlayMode verification.

To exercise default custom profiles across larger grids, supply sizes as the third
argument (the probe uses the same area-scaled gate budget as the default asset):

```powershell
dotnet run --project Tools/GenerationProbe/GenerationProbe.csproj --configuration Release -- 20 TestResults/large-grids.csv 29,30,32,48,64
```

### Verified results

Verified on **2026-11-15**, Unity **6000.6.7f1**:

- **147/147 EditMode tests passed**, including the 1,008-puzzle stress test and prior-stage regressions.
- **7/7 PlayMode tests passed**, including complete generated-level playback in all three exit modes.
- **1,008/1,008 puzzles generated and replayed**, with zero failed recipes or solution validation failures.
- **54/54 sampled fingerprints matched** between the independent .NET probe and Unity.

See [verification summary](../TestResults/Stage3/Summary.md) and the
[full stress CSV](../TestResults/Stage3/stage3-generation-1008.csv). The same directory
contains XML results and Editor logs. The error log is empty. The CSV has one row per
accepted generated puzzle; retries and rejected candidates are not counted as extra puzzles.

In this run, Expert puzzles averaged roughly **175–255 paths**, depending on exit
mode. Expert mean coverage was about **82–90%**, rather than the 93% soft target.
Mean generation times ranged from about **2 ms** for Tutorial to **20–30 ms** for
Expert on this machine. These are CPU batch measurements, not a rendering or mobile
performance benchmark. Full per-profile/mode measurements are in the summary and CSV.

## Remaining limitations

Stage 4 now adds direct desktop/touch selection, orbit/zoom, camera reset, and a
responsive control lab. See [Stage 4 setup](Stage4-CameraAndControls.md). The original
stage 3 generation lab remains available for inspecting recipes and verified solutions.

This is a verified procedural foundation and development lab. Density targets,
gate budgets, opening safe-move counts, and dependency depth still need human
difficulty/playtesting. No claim is made that difficulty increases identically for
every seed or mode. Accepted quality floors are enforced; soft targets can be missed.
Generation is synchronous in the lab. The pure C# API supports cancellation and can
later run on a worker thread without Unity objects.

Runtime paths now use surface-aligned ribbon meshes and filled heads (see the stage 4
rendering update), still as individual objects. Production batching/pooling,
rendering performance on mobile, power-ups,
local saving/progression, final UI/audio, and platform builds remain later stages.
The lab's solution playback is a development control, not the final hint system.
