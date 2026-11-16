# Phase 2: shape-independent puzzle data and movement

The subsequent [Phase 3 geometry and playable-demo implementation](ArbitrarySurface-Phase3.md)
now provides the two physical authored shapes, shared rendering, interaction and
animations. This page documents the logical Phase 2 foundation.

The canonical puzzle data, occupancy, gates, movement plans and trajectories now use
`ISurfaceTopology` and integer cell IDs. One `PathMovementSimulator` handles every
surface. The existing cube generator and presentation consume compatibility views
over that same data and simulation.

## Data and compatibility APIs

| Data | Canonical API | Existing cube adapter |
| --- | --- | --- |
| Puzzle/occupancy topology | `Surface` (`ISurfaceTopology`) | `Topology` (`CubeTopology`) |
| Arrow cells | `CellIds`, `HeadCellId`, `TailCellId` | `Cells`, `Head`, `Tail` |
| Arrow connections | `CellSegments`, `BodyDirections` | `Segments` |
| Directional gates | `CellGate(cellId, direction)`, `ExitMap.CellGates` | `ExitGate(coordinate, direction)`, `ExitMap.Gates` |
| Movement rail | `CellRail`, `GetCellPosition` | `Rail`, `GetPosition` |
| Collision location | `BlockedCellId`, `AttemptedCellHead` | `BlockedCell`, `AttemptedHead` |
| Removal sweep | `SweptCellIds`, `CellVisits`, `SweptCellSegments` | `SweptCells`, `Visits`, `SweptSegments` |

Cube-only properties reject a non-cube surface explicitly rather than inventing
face coordinates. `CubeTopologyAdapter` centralizes conversions and compatibility:
equal-sized `CubeTopology` instances preserve the existing coordinate convention;
arbitrary surfaces require the same immutable topology instance. Sharing cell IDs
does not make two unrelated surfaces compatible.

Paths defensively copy cell IDs and connection directions. Gates and movement
rails are also published as read-only lists. Occupancy uses topology-local cell
IDs and `CellSegment` keys, with atomic adds/removals and revision-checked commits.
An undirected segment includes the boundary direction at each endpoint, so parallel
connections between the same two cells remain distinct. Segments are also scoped
to their topology.

For ordinary cell graphs, `ArrowPath` infers the body connections from consecutive
IDs. For a graph with parallel links, pass `headToTailDirections` explicitly, one
departure direction per body connection. Ambiguous cell-only authoring is rejected.
The first body connection must point opposite the arrow's head direction; each
remaining connection must reach the next declared cell.

## Shared simulation and trajectories

The existing snake algorithm now stores `CellPathPosition` samples. The head follows
boundary connections and transported local headings; every body segment follows
the head's chronological route. The tail vacates simultaneously, so moving into
its just-vacated cell remains legal. Hits on another path and hits on the moving
body produce failed read-only plans. No failed or stale plan can change occupancy.

Concave folds have `AllowsEdgeExit=false`; they remain traversable with ordinary
edge exits enabled. Convex folds are traversed when edge exits are disabled and
can qualify for departure when enabled. Directional gates remain puzzle-owned
cell-ID/heading pairs. An eligible ordinary edge still wins a tie with a gate.
`ExitType.CubeEdge` retains its existing enum name and value for compatibility;
it represents an ordinary geometric edge exit on any supported surface.

Loop detection tracks `(cellId, heading)`. A departure records its cell ID, exit
type, direction and distance until the entire body has left. No geometric position,
surface normal, mesh query or physics calculation is part of this simulation.

`RemovalTrajectory` exposes the generic full-body sweep and legacy cube projections.
Its cache includes topology identity, path identity, gate-map identity and rule flags.
`PuzzleValidator` uses the same simulator for both kinds of puzzle, including
dependency counts/depth and proposed or greedy removal solutions. Cube face metrics
remain cube-specific; `ConnectedFaces` is zero for a non-cube graph and is not an
estimate of its geometric patches. Generation difficulty algorithms were not changed.

## Authored non-cube demonstration

`ManualSurfacePuzzle.Create()` builds a fixed 16-cell closed graph with periodic
toroidal connectivity and sparse IDs. It is not a `CubeTopology`. Its local frames,
concave/convex boundary metadata, arrows and gates are explicitly authored. The
fixture is logical test data, with no physical embedding or renderable mesh claimed.

| Arrow | Head-to-tail cells | Initial result | Gate |
| --- | --- | --- | --- |
| 10 | 101, 143 | Blocked by 20 at cell 115 | 129, Left |
| 20 | 216, 115 | Blocked by 30 at cell 317 | 317, Up |
| 30 | 418, 317 | Removable | 418, Up |

