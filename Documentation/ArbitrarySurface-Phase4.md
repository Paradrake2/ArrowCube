# Phase 4: shape-independent puzzle generation

## Running the delivered lab

Open **Arrow Cube > Phase 4 > Open Procedural Surface Lab**, or `Assets/Scenes/SurfaceGeneration.unity`, and press Play. The scene starts with a generated L-shaped puzzle, seed **12345**. Click **SURFACE LAB / Settings** to expand the scrollable recipe editor.

- **Shape** cycles between L, ObliquePrism and T. The same generator handles each.
- Enter a signed integer seed; use **Generate this seed** or **Next seed**.
- Choose edge-only, gate-only or hybrid exits independently of the shape.
- Set cells per authored quad side, target coverage, maximum arrow length, maximum starting free arrows, and blocker amount.
- Optionally enable measured difficulty search and set its score, candidate count and restart budget.
- **Cancel generation** keeps the previous puzzle. A replacement is installed only after complete validation.
- Collapse Settings to inspect/play. The normal orbit, zoom, picking, hint, undo, hammer, blocked-contact animation and removal animation remain available. The camera frames the actual geometry bounds.

The inspector exposes the same defaults. Settings apply to the next generation. The original cube `Game` and settings `TestScene`, and the Phase 3 hand-authored `SurfaceL`/`SurfacePrism` scenes, remain available. The new lab has a separate save file, `arrow-cube-procedural-surfaces.json` plus its sandbox save, so campaign progress stays separate.

The example seed is intentional: all three shapes have multiple independent blockers, cross-region paths and complete verified solutions. The L and T include arrows traversing concave folds. The prism uses the existing rotated, skewed, non-right-angle geometry. T puzzles distribute arrows across the stem and both arms; dependencies follow actual removal trajectories across these regions.

## One construction pipeline

`PuzzleGenerator` now builds against `PuzzleRecipe.Surface : ISurfaceTopology`. Candidate bodies, free-cell sampling, corridors, trajectory intersections, gates, occupancy and dependency proposals use integer cell IDs. The generator no longer uses cube coordinates as its canonical working data.

`GenerationSurface` builds a stable sorted ID array and a dense sampling-index lookup. Sparse authored IDs are supported. Its regions are connected components of **non-geometric** boundaries. Thus neighboring coplanar tiles belong to one region, while a physical fold separates regions. The topology author must mark real folds with `IsGeometricEdge` and leave flat internal seams unmarked. No mesh interpretation is performed here.

The same restart, proposal-pool, body-growth, candidate selection, tail-fill, verification, tuning and difficulty-search code serves cubes and non-cubes. Small cube compatibility helpers retain historical face-edge sampling and edge-distance ranking. There is no shape-specific puzzle generator or second movement simulator.

### Candidate construction

1. Select a free head and heading, a boundary crossing for required region coverage, or a contact targeted at an existing arrow/trajectory.
2. Follow directed topology steps from the head until an enabled eligible edge or directional gate is reached. The state includes both cell ID and transported heading. A bounded visited-state check prevents endless routes. A proposed new gate is puzzle-owned and respects the gate budget.
3. Build the body behind the head through free neighbors, respecting minimum/maximum length, turn probability, transported directions, unique occupied cells, and the future leading corridor.
4. Construct explicit body direction slots. This also supports small logical surfaces with parallel cell connections without ambiguous cell-only inference.
5. Use the **gameplay movement simulator** for solo clearance, occupancy and eventual exit verification. `InitialExitClearance` rejects arrows pointing into their own starting bodies, even if tail motion could eventually clear the cell.

Ordinary exits require **both** the level rule and `AllowsEdgeExit`. `IsGeometricEdge` is used for geometry/regions, not as exit authorization. Concave boundaries remain traversable. Directional gates are independent, and an eligible ordinary exit retains priority at a tie. A gate-only or hybrid recipe works on a closed topology with no eligible edges; edge-only fails explicitly on such a topology.

### Dependency-order construction remains the foundation

New shape recipes always use the existing **dependency-order** proposal algorithm. An inserted arrow may be blocked by previously inserted arrows. This is not a replacement with random independent placement or reverse-only insertion.

For each proposal, the generator caches each older arrow's solo leading trajectory and current owners. It determines:

- incoming dependencies: existing bodies intersecting the candidate's unobstructed route;
- outgoing dependencies: older routes intersected by the candidate's body;
- existing links removed when a proposed gate shortens a route;
- the resulting free-arrow count;
- whether an incoming dependency can reach an older arrow newly blocked by the candidate.

That last reachability check rejects every new dependency cycle before acceptance. Proposals favor fewer opening moves and useful chains. Independent blocker amount is then tuned using the existing bounded orientation/endpoint adjustments, with shared simulation and full validation. All final puzzles have a replayed removal solution and an independently checked acyclic dependency graph.

Targeted mutation, multi-blocker targeting, subset verification, independent-blocker analysis and transactional measured-search mutations also use cell IDs and cached generic trajectories. New-version targeting skips arrows whose trajectories have no empty room for another body. A targeted attempt can legitimately produce no mutation; an accepted puzzle is never replaced by an invalid one.

