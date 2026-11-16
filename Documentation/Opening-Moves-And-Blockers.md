# Opening moves and blocker controls (generation version 5)

Newly generated puzzles reject arrows whose forward exit route crosses their own
starting body, including U shapes whose tail would technically move out of the way.
This check applies with dependency controls off or on, with measured difficulty
search off or on, and in all three exit modes. The gameplay simulator retains its
existing tail-vacating rules for compatibility with saved puzzles.

## Settings

In a **Difficulty Profile** asset, use **Blockers and starting moves (works without
measured search)**. In the Stage 4 panel, expand **Show / hide advanced settings**.

1. Enable **Control Dependencies** in the Inspector, or **Control blockers / few
   starting moves** in the runtime panel.
2. Adjust **Dependency Amount** from 0 to 1. Higher values seek more distinct arrows
   blocking each blocked arrow. The soft average target is `1 + 4 * amount`:
   0 targets 1, 0.5 targets 3, and 1 targets 5 blockers per blocked arrow.
3. Set **Maximum Starting Free Arrows**. The default is **3**; **1** permits a single
   opening move. This is a hard maximum, not a desired fraction. Every accepted
   puzzle has at least one safe opening move.
4. Regenerate the puzzle to apply the settings. They work with **Use measured
   difficulty search** disabled. The current puzzle changes only after successful
   generation and validation.

The controls are opt-in so existing custom profiles keep their previous packing
preferences. A preset/master reset replaces these settings with their defaults.
The cap applies at the start: removing arrows will naturally reveal additional
safe moves. It does not limit player attempts or impose artificial locks.

The amount controls an average, not a minimum for every arrow. At least one arrow
must be free, and arrows near the end of a solution necessarily have fewer remaining
blockers. Count each distinct blocking arrow once even if its body intersects the
route several times. These are direct physical blockers; several may depend on
one another. The measured graph separately reports independent blocker branches.

Geometry, coverage, path lengths, gate budget, connectivity and bounded search can
prevent the requested blocker average. The runtime statistics display actual
**Free at start** and **Blockers** values. `DependencyControlReport` also contains
the target and `BlockerTargetReached`. A puzzle that misses the hard opening cap
is rejected explicitly; the previous playable puzzle remains loaded. Restrictive
custom bounds may require a larger opening cap or more generation restarts.

## Construction

Version 5 uses two bounded construction strategies when these controls are enabled.
Reverse construction places removable arrows that block older routes, favoring
placements that reduce the free-arrow count. Dependency construction also permits
new arrows to be blocked by existing bodies. It updates physical blocker links
and rejects any addition that creates a cycle or exceeds the opening cap. This lets
the puzzle expand over larger faces while keeping few opening moves. Restarts
alternate strategies, starting with dependency construction above an 8x8 grid.

A final bounded pass can reverse an arrow or move one endpoint to gain blockers.
It rejects self-pointing routes and dependency cycles. Every accepted layout is
validated and its complete solution replayed with the gameplay simulator; all
mandatory profile bounds remain in force. When measured search is enabled, later
mutations must preserve the opening cap and self-clearance, and candidate scoring
also considers the independent amount setting.

`PuzzleRecipe` snapshots `DependencyControlSettings`. Save these settings along
with the profile, seed, exit rules, budgets and generation version to reproduce a
puzzle. Versions 1 through 4 remain supported with their previous fingerprints;
version 5 is the default for new recipes.

## Verification

`DependencyControlTests` covers the self-pointing tail-vacating case, actual
self-collision, all exit modes, measured search on/off, deterministic snapshots,
different opening caps, blocker-density changes, invalid settings and cancellation.
`GenerationDemoTests` exercises the runtime controls with measured search off and
verifies that generation failure preserves the existing level.

Validation passed 246 EditMode and 27 PlayMode tests, including the 1,008-puzzle
stress suite and 16x16/30x30 opening-cap regressions. After bounding the automatic
endpoint fallback, four density/cap tests and the runtime-controls test passed again.

The final standalone sample passed 180 layouts at 8x8 with a three-arrow cap,
27 layouts with a single opening, and 18 layouts at 30x30 with a three-arrow cap,
across all exit modes. In the 8x8 sample, raising Amount from 0 to 0.75 increased
the average distinct blockers per blocked arrow from **1.63 to 2.27**. Amount 1
reached the same average there: increasing a soft target can plateau. Larger layouts
can achieve more blockers, but requested averages are still not guaranteed.

Results are in `TestResults/OpeningControls/UnityFinal`, `FinalBounded`,
`finalControls8.csv`, `finalCap1.csv` and `final30.csv`.

The standalone probe compiles the same generation code without Unity:

```powershell
dotnet run --project Tools/GenerationProbe --configuration Release -- --dependency-controls 20 TestResults/OpeningControls/probe.csv 8 3
```

Arguments are seed count, CSV path, grid size, opening cap, and optional `measured`.
It tests all three exit modes and amounts 0, 0.75 and 1, validates strict
self-clearance, replays every solution, and records actual blocker counts.
