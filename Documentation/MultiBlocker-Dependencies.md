# Multi-blocker dependency generation (version 4)

New recipes now use version 5. For controls that also work with measured search
disabled, see [Opening moves and blocker controls](Opening-Moves-And-Blockers.md).

Version 4 adds explicit full-body removal trajectories, simulator-confirmed blocker
graphs, independent-branch analysis, dependency-aware proposals and targeted blocker
replacement. It keeps the existing movement, collision, exit and input systems.
There are no logical locks, hearts or player-attempt limits.

## Using it

Select a difficulty profile asset or open the Stage 4 controls and expand Advanced.
The new group provides:

- Enable multi-blocker generation; enable targeted corridor placement.
- Target average confirmed blockers **per blocked path** and maximum target blockers.
- Desired fraction with two **independent** blockers.
- Cross-face dependency and branching preferences, and maximum desired blocker reach.
- Relative blocker-count distribution weights: 0, 1, 2, ...; the final bucket includes
  larger counts. The Inspector permits 2–17 buckets; the runtime panel exposes 0–6+.
- A separate targeted-attempt budget and a replacement/insertion switch.

These are soft structural targets. Coverage, path bounds, six-face connectivity and
solvability remain mandatory. Reports show actual counts, requested averages/fractions,
and a warning when blocker targets are not reached. `TargetReached` continues to refer
to the overall score; `MultiBlockerTargetsReached` separately checks the independent
fraction within 10 percentage points and the blocked-path average within 0.5 below target.
Other preferences/distribution weights are optimization objectives, not hard guarantees.

Preset initial targets are Easy 0–1 blockers / 0–10% multi-blocker paths, Normal 1–2 /
15–30%, Hard 2–3 / 35–55%, Expert 3–4 / 55–75%, Extreme 4–6 / 70–90%. The average is
conditional on being blocked, and the multi-blocker target uses independent branches.
These initial values are ambitious; see the benchmark for actual achieved distributions.
Easy disables the extra construction by default. Disabling the feature preserves
incidental physical dependencies and other difficulty settings; it does not prohibit
arrows from naturally having several blockers.

Master/preset selection explicitly replaces the configuration. Individual edits
remain independent. Existing edge-only, gate-only and hybrid switches are unchanged.
Fast mode uses one layout candidate and no final targeted replacements. Balanced
uses more candidates for harder tiers (8 Hard, 16 Expert, 24 Extreme); High Quality
uses 16/24/32 respectively. Every budget remains individually editable and cancellable.

## Complete removal trajectories

`Movement/RemovalTrajectory.cs` calls the unchanged `PathMovementSimulator` with only
the target arrow present, preserving snake motion, self-collision, seam directions,
gate direction checks, off-surface positions and the edge/gate tie policy.

The result includes every initially occupied cell and every cell/segment occupied
by any part of the moving body. It uses the simulator's chronological rail rather
than expanding every animation frame: rail position `k` belongs to the body's moving
window for steps `max(0, k-length+1)` through `min(finalStep, k)`. `Visits` records
these actual step ranges, `SweptCells`/`SweptSegments` expose the complete union, and
`GetBodyPosition(step, segment)` reproduces the exact simulation state.

Off-surface anchors are not treated as surface occupancy. The target's own valid
starting cells are excluded when looking for other owners. Failed solo trajectories
are never certified as removable. Tests compare the compressed sweep with explicit
occupancy of every segment at every simulated step in all three exit modes.

## Potential, confirmed and independent blockers

`PuzzleDependencyGraph.Analyze` performs these separate operations:

1. Compare the full sweep against the central grid's cell and segment owners.
2. Collect unique potential blocker IDs, with contact cells, faces and occupancy steps.
3. Simulate the target with each potential blocker in isolation. A mandatory label
   requires a valid solo exit and an actual foreign-path collision with that blocker.
4. Test every subset for groups of at most three blockers. For larger groups, test
   zero/full occupancy plus all singleton obstructions; record that exhaustive
   subset enumeration was **not** performed.
