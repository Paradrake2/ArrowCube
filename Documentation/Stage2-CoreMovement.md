# Stage 2: deterministic snake movement

Stage 2 adds full-trajectory simulation, centralized cell/segment occupancy, successful
removal, failed-attempt feedback, and hand-authored cross-face puzzles. Cube-edge exits
and directional gates can be enabled independently per level. Gameplay logic
lives in the engine-independent `ArrowCube.Movement` assembly. Rendering and animation
consume its read-only predictions.

## Try the movement lab

1. Open the project in Unity **6000.6.7f1** and let compilation finish.
2. Select **Arrow Cube > Stage 2 > Create Movement Demo**. This adds one component to
   the current scene; save the scene if you want to keep the lab.
3. Enter **Play** and use the **Game** view. The component creates its cube, paths,
   gates, and inspection camera at runtime. No materials, colliders, or camera setup
   is needed. Create only one movement lab in a scene.
4. New demo components start with **Hybrid: edge exits + blue gate**. Activate
   **Green (20)** first: Red (10) blocks it. Orange (30) is blocked by Green.
5. Remove **Red (10)**, then **Green (20)**, then **Orange (30)** through face edges.
   **Blue (40)** can leave independently through its compatible gate. Select
   **Diagram: ordinary edge exits** to run the same four-color layout without gates;
   Blue then exits through its Top-face edge.
6. Use the face inspection buttons to see any of the six faces. Use **Restart exact
   puzzle / cancel animation** at any time to restore the original manual layout.

The two checkboxes in the lab enable cube-edge exits and directional gates separately.
Changing either resets the current manual puzzle and cancels any animation. Restart
retains the selected rules; selecting another fixture loads that fixture's rules.
In the Inspector, disable **Use Puzzle Exit Rules** to override the fixture with the
two serialized switches at startup. Both switches are true by default. Existing saved
demo components retain their selected puzzle index; original fixtures remain gate-only.

The seven fixed puzzles are:

| Puzzle | Exit rules | Intended check |
| --- | --- | --- |
| Straight departure | Gate-only | A straight snake drains completely through a gate |
| Bent snake across three faces | Gate-only | Original bends follow the route, with existing and future face crossings |
| Remove blocker 20, then snake 10 | Gate-only | A distant blocker prevents removal until it leaves |
| Rotated seam: Right to Top | Gate-only | The head's Up heading becomes Left across a seam; a Top-face blocker is detected |
| Closed circuit: no exit | Gate-only, no gates | A diagnostic ungated circuit cannot be removed |
| Diagram: ordinary edge exits | Edge-only | Red → Green → Orange, with Blue independently removable |
| Hybrid: edge exits + blue gate | Both enabled | Red/Green/Orange use edges; Blue uses an earlier interior gate |

Activation and face inspection use test-harness buttons. Clicking a path, orbiting,
mouse gestures, touchscreen gestures, and gameplay UI belong to later stages. The
component uses unlit LineRenderers and simple original gate markers; these are test
visuals rather than final meshes or UI.

## Independently configurable exit rules

The stage 2 update explicitly adds ordinary cube-face edge departures alongside
the existing gate mechanic. This supersedes the initial gate-only design choice;
the underlying connected topology and original gate behavior remain intact.

`ExitRules` is immutable per-level configuration:

| Level type | EnableCubeEdgeExits | EnableDirectionalGates |
| --- | --- | --- |
| Ordinary edge exits (`ExitRules.EdgeOnly`) | true | false |
| Gate-only (`ExitRules.GateOnly`) | false | true |
| Hybrid (`ExitRules.Hybrid`, default) | true | true |

`new ExitRules()` and simulation without an explicit rules argument enable both
mechanics. The original manual fixtures explicitly select `GateOnly`, preserving
their original solutions and animations. Both switches can also be false for
diagnostics (`ExitRules.None`); this does not provide another playable level type.

