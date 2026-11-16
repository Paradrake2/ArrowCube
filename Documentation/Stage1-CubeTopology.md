# Stage 1: connected cube surface

Stage 1 implements the cube topology and path data foundation. It includes a Scene-view
preview and EditMode tests. It does not implement activation, movement, occupancy,
collision, procedural generation, or removal animation.

## Editor setup

1. Open the project in Unity **6000.6.7f1** and let scripts/packages import.
2. Select **Arrow Cube > Stage 1 > Create Topology Preview**.
3. In the Scene view, enable **Gizmos**. The menu selects and frames the preview.
4. Orbit the Scene view to inspect all six labeled faces. Colored dots are cell
   centers; links wrap over every seam. The black arrow is **one continuous path**
   crossing Front, Right, and Top, with a bend on Right.
5. Change **Grid Size** on GameManager (default 6) or **Side Length** on the preview.
   The model and sample path rebuild for the new density. Save the scene if desired.

The preview uses Editor gizmos; Play mode's Game view does not yet contain a rendered
puzzle. No camera, materials, colliders, prefabs, or scene changes are required for
the logical foundation. The existing SampleScene is left available for subsequent work.

## Tests

Use **Window > General > Test Runner > EditMode > Run All**. The test assembly is
`ArrowCube.Topology.Tests`. The project explicitly references its existing Unity Test
Framework version (1.8.0).