5. Check full-state removability against the verified blocker set and detect cycles
   through iterative topological processing.

With today's fixed head direction, static gates and deterministic exit priority,
singleton obstruction plus a clear solo route establishes that each confirmed path
must be removed before the target. This is explicitly labeled
`ConfirmedMandatoryFixedRoute`. Artificial isolated subset states are tests of
collision behavior, not a claim that every subset is reachable during gameplay.
If future gameplay permits route/exit choices, this classification must be revisited.

Distinct blockers are not necessarily independent. If B already requires A, a target
touching both has two direct blockers but only one independent branch. The graph
therefore finds an antichain of blockers: members do not depend on each other through
any prerequisite chain. Maximum width is exact up to 12 blockers. Larger sets use a
verified greedy antichain as a conservative lower bound and mark `IndependentWidthExact`
false. Nothing unverified is labeled an independent group or a mandatory dependency.

For two genuine independent blockers A and B, removing only A leaves the target
blocked by B; removing only B leaves it blocked by A; removing both clears the target
when no other blockers remain. Tests cover this explicitly, as well as three-, five-
and fourteen-blocker groups, redundant chains, multiple contacts with one body and cycles.

## Constructing better relationships

The existing reverse construction remains the basis of the solution proof. Each
new arrow must be removable while all older arrows remain; it may block their
future routes. Ordinary proposal pools additionally evaluate:

- Creating second/third blockers at distinct locations, not just first collisions.
- Whether a proposed blocker also blocks its supposed sibling, producing a chain.
- Whether new gates erase existing blocker relationships or expose older arrows.
- Distributed branching and interior blockers of edge paths, with a penalty for a
  single arrow controlling a large fraction of the puzzle.
- The desired safe-move fraction, blocker distribution and cross-face relationships.

These local estimates rank proposals; the final graph's simulations certify the
reported relationships. Some optional proposals choose free cells on a target's
future corridor. Ordinary placement strategies remain configurable; disabling targeted
placement returns to ordinary sampling while retaining dependency-aware ranking.

After whole-layout selection and local optimization, a bounded targeted pass chooses
an under-target arrow, samples free cells along its sweep, and tries a blocker there.
By default it **replaces a weak path**, keeping path count unchanged in that pass.
An optional mode inserts a path subject to the existing count cap. The new blocker
must be immediately removable in the retained construction state; its ID is prefixed
to the retained verified solution. It cannot occupy the target's starting body.

The pass protects prerequisites of an existing independent blocker group so the new
arrow can add an incomparable branch. Every proposed definition gets complete
solution replay, mandatory-quality checks and fresh dependency/difficulty analysis.
Only an improvement to the combined objective replaces the immutable accepted level.
Failed geometry, cycles, lower quality or worse objectives leave the previous candidate intact.

## Metrics and scoring

Reports retain the earlier metrics and add confirmed blockers per path / per blocked
path, maximum blockers, count histograms, fractions with at least two/three direct
blockers, independent two/three-blocker fractions, average independent blockers,
branching, independent openings, blocker reach/concentration and exactness flags.
The existing sampled-state unlock counts remain available.

Cross-face links use **actual contact faces and the blocker geometry/head**. A target
having an unrelated tail on another face does not alone make its dependency cross-face.
Contact evidence and node face masks are exported so the classification is inspectable.
The older head-face matrix is retained separately.

Analysis version 3 keeps the six original component weights. Dependency credit is
conservatively modified by verified independent groups, blocker-count diversity and
hub concentration; cross-face credit is discounted when confirmed geometry evidence
is weak. Raw blocker count cannot create a high score by itself. Candidate selection
also considers structural target shortfalls and actual edge-strategy resistance.
The comparison report evaluates real heuristic mistakes, not merely internal scores.

The rules remain monotonic: any sequence of safe removals solves a valid puzzle.
This system increases inspection/dependency complexity; it does not introduce a safe
move that can later create an irreversible dead end.

