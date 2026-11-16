# Phase 3: surface geometry, rendering and playable authored shapes

Phase 3 connects the Phase 1/2 cell-ID model to physical quadrilateral geometry.
Both authored demos use the existing `GameplayController`, `GameplaySession`,
`PathMovementSimulator`, input manager, ribbon renderer, triangle heads and HUD.
There is one movement implementation and one arrow presentation path.

## Open and play the demos

1. Open Unity **6000.6.7f1** and allow scripts to import.
2. Choose **Arrow Cube > Phase 3 > Open L-Shaped Demo**, or open
   `Assets/Scenes/SurfaceL.unity` directly.
3. Enter Play. The puzzle is loaded immediately. Click/tap arrows normally.
4. Try the arrow running around the lower step before clearing its blocker. It
   advances through the inside fold, highlights the contact, then returns.
   **Hint (H)** identifies a free arrow. Clear it, then watch the blocked arrow
   traverse the inside and outside corners before leaving through its gate.
5. Orbit to find the rear arrow and clear the whole puzzle. Existing Undo,
   Hammer, Pause, Restart, Settings and inversion/zoom/thickness controls work.
6. Select **Oblique prism** in the demo selector, or open
   `Assets/Scenes/SurfacePrism.unity` / the corresponding Phase 3 menu command.

Desktop controls: right-drag anywhere over the puzzle, left-drag from empty space,
wheel zoom, R reset. Touch: one-finger orbit, two-finger pinch, tap to activate.
The selector respects the safe area and scales down on narrow screens. Its edge
exit button reloads the authored puzzle with ordinary edge exits enabled/disabled.
Gate-only is the default so arrows actually travel around the folds; enabling edge
exits allows earlier departures at eligible outside boundaries. Inside boundaries
remain traversable.

Both scene assets are included in Editor Build Settings after the original Game
and TestScene entries. The main campaign/lab behavior is unchanged. Demo preferences
and progress use the separate `arrow-cube-surface-demo.json` store. The selector's
shape change starts that authored puzzle again. The shared game's Continue Sandbox
can restore these authored demos with their geometry.

## Logical topology versus geometry

`ISurfaceGeometry` lives in `ArrowCube.Presentation`. It supplies:

- The associated immutable `ISurfaceTopology`.
- Cell quadrilaterals with center, outward normal, local boundary positions and
  local directional tangents.
- A conservative reference cell size for consistent arrow width and head size.
- Root-local bounds for orbit framing.
- Rendering quads, which may cover several logical cells (the cube keeps its six
  large rendering faces).

`SurfaceGeometry` exposes center/normal/direction/boundary sampling and actual
quad-triangle ray intersections. `QuadSurfaceGeometry` copies a cell-ID/quad map,
validates it, and exposes read-only geometry. `SurfaceQuad` is immutable. All
positions and directions are local to the puzzle root; root translation/rotation
and scale are applied consistently by rendering and interaction.

No Unity types were introduced into topology, movement, dependency logic or the
generator. `CubeSurfaceGeometry` retains its coordinate methods and implements the
new geometry contract. Existing cube coordinates, layouts, palettes and shader
resources retain their meanings.

## Authoring a valid surface

First construct and validate the logical graph with `SurfaceTopologyBuilder`.
Then provide exactly one convex planar `SurfaceQuad` per logical cell ID. Corners
are ordered **bottom-left, bottom-right, top-right, top-left**, as viewed from the
outward side. Directions keep Right=0, Up=1, Left=2, Down=3. Corner coordinates,
dimensions, normals and quad shapes can vary; cells need not be axis aligned,
square, equal-sized, or associated with a `CubeFace`.

For every connection, its source boundary and destination entry boundary must be
the same full physical edge with reversed endpoint order. The destination entry
is `NewDirection.Opposite()`. The validation checks finite positions, nondegenerate
convex planar winding, missing cells, boundary alignment, fold metadata and inside
edge-exit eligibility. A physical fold must have `IsGeometricEdge=true`. Concave
folds must disallow ordinary edge exits. Construction does not discover neighbors,
classify an imported mesh, or assign topology automatically.

The current contract supports planar quadrilateral cells on closed surfaces.
Antiparallel, zero-thickness folds are rejected: they need a real bridge/thickness
definition instead of a singular offset intersection. Triangular logical cells,
non-planar cells, open-boundary traversal and automatic mesh conversion remain
future work. Extremely sharp folds need careful cell sizing and offset clearance.

Supply an authored pair to the existing controller:

```csharp
var demo = AuthoredSurfaceDemos.CreateL();
game.LoadPuzzle(demo.Puzzle, sandbox: true, geometry: demo.Geometry);
```