**Cube-edge exit:** the head advances in its pointing direction on its current face.
If its next surface step would cross an edge and edge exits are enabled, the head
instead leaves along that face's local tangent, continuing straight beyond the
boundary. It does not enter or test occupancy of the would-be neighbor cell on the
adjacent face. A blocker before the edge still prevents removal. At integer samples,
the first outside position is one cell spacing beyond the final cell center, and
continuous animation crosses the actual edge halfway through that step.

**Directional gate:** a gate is a cell plus the
head's face-local heading. When the head is on that cell with that heading, its next
step leaves along the face's outward normal. Subsequent leading steps advance one
cell unit farther along that normal. Every trailing segment follows exactly the
same surface route and departure turn. A gate with a different heading is ignored.
Both conditions affect the movement policy; they do not change `CubeTopology` or
disconnect any grids. Gate cells remain ordinary occupied or empty cells until traversed.
Other paths can block a gate or its approach like any other surface location.

In the lab, cyan diamonds mark gate cells, an inner chevron identifies the required
heading, and the outward stem indicates departure. Disabled gates are hidden in the
lab without deleting their authoring data. A successful path disappears
only once its **tail** is off the surface, not when its head reaches the gate.

The first enabled valid exit along the head's trajectory is used. A compatible
interior gate can therefore be used even when edge exits are enabled. If a matching
gate and ordinary edge departure are available on the same boundary step, the
ordinary edge wins the tie; gates have no special priority. In gate-only mode, that
same boundary gate retains its original normal departure. Wrong-heading gates are
ignored. A gate cannot skip a blocking path occupying its cell.

Only the leading head evaluates exits. Trailing segments always follow the recorded
route, including its original bends and face crossings, and follow the selected
departure until the entire snake leaves. With edge exits disabled the head continues
wrapping across ordinary seams as before. With edge exits enabled the head leaves
its current face at its first unobstructed boundary, while an existing cross-face
body remains one continuous snake.

The diagram fixture uses Front/Top/Right for Faces 1/2/3 and path IDs 10/20/30/40 for
Red/Green/Orange/Blue. Tests remove Blue at every possible point in the dependency
sequence, verifying that it is independent and that Green and Orange remain blocked
by their respective predecessors until those paths are actually removed.

## Logical architecture

`SurfaceGrid` owns active path IDs, cell owners, and undirected segment owners across
all six faces. Adding a path validates its entire geometry against this grid and
checks all conflicts **before** writing anything. Duplicate IDs and overlaps fail
atomically. Removal clears both kinds of occupancy and advances a revision counter.
The exposed path dictionary and path geometry are read-only.

`ExitMap` is an immutable set of directional gates. `PathPosition` describes a surface
cell or an integer distance outside its departure cell, including the selected exit
type and tangent direction for an edge exit. Gate positions continue using the face
normal. These types and `ExitRules` are pure C# data.

`PathMovementSimulator.Simulate(grid, id, exits, rules)` builds a `MovementPlan` containing:

- The exact original path and grid revision.
- Immutable exit rules and the actual `ExitType` (CubeEdge or DirectionalGate) on success;
  failed predictions have no exit type.
- A chronological tail-to-head rail containing the original body and future head steps.
- A deterministic outcome: Removed, BlockedByPath, SelfCollision, or NoExit.
- The safe prefix length and, for collisions, the attempted head cell and blocker ID.

`GetPosition(step, bodyIndex)` samples the discrete head-to-tail body at a logical
step without storing a full copy of the body per step. Plan storage is proportional
to body length plus head travel, rather than their product. Returned collections
cannot be mutated by callers. Simulation never changes the grid or immutable path.

At each step the head advances in its current heading, using the topology's returned
heading after a seam. Each subsequent body position becomes the previous position
of the segment ahead of it. The tail vacates simultaneously. A head entering that
just-vacated tail cell is legal; entering another still-occupied moving body cell
is a genuine self-collision. Original occupancy belonging to the activated path is
ignored for external collision checks, avoiding stale-self-occupancy false positives.