## Regions, geometry and difficulty

The historical cube meanings remain unchanged: six face regions and the original all-six-face connectivity requirement, cross-face counts, face masks and formulas.

For authored surfaces:

- `MinCrossFacePaths` is also exposed as `MinCrossRegionPaths`; it is a minimum crossing-path requirement, not a demand to connect six faces.
- Overall coverage uses actual occupied cells divided by actual topology cell count.
- `RegionCount`, `OccupiedRegions`, `RegionOccupiedCells` and `ConnectedRegions` describe distribution and the largest region component connected by arrow bodies.
- `GeometricFolds` counts physical fold crossings; flat logical seams do not add a bend.
- Difficulty analysis exposes per-region coverage and coverage deviation, occupied-region count and fold count separately from dependency depth/count/independence/branching and free-move metrics.
- Cross-region dependency evidence uses actual contact cell IDs, contact regions, and the blocker's regions. Generic evidence never relies on a six-bit face mask.
- Spatial ranking uses graph distance from eligible boundaries (or fold boundaries as a placement proxy when no eligible edges exist), and directional projected exit distance follows actual topology mappings and enabled gates.
- Safe/blocked distributions and entropy use the actual region count. Dependency difficulty is measured from the generated blocking structure; adding regions does not directly increase dependency depth or blocker count.

Legacy public names such as `SafeByFace`, `BlockedByFace`, `CrossFaceGeometryFraction` and `DependencyFaceMatrix` remain compatible. On generic surfaces they describe regions. `DependencyNode.Regions` and `DependencyInterference.CellIds/Regions` are the authoritative generic evidence; cube coordinate fields/masks are legacy projections only. For more than 256 regions the optional dense matrix is empty; explicit dependency links, node region lists and contact evidence remain available, avoiding quadratic memory. Region connectivity itself uses sparse adjacency.

Measured score and average-blocker targets are **soft search goals**. Coverage floor, path bounds, solvability, solo clearance and configured maximum opening arrows are hard acceptance requirements. Failure to hit a measured score/blocker target within the budget is reported, rather than silently claiming success.

## Reproducible shapes, recipes and saves

`SurfaceShapeDefinition` copies and validates a connected, closed, four-boundary-per-cell topology into an immutable snapshot. Identity includes a stable authored ID, positive version, sorted cell IDs and every boundary's neighbor, heading, geometric-fold flag and exit flag. Its SHA-256 digest distinguishes shapes even if IDs and cell counts overlap.

Use the shape overload:

```csharp
var authored = AuthoredGenerationSurfaces.Create(AuthoredGenerationShape.L, 4);
var profile = AuthoredGenerationSurfaces.Profile(authored.Shape.Topology.CellCount);
var recipe = new PuzzleRecipe(12345, authored.Shape, profile, ExitRules.GateOnly,
    dependencyControls: new DependencyControlSettings {
        MaximumStartingFreeArrows = 3, Amount = .5
    });
var puzzle = new PuzzleGenerator().Generate(recipe);
game.LoadPuzzle(puzzle, geometry: authored.Geometry);
```

Shape recipes use **generation version 6**. Default historical cube recipes still use **version 5**, and versions 1?5 retain their byte-level fingerprint layout, random call ordering and compatibility rules. `CurrentVersion` is the highest supported version; `LegacyCubeVersion` is the historical cube default.

`DifficultyProfile.SurfaceCellCount` supplies generic path-capacity validation; its `GridSize` field remains legacy profile/serialization data and does not define the authored topology. `MinCrossRegionPaths` can be zero for a single-region logical surface. Shape construction validates its own actual cell count and connectedness. Generation budgets scale with that actual count, plus a typical region scale, rather than assuming six square grids.

`RecipeBinary` keeps **format 1 unchanged** for old cube recipes. Explicit **format 2** adds the generic profile cell capacity and immutable shape snapshot. Decode preserves exact IEEE double bits. A new generated fingerprint includes the shape digest, generic gates, path cell IDs and explicit body directions. Gate order, path order and verified removal order are deterministic.

`PuzzleDefinition`/`PuzzleSave` retain optional `SurfaceAssetId` and `SurfaceAssetVersion` so restored puzzles can recover registered physical geometry. `ActiveLevelSave` checks the recipe's full topology digest against the saved puzzle. The topology snapshot makes regeneration independent of an installed authoring registry; physical rendering still requires the matching geometry asset. `AuthoredGenerationSurfaces.Resolve` validates the snapshot against its registered asset before binding geometry to restored cell IDs.

Trajectory caches remain scoped by immutable topology/path/map identities and rules. Equal cell counts and overlapping IDs cannot cause cross-shape reuse. No dictionary iteration is used to choose random sampling indices or tie-breaking order.

## Authored geometry fixtures

The L and prism reuse Phase 3's authored geometry and connectivity. The T adds ten explicitly connected side quads and four rectangular tiles on each cap (18 parent quads). All logical cells remain quadrilaterals, including the prism's subdivided caps.