To run without interrupting an already open Editor, from the project directory:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\RunStage1Tests.ps1
```

The script starts the same Unity version in batch mode against a fresh, minimal test
project inside `Temp/Stage1Validation/Project-<id>`. It copies the actual stage 1 source
and tests, compiles the Unity presentation and Editor code, runs the tests, and checks
both the exit code and NUnit results. Logs and XML results remain in that directory.
It does not close or modify the scene in the user's running Editor. To select another
installation, supply `-UnityPath 'C:\path\to\Unity.exe'`.

After stage 2, this wrapper delegates to `RunUnityTests.ps1` and runs the full current
EditMode suite. The original stage 1 tests remain included. Current log/result file
names include the test platform, for example `results-EditMode.xml`.

Coverage includes all 24 directed mappings (12 physical edges), all offsets in grids
of sizes 1, 2, 3, 6, 8, and 20, reverse traversal, heading rotation, seam agreement,
all corner-adjacent cells, whole-surface connectivity, repeated wraps, invalid inputs,
immutable cross-face geometry, and transformed Unity world positions. Seam geometry
is checked independently against integer face frames rather than only against the
mapping table's reverse entries.

Verified on **2026-11-15** with Unity **6000.6.7f1**: compilation succeeded and
**40/40 EditMode tests passed**, with zero failures or skipped tests. The successful
isolated run is stored at
`Temp/Stage1Validation/Project-3dd8e640fb90457cbcc330c3d92e08bd/results.xml`.

## Coordinate and orientation contract

`ArrowCube.Topology` is a C# assembly with **no Unity engine references**. All gameplay
coordinates and connections are integers. World-space conversion lives separately in
`ArrowCube.Presentation`.

`SurfaceCoordinate(Face, X, Y)` identifies a **cell center**, not a shared mesh vertex.
For an N by N face, X and Y run from 0 through N-1. Looking at a face from outside,
X increases right and Y increases up. Faces have these fixed cube-local frames:

| Face | Outward normal | Local right | Local up |
| --- | --- | --- | --- |
| Front | +Z | +X | +Y |
| Right | +X | -Z | +Y |
| Back | -Z | -X | +Y |
| Left | -X | +Z | +Y |
| Top | +Y | +X | -Z |
| Bottom | -Y | +X | +Z |

Each cell has exactly four neighbors. `CubeTopology.Step(cell, direction)` returns
the neighboring coordinate, its face-local forward direction, and whether a seam
was crossed. Continue with the **returned direction** after crossing an edge:

```csharp
var step = topology.Step(cell, direction);
cell = step.Coordinate;
direction = step.Direction;
```

Each step crosses at most one edge. At a face corner the chosen cardinal direction
selects one of two different seams. There is no diagonal step or automatic branching.
The three cells nearest a physical cube corner can be traversed in a three-step
cycle, with one explicit edge crossing per step.

`GetCellIndex` and `GetCoordinate` provide dense reversible IDs over all `6*N*N`
cells. `EnumerateCells` includes every face; neighbors are available by stepping in
each of the four `SurfaceDirection` values. `GetOrientation` supplies each cell's
face frame.

### Explicit edge table

Each entry below is `destination face / destination entry edge`, with `*` indicating
that the edge offset is reversed. Offset increases along Y on left/right edges and
along X on up/down edges. Reversed offset is `N-1-offset`. Forward direction on the
destination is the opposite of its entry edge.

| From | Right | Up | Left | Down |
| --- | --- | --- | --- | --- |
| Front | Right / Left | Top / Down | Left / Right | Bottom / Up |
| Right | Back / Left | Top / Right | Front / Right | Bottom / Right * |
| Back | Left / Left | Top / Up * | Right / Right | Bottom / Down * |
| Left | Front / Left | Top / Left * | Back / Right | Bottom / Left |
| Top | Right / Up | Back / Up * | Left / Up * | Front / Up |
| Bottom | Right / Down * | Front / Down | Left / Down | Back / Down * |

For example, at N=5, stepping Up from Right(1,4) arrives at Top(4,1), now pointing
Left. Stepping Down from Right(1,0) arrives at Bottom(4,3), also pointing Left.
Stepping backward using the opposite returned heading restores both the original
cell and the original reverse heading.

### Exact positions and rendering

For face normal `n`, local right `r`, and local up `u`, the doubled integer position is:

```text
p2 = n*N + r*(2*X+1-N) + u*(2*Y+1-N)
localPosition = p2 * (sideLength / (2*N))
```

`CubeSurfaceGeometry.GetWorldPosition` applies the cube Transform after conversion,
so translation, rotation, and scale do not affect logical adjacency. A nonuniform
scale is supported geometrically, but changes physical spacing between faces.

A connection over a seam is rendered as two segments meeting at the exact edge
midpoint, rather than one straight chord inside the cube. With raised geometry, the
seam point is offset along **both** face normals; the two segments still meet at one
point outside the cube. `WritePolyline` preserves every body bend and seam. This is
a foundation for later mesh generation, not a finished path mesh or animation.

## Path data contract

`ArrowPath` stores an immutable ID, a defensive copy of connected cells in **head to
tail** order, normalized undirected `SurfaceSegment` connections, a leading endpoint,
the arrowhead's face-local heading, and the number of face transitions. The constructor
rejects empty routes, repeated cells, disconnected cells, diagonals, out-of-bounds
coordinates, and a head heading that does not point away from the neck. Single-cell
paths require an explicit heading.

`SurfaceSegment(a,b)` compares equal to `SurfaceSegment(b,a)`, including across seams.
This gives the next stage a common key for segment occupancy. Cell occupancy and
dynamic active/removed/moving state will be separate from immutable path geometry.
The puzzle manager, rather than a path constructor, will enforce ID uniqueness among
multiple paths. All paths in one puzzle must be validated against that puzzle's topology.

The selected future movement model is the brief's snake model: extend the head in
its current heading, rotate that heading at each seam, and have each trailing segment
follow the previous segment's route. Do not translate an entire bent path rigidly.
Stage 1 does not simulate or claim to verify that movement yet.

## Exit rule to resolve before stage 2

A closed cube surface has no boundary. In this topology every edge is a wrap, and a
straight heading eventually cycles around four faces. An unobstructed trajectory
alone therefore does not make a snake leave the surface. Treating a wrap as an exit
would violate the brief.

For stage 1 the consistent choice is a fully closed surface with **no implicit exits**.
Stage 2 needs an explicit gameplay exit rule before successful removals can be
implemented. Possibilities include designated departure gates, a player-visible
departure condition after a complete clear circuit, or a deliberately specified
lift-off rule. These have different puzzle-generation consequences and must not be
silently substituted for one another. Full movement, collision, and exit tests must
pass before procedural generation begins.

The stage 2 update now supports independently configured cube-edge exits and
directional gates. The topology itself always supplies wrapping neighbors; the
movement policy decides whether the leading head departs or wraps. Existing gate-only
fixtures retain their behavior. See [Stage 2 movement](Stage2-CoreMovement.md) for the
current rules, simulator contract, demo setup, and verification results.