### Collision completeness

Every new head cell and connecting grid segment is checked against every other
active path through centralized owner lookups. No cell is skipped, even when the
visual animation advances several steps in one rendered frame. There is no physics,
raycast, or frame-rate-dependent logical collision decision.

Initially all body cells and segments belong exclusively to this path. A trailing
segment moves only over its predecessor's exact route. Therefore every later body
location/connection is either an original validated body location/connection or a
previously checked head location/connection. This is why validating each new leading
cell and segment covers the **whole moving body**, including its bends and seams.
The presentation retains those intermediate bends instead of interpolating across
them. Other paths remain stationary for the duration of an attempt.

Repeated directed head states terminate a route with no reachable enabled exit as NoExit. On this cube a
straight surface heading repeats after `4*N` steps; the simulator uses state detection,
not an arbitrary iteration timeout. Once departed, the snake terminates after its
remaining surface body has drained. No failure outcome can be committed.

### Commit and cancellation

```csharp
var definition = ManualPuzzles.Create(2);
var grid = definition.CreateGrid();
var simulator = new PathMovementSimulator();
var prediction = simulator.Simulate(grid, 10, definition.Exits, definition.Rules);
// Inspect prediction. Animate it if successful; give feedback if blocked.
// After successful animation:
bool removed = grid.TryCommit(prediction);
```

The example retains the original gate-only fixture. It initially predicts a blocker
rather than removal. Simulating and committing path 20 with those same rules first
makes a fresh prediction for path 10 succeed. To configure a level independently:

```csharp
var rules = new ExitRules(enableCubeEdgeExits: true, enableDirectionalGates: false);
var prediction = simulator.Simulate(grid, pathId, exitMap, rules);
```

Always pass the level's rules when predicting, validating, and replaying a puzzle.
Omitting the fourth argument defaults to Hybrid; callers reproducing old gate-only
layouts must explicitly pass `ExitRules.GateOnly` or their stored level rules.

`TryCommit` accepts only a successful plan for the exact same grid, original revision,
and original active path instance. Foreign, stale, failed, and already committed plans
return false. Callers must serialize attempts; the lab locks activation while an
animation runs. Its reset stops the coroutine and replaces the grid, so an old plan
cannot remove a path in the restarted puzzle. Adding/removing other paths during an
animation is outside the supported lab flow; a stale plan cannot commit afterward.

## Presentation and failed attempts

`PathAnimationGeometry` samples continuous distances along the logical rail. It
retains all intermediate route vertices between the interpolated head and tail and
inserts the shared midpoint for every cube seam. Endpoints follow the two surface
halves of each seam rather than a straight chord through the cube. At a gate the
rail turns outward along the normal; at an edge it continues in the original
face's tangent plane beyond the boundary. Trailing segments follow either route
without shrinking the snake. The reported exit type selects the departure geometry
and the lab's feedback; the original gate markers and normal animation are retained.

The lab computes the **entire result before** starting animation. A successful
prediction animates the full rail at a configurable cells-per-second speed, then
commits the removal and updates the remaining count. Original occupancy stays
reserved throughout animation. A blocked prediction now follows its entire verified
safe prefix, including cube seams. Its head approaches the visible blocking ribbon
or triangle with a small clearance, pauses for 0.18 seconds, then reverses along
the same rail at 1.5 times the forward speed. At contact the moving arrow turns red,
the blocker turns orange, and a small orange ring marks the obstruction in the
actual game view. Both arrows regain their original colors and the moving arrow
returns to its exact initial geometry. Occupancy never changes during a failed
attempt; stage 5 records one mistake after the return finishes. Pause freezes both
travel and the contact hold, and restart cancels the whole attempt.

The diagnostic no-exit circuit still uses a short 0.15-cell nudge rather than
following an unbounded circuit. Self-collision feedback follows the safe prefix
with conservative clearance. Generated puzzles already reject self-blocking arrows.