The lab refines each existing parent quad into an n-by-n cell table. Interior connections are regular; connections at a parent's border come directly from the parent's explicit boundary slot, reversed edge order and transported heading. Quads use bilinear sampling of the already authored parent. This is deterministic **refinement of existing authored surfaces**, not procedural shape creation, geometric adjacency inference or mesh-to-topology conversion.

At n=4 the L has 224 cells, prism 192 and T 288. Their differently sized regions, skewed prism cells, convex/concave folds, and non-cube cell IDs all use the same production generator. To add an authored shape, provide validated topology plus Phase 3 `ISurfaceGeometry`, a stable ID/version, and a geometry resolver for saves.

## Files

Core generation changes:

- Added `Generation/GenerationSurface.cs`, `Generation/GenerationPaths.cs`, `Generation/SurfaceShapeDefinition.cs`.
- Updated `PuzzleGenerator`, `PuzzleRecipe`, `GeneratedPuzzle`, `PuzzleValidator`, `PuzzleDependencyGraph`, `PuzzleDifficultyAnalyzer`, `HeuristicPuzzleSolver`, `DifficultyAwareGeneration`, `DependencyControlTuner`, `DifficultyProfile`, `GenerationCsv`.

Gameplay, rendering integration and authoring:

- Added `Presentation/AuthoredGenerationSurfaces.cs`, `Game/SurfaceGenerationDemo.cs`, `Scenes/SurfaceGeneration.unity`, plus Unity metadata.
- Updated `Movement/PuzzleDefinition.cs`, `Gameplay/PuzzleSave.cs`, `Gameplay/RecipeBinary.cs`, `Game/GameplayController.cs`, `Presentation/AuthoredSurfaceDemos.cs`, `Presentation/Stage2MovementDemo.cs`.
- Updated `Editor/SurfaceGeometryDemoMenu.cs` and `ProjectSettings/EditorBuildSettings.asset`.

Validation:

- Added `Tests/EditMode/GenericSurfaceGenerationTests.cs` and `Tests/PlayMode/GenericSurfaceGenerationPlayTests.cs`.
- Results and deterministic comparisons: `TestResults/SurfaceGeneration/`.

## Validation and performance

Final run results are recorded in `TestResults/SurfaceGeneration/Summary.md`. The test coverage includes all three shapes and exit modes, independent multiple blockers, hard opening limits, solo routes, coverage, complete removal replay, measured search/targeted mutation/tuning, repeated fingerprints, shape recipe and active-save round-trips, changed identities, sparse IDs, flat logical surfaces, 324-region evidence, explicit cycles, no-edge-exit surfaces, cancellation, progress and impossible constraints.

The full historical 1,008-puzzle CSV comparison matches every deterministic field, excluding elapsed time. Historical version-specific fingerprint and save tests are included in the full Unity run. PlayMode loads the delivered scene, exercises runtime controls and cooperative generation, rotates/focuses/picks, animates a blocked attempt, removes every arrow, reaches victory, and restores a generated sandbox. A separate Mobile URP run renders front/rear captures of all three procedural puzzles.

Representative editor construction timings, seed 12345, gate-only, 65% target coverage, without measured search or tuning:

| Shape | n=2 cells / ms | n=4 cells / ms | n=6 cells / ms |
|---|---:|---:|---:|
| L | 56 / 2.6 | 224 / 12.6 | 504 / 40.9 |
| Oblique prism | 48 / 4.1 | 192 / 9.1 | 432 / 30.7 |
| T | 72 / 3.0 | 288 / 15.1 | 648 / 51.3 |

These are observations from this machine, not frame-time guarantees. JIT, restarts, hard opening limits, optional tuning and measured searches can add substantial work. At n=4, the nine controlled examples took roughly 50?213 ms, produced 12?25 arrows and 1?3 free openings, with verified depth 6?14. Cooperative generation retains the existing yield boundaries and native-worker/WebGL-main-thread distinction; its millisecond tick budget is a work-slice target, not preemption inside a simulator call.

## Remaining cube assumptions and Phase 5

Cube-specific code remains intentionally in the historical recipe/fingerprint adapters, legacy face-mask/coordinate projections, cube placement policy, old campaign presets and cube scene authoring. Generic generation does not require their dimensions or faces. Version-6 authored generation requires a closed connected quad surface with valid inverse boundary mappings; open surfaces and zero-thickness antiparallel folds remain outside the Phase 1?3 movement/geometry contract.

For Phase 5, mesh conversion should produce this same validated immutable topology/geometry pair. It should canonicalize stable IDs and boundary headings, merge genuinely coplanar regions, explicitly classify convex/concave folds and eligible exits, reject non-manifold/open/inconsistent connections, and version the resulting asset identity. Preserve full-edge quad adjacency; resolve subdivisions/T-junctions during conversion rather than introducing special rules into movement or generation. Include geometry/import settings in the future asset version/digest contract and test regenerated fingerprints before changing conversion rules.