`Stage2MovementDemo.LoadDefinition` and `LoadSession` also accept geometry directly.
The puzzle, grid and geometry must share the same topology instance. The two
delivered demos have a resolver that reattaches their fixed geometry to a restored
logical snapshot. Arbitrary custom geometry is not embedded into `PuzzleSave` in
this phase: its author must supply the matching geometry when loading. The Phase 2
logical graph/session save format and existing cube saves remain compatible.

## Paths, folds and animation

`SurfaceRail` is a presentation view of the simulator's `CellRail`. It retains
explicit connection directions, including reversed initial body connections and
transported head headings. Parallel cell connections do not require guessing from
IDs. No alternative trajectory is calculated for drawing.

`PathAnimationGeometry` maps those samples to the surface. Between surface cells,
it passes through the shared boundary midpoint. A boundary knot is inserted for
changes of plane or a bend in a coplanar connection, preserving every fold within
the moving head-to-tail window. The split fraction is determined by the two
center-to-boundary distances, rather than assuming equal cell dimensions.

The elevated boundary point is the intersection of both offset planes. For unit
normals `nA`, `nB` and lift `h`, the offset is:

```text
offset = h * (nA + nB) / (1 + dot(nA, nB))
```

Its signed distance from each adjacent cell plane is exactly `h`. This reduces to
the previous cube normal-sum rule at 90 degrees and also works at concave and
oblique folds. Direct interpolation between centers is avoided at transitions.
Normals for each ribbon segment come from the actual occupied patch, not a
single arrow-wide normal.

`SurfaceStrokeGeometry` uses the intersection tangent at folded joins, so shared
ribbon vertices remain in both patch planes. In-plane joins retain bounded miters.
`TriangleArrowHead` splits its filled triangle into rows when it straddles a fold.
Ribbon/head normals are assigned explicitly; reversed back-face triangles no
longer cancel them to zero during normal recalculation. The opaque unlit material
continues to keep arrows and faces bright without scene-light shadows.

Movement progress remains measured in the simulator's logical cell steps. Unequal
physical cells can produce different physical travel distances per logical step;
body segments still follow the exact same chronological route. Gate departure
follows the departure cell's outward normal. Eligible-edge departure follows its
current local heading. The departure ribbon plane retains its cross-section.
Blocked travel, contact highlighting, return motion, pause and cancellation reuse
the same renderer and simulator plans.

## Surface rendering and interaction

`SurfaceMeshRenderer` builds the opaque, outward-facing rendering quads. The cube's
`ColoredCubeSurface` is a compatibility wrapper around it, retaining its six
colors. All primary meshes use `ArrowCube/OpaqueSurface`: opaque queue, depth writes
and depth tests enabled, with no transparent sorting fallback.

`PathPicker` samples the actual current ribbon and filled-head triangles, including
their current width. Screen-space padding supports mouse and touch. Perspective-
correct barycentric reconstruction finds the candidate point robustly at triangle
edges, including under nonuniform root transforms. Occlusion is tested against
the actual surface triangles in root-local coordinates. No analytic cube-shaped
occluder or bounding-volume occlusion shortcut is used; the empty L notch stays
empty, while rear arrows are hidden by actual surface panels.

Orbit framing uses geometry bounds and their center. The demo's initial fit also
accounts for viewport aspect. Generic cell focus uses its physical normal and up
direction, with inverse-transpose normal transformation for nonuniform root scale.
The legacy cube face-focus API remains available. The normal HUD hint action now
focuses a cell, which works on either geometry type.

## Authored examples

**L-shaped solid:** an extruded L cross-section with eight side cells and six cap
cells, 14 planar quads in total. The cap is partitioned into quadrilaterals to match
the side edges exactly. It contains convex outside corners and a concave notch.
Arrow 10 spans the lower outside corner and is blocked by arrow 20's tail at the
inside connection. With the blocker removed it traverses the inside wall and
subsequent outside corners to a directional gate. Arrow 30 is on the rear cap.
The verified order is **20, 10, 30**.

**Oblique folded prism:** a closed scalene triangular-prism surface, with each cap
partitioned into three quadrilateral logical cells, plus six side cells. None of
the logical cells is triangular. The side folds include non-right angles, and the
entire authored geometry is rotated in 3D. Cells have different sizes and skew
directions. Arrow 10 crosses successive oblique folds after arrow 20 is cleared;
arrow 30 is on the rear cap. Its verified order is also **20, 10, 30**.

These are fixed authoring fixtures with explicitly listed connections. They are
not a procedural shape generator or mesh-to-topology conversion tool.

## Verification