Only one arrow is free initially. The verified removal order is **30, 20, 10**.
Arrow 10's head route is `101/Right -> 115/Up -> 129/Left -> gate departure`.
It demonstrates continued traversal through an inside fold and a convex fold when
ordinary edge exits are disabled. Additional tests solve the same puzzle in
edge-only and hybrid modes, and exercise self-collision, legal tail vacating,
no-exit loops, stale plans and foreign topology rejection.

Run the engine-independent demonstration with the .NET 10 SDK:

```powershell
dotnet run --project Tools/GenerationProbe/GenerationProbe.csproj -- --surface-demo
```

The report is written to `TestResults/SurfacePuzzle/demo.txt`. It shows initial
collisions, dependency depth 3, all three removals, route samples, and final zero
path/cell/segment occupancy. This command does not modify a Unity scene.

## Save compatibility

Missing or zero `PuzzleSave.SurfaceFormat` means the existing cube format.
`GridSize`, integer cell indices, headings, gates, recipe versions and the existing
player/checksum envelope keep their meanings. Old cube JSON is covered by a literal
legacy-file test and the existing recipe/session save suites.

Format 1 adds an explicit closed graph with cell IDs and four boundary records per
cell. Generic path snapshots also store body boundary directions, including parallel
connections. Restoration reconstructs the graph through `SurfaceTopologyBuilder`,
validates paths and gates, then verifies removability with the shared validator.
Invalid IDs, headings, reverse links, bounds, versions and unsolvable puzzles are
rejected. Generic session snapshots preserve partial removals, hints, mistakes,
hearts and undo history, and work with the existing checksummed save/backup store.
Procedural recipe snapshots still require a cube; a generic manual puzzle has no
procedural recipe. There is no non-cube geometry stored or inferred.

## Verification

The existing GenerationProbe captured 54 recipes before and after this phase:
six profiles, three exit modes and three seeds. Every fingerprint and deterministic
CSV field matches. Timings are excluded from equality checks. Existing recipe
goldens and the 1,008-puzzle cube suite are also run. Results and the final counts
are recorded in `TestResults/SurfacePuzzle/Summary.md` and `Unity/`.

New tests are `SurfacePuzzleTests` and `SurfacePuzzleSaveTests`. They cover generic
data immutability, occupancy, collisions, dependencies, local heading transport,
exit modes, body motion, self-collision, tail vacating, loop detection, parallel
connections, topology ownership, cache/cancellation, verified solutions, legacy
cube JSON, generic serialization, session undo, backups and malformed saves.

The final run passed **320 EditMode + 45 PlayMode tests**, with no failures or
skips. All **1,008** cube-regression fingerprints and deterministic CSV fields
match the existing `TestResults/Stage5/Final` dataset. One first-pass mismatch in
the exception returned for malformed generic save data was corrected before the
final full run.

## Source changes

- Topology: added `CellSegment.cs` and `CubeTopologyAdapter.cs`; generalized
  `ArrowPath.cs` and added direction lookup to `SurfaceTopology.cs`.
- Movement: added `CellMovementData.cs` and `ManualSurfacePuzzle.cs`; generalized
  `PuzzleDefinition.cs`, `SurfaceGrid.cs`, `ExitGate.cs`, `PathMovementSimulator.cs`
  and `RemovalTrajectory.cs`.
- Validation: adapted `PuzzleValidator.cs` and `InitialExitClearance.cs` to IDs
  without changing cube scoring formulas or procedural generation algorithms.
- Saves: extended `Gameplay/PuzzleSave.cs` with an optional explicit-graph format.
- Tests: added `SurfacePuzzleTests.cs` and `SurfacePuzzleSaveTests.cs`, with Unity
  metadata for all new source and test files.
- Tooling: added the demo command in `Tools/GenerationProbe/Program.cs` and made
  its hand-authored `.csproj` trackable through `.gitignore`.
- Documentation: this guide, the Phase 1 guide's migration note, and the result
  summary under `TestResults/SurfacePuzzle`.

## Following phases

- Phase 3 supplies generic surface geometry, rendering, camera/picking and seam
  animation. The existing game scenes continue rendering cubes through adapters.
- Phase 4 makes puzzle generation and generation-specific helpers shape independent.
  Cube generation and its deterministic fingerprints are preserved in this phase.
- Phase 5 converts procedural meshes into topology.

Mesh conversion, procedural L/T construction, non-cube rendering and open-surface
behavior are not implemented here. The hand-authored logical fixture is available
through its factory, validator, CLI and tests.