## Debugging and export

All debug options are off during normal play. In Advanced:

1. Enable **Debug: inspect arrow (click selects)**. A normal world click/tap now selects
   the arrow without moving it. Disable inspection to restore activation.
2. Alternatively use **Inspect next arrow**.
3. Read potential/confirmed blockers, an independent group, paths blocked, depth,
   remaining blocker count, involved faces and proof exactness.
4. The selected arrow is yellow; its confirmed blockers get distinct colors.
5. Enable Unity Gizmos to see the complete swept surface occupancy and dependency lines.

The graph/report describe the original definition; remaining-blocker counts and safe/
blocked colors use the current grid. JSON export includes nodes, contact evidence,
certainty, metrics and target outcomes. DOT export uses **blocker -> dependent** edges.
Example API:

```csharp
var recipe = DifficultyPresets.All[3].CreateRecipe(123, ExitRules.Hybrid);
var puzzle = new PuzzleGenerator().Generate(recipe, progress, cancellationToken);
var node = puzzle.DependencyGraph.GetNode(pathId);
var blockers = node.ConfirmedBlockers;
var independent = node.IndependentBlockers;
var dot = puzzle.DependencyGraph.ToDot();
```

## Performance, budgets and compatibility

Trajectory caches are bounded and scoped to one search/debug session. Keys contain
immutable path and exit-map identities, topology size and both exit flags. Changing
geometry, gates or rules produces a different key. Occupancy-dependent graph results
are recalculated after modifications; they are not reused as if the board were unchanged.
Proposal contexts cache routes and ownership within a fixed placement state. Analysis
uses its confirmed graph instead of redundantly simulating the same routes again.

`MaxConstructionAttempts` is charged for every ordinary proposal. Each whole candidate
has at most the recipe's restart budget. `OptimizationIterations` bounds local mutations.
The separate `TargetedBlockerAttempts` budget is charged once per final targeted trial,
with at most `CandidatePoolSize` path proposals per trial; these are recorded separately.
All loops remain bounded and check cancellation. Workers use pure C# data only.

New generation uses **version 4**; versions **1, 2 and 3** remain available. Version 3
keeps its original analysis version and fingerprint serialization. New multi-blocker
fields are included in version-4 recipes only. Persist the full snapshot, not just
the seed. `CreateLegacySettings` reproduces the prior five-preset defaults for comparison.
The basic constructor without difficulty settings remains a packing-only option.
Disabling multi-blocker construction keeps ordinary advanced generation available.

## Verification and benchmarks

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\RunUnityTests.ps1 -TestPlatform All -OutputDirectory .\Temp\MB -ArtifactDirectory .\TestResults\MultiBlocker\UnityFinal
dotnet run --project Tools/GenerationProbe/GenerationProbe.csproj --configuration Release -- --multi-blocker-benchmark 70 TestResults/MultiBlocker/FinalVerified Balanced
dotnet run --project Tools/GenerationProbe/GenerationProbe.csproj --configuration Release -- --multi-blocker-benchmark 20 TestResults/MultiBlocker/ControlVerified Balanced disabled
```

The first benchmark compares 1,050 new puzzles with the original version-3 presets,
using identical seed, geometry/coverage bounds and exit mode. New hard-tier defaults
include stronger logical targets and larger search budgets. The second comparison
holds those new settings/budgets fixed and toggles only multi-blocker generation.
This separates the full release improvement from the effect of the toggle alone.
The small core-grid suite uses 6x6 and 8x8; separate Unity cases verify 16x16 and
32x32 generation, complete replay and bounded Fast budgets. CPU batch measurements
are not mobile-device performance or human-difficulty validation.

See [the benchmark and regression summary](../TestResults/MultiBlocker/Summary.md)
for measured gains, tradeoffs, target misses and limitations. Earlier calibration
directories are diagnostic runs, not additional final samples.