The full suites passed **330 EditMode + 51 PlayMode tests**, with no remaining
failures or skips. An additional focused run passed all **11 generic geometry
tests**, including a sheared, non-right-angle concave fold. The first full PlayMode
pass exposed a triangle-tip precision issue on the nonuniformly transformed prism;
the picker was corrected and all 51 PlayMode tests were rerun successfully.

All **1,008** cube fingerprints and deterministic CSV fields match the completed
Phase 2 dataset. Existing recipe versions, random sequences, generation ordering,
dependency construction and difficulty algorithms were not changed in Phase 3.

The scene test loads both delivered scenes, switches shapes through the actual
selector, uses the shared hint system and completes each puzzle. Interaction tests
clear a non-cube puzzle with real simulated mouse/touch events, reject rear arrows,
check picking after transformed orbit, and restore geometry after save/undo.
Geometry tests cover shared boundaries, planes, normals, unequal/skew cells,
concave/convex/oblique seams, continuity, head/tail positions, projected ribbon
width and gate/edge departures. Existing cube rendering and controls regressions
remain included.

An actual graphics-enabled PlayMode run with the project's **Mobile RP asset /
Mobile Renderer** passed and captured eight front/rear/contact/moving-fold images.
They were visually inspected and are in `TestResults/SurfaceGeometry/Visuals`.
The run checks that arrow pixels survive depth-tested rendering; it also performs
the complete animations and commits, rather than only capturing static meshes.
This is a URP GPU check, not a deployment or mobile-device/browser test.

To reproduce the graphics run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\RunUnityTests.ps1 `
  -TestPlatform PlayMode `
  -TestFilter 'GenericSurfacePlayTests.BothAuthoredPuzzlesAnimateBlockedAttemptsAndVerifiedRemovals' `
  -Graphics -MobileURP -OutputDirectory .\Temp\GeometryGPU
```

The harness uses an isolated project and leaves the open authoring scene alone.
Its ordinary defaults still run without graphics. See the result summary for
artifact locations and verification details.

## Added and modified files

Added:

- `Presentation/SurfaceGeometry.cs`: geometry contract, immutable quads, validation, sampling and ray queries.
- `Presentation/SurfaceRail.cs`: directed presentation view of simulator/body rails.
- `Presentation/SurfaceMeshRenderer.cs`: shared opaque quad mesh rendering.
- `Presentation/AuthoredSurfaceDemos.cs`: fixed physical L and oblique-prism fixtures.
- `Game/SurfaceGeometryDemo.cs`: authored demo bootstrap and selector over the standard controller/HUD.
- `Editor/SurfaceGeometryDemoMenu.cs`: scene menu commands and isolated scene creation.
- `Scenes/SurfaceL.unity`, `Scenes/SurfacePrism.unity` and Unity metadata.
- `Tests/EditMode/GenericSurfaceGeometryTests.cs`.
- `Tests/PlayMode/GenericSurfacePlayTests.cs`, `GenericSurfaceInteractionTests.cs`, `SurfaceGeometrySceneTests.cs`.

Modified:

- `CubeSurfaceGeometry`, `ColoredCubeSurface`, `PathAnimationGeometry` and `Stage2MovementDemo`.
- `SurfaceStrokeGeometry`, `SurfacePathRenderer` and `TriangleArrowHead`.
- `PathPicker`, `OrbitCameraController` and `PuzzleInputManager`.
- `GameplayController`: supplied/resolved geometry, cell-focused hints and bounds-aware refresh.
- `ProjectSettings/EditorBuildSettings.asset`: preserves the first two scenes and adds both demos.
- `Tools/RunUnityTests.ps1`: opt-in graphics and Mobile URP validation.
- Architecture documentation and `TestResults/SurfaceGeometry` artifacts.

## Remaining assumptions and Phase 4

Cube compatibility coordinate APIs, face controls/debug overlays and the original
generator remain cube-specific. The renderer/controller class name
`Stage2MovementDemo` is retained for serialized component compatibility; its drawing
path is now generic. Physical authoring currently requires closed planar convex
quads with matching full edges. There is no mesh importer, automatic topology
extraction, procedural L/T construction, generic procedural puzzle generation,
non-planar logical cell or automatic persistence of custom geometry assets.

For Phase 4, start with deterministic generation over validated `ISurfaceTopology`
and a stable ordered cell/connection catalogue. Replace generation-only cube face
quotas, coordinate sampling and edge enumeration with surface/patch metadata,
while retaining the existing cube recipe path and historical fingerprints.
Use the shared simulator/validator as the acceptance oracle. Define a stable
surface asset/geometry identifier for saved generated surfaces instead of relying
on the two demo names. Calibrate coverage/difficulty for differently sized physical
cells deliberately in that phase; no scoring or balancing change is made here.
Procedural mesh-to-topology conversion remains Phase 5.