`ColoredCubeSurface` gives the six faces distinct light colors: blue, mint, peach,
lavender, yellow, and pink. It uses the same opaque unlit shader as the arrows,
so every face remains evenly visible without scene lighting and cannot receive
dark shadows. Face colors stay fixed as the camera rotates, making edges visible
in the test lab and game scene, including Web builds.

There are no hearts or failure penalties in stage 2. All failed paths remain
activatable. Standard/unlimited mode settings and penalties belong to stage 5.

## Verification

Run both suites in Unity's **Window > General > Test Runner**, or run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\RunStage2Tests.ps1
```

`RunUnityTests.ps1` copies the actual source and tests to a fresh minimal Unity
project under `Temp/Stage2Validation`. It compiles and runs EditMode, then PlayMode,
in separate hidden Unity processes without touching the user's open scene. Each
mode produces an XML result file and Editor log. It checks the process exit code,
nonzero test count, and passed result. The stage 1 wrapper remains available for
the full current EditMode suite.

Verified on **2026-11-15**, Unity **6000.6.7f1**:

- **99/99 EditMode tests passed**, including all 40 stage 1 tests.
- **5/5 PlayMode tests passed**.
- No failed or skipped tests. Unity compiled the runtime, presentation, Editor menu,
  and test code.

Results are at:

```text
Temp/Stage2Validation/Project-b7849656dda04a34b51724b9dc261e75/results-EditMode.xml
Temp/Stage2Validation/Project-356fd98d0b5f4c699a309819222e7723/results-PlayMode.xml
```

New checks cover straight and multiply bent routes, all six starting faces and four
headings, multiple edge crossings, rotated-seam collisions, distant blockers,
neighboring paths, removal of blockers, complete tail departure, wrong-direction
gates, closed circuits, moving self-collision versus a vacated tail, atomic occupancy,
stale/double/foreign commits, deterministic restart, fractional seam interpolation,
preserved body bends, and exact visual restoration. PlayMode tests construct all
manual visuals, exercise failure then successful dependency removal, serialize input,
and cancel an animation through restart. Additional checks exercise all independent
switch combinations, tangent departures on all faces and headings, edge/gate ties,
gate occupancy, gates before distant blockers, gate-only compatibility, the diagram's
dependency order with Blue removed at each possible point, colored failure restoration,
and a complete hybrid puzzle using both animations. Rule changes cancel old plans,
and restart preserves the chosen configuration. Batch tests use the null graphics device;
final visual appearance should be inspected in the Editor's Game view.

## Stage 3 integration

Procedural generation is now implemented and verified. See
[Stage 3 generation](Stage3-ProceduralGeneration.md) for the generation lab, difficulty
assets, seeded recipes, verified solutions, and 1,008-puzzle stress reports. Manual and
generated layouts now share `PuzzleDefinition`; the original manual fixtures and
movement rules remain available. The stage 2 renderer also accepts a generated
definition. Generated exit rules must be changed by regenerating with a new recipe,
so their verified solution is never silently invalidated.

## Scope of the original stage 2 implementation

At the end of the original stage 2 implementation, no generation algorithm, procedural-solution
validator, input/orbit controller, hints/undo, settings, progression, persistent save,
platform build, final mesh, audio, or visual-polish work is included. Manual puzzles
use fixed sizes 6 and 8; the logical model and simulator use the supplied topology size.
The lab reserves one attempt at a time and is not a concurrent-motion solver. It
uses test LineRenderers rather than pooled/batched production meshes. Gates are
explicit authoring data. Stage 3 must generate and store the two independent exit
switches per level and validate every candidate/solution using those same switches.
An edge exit or enabled compatible gate is valid only when the full trajectory is
clear. Disabling a mechanic must affect generation validation exactly as it affects
gameplay; replay must preserve the rules alongside the layout. These generation
requirements are now handled by stage 3.
