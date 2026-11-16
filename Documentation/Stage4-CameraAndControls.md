# Stage 4: camera, mouse, touch, and path selection

Advanced controls now also expose [multi-blocker targets and debug inspection](MultiBlocker-Dependencies.md).
Debug inspection is opt-in and leaves normal path activation unchanged when disabled.

The panel now includes five measured-difficulty presets, a master slider, expandable
Custom controls, progress/cancellation, debug views and JSON export. See
[Advanced procedural difficulty](Advanced-Procedural-Difficulty.md) for setup, exact
metrics, the matched benchmark and the distinction between requested and measured scores.

Stage 4 adds direct path activation, a free orbit camera, desktop and multi-touch
input, UI capture, safe-area-aware controls, and device-level input tests. It reuses
the existing generated puzzles and deterministic movement/collision simulator.

## Editor setup

1. Open Unity **6000.6.7f1** and let packages/scripts import. The existing Input System
   package is **1.20.1**; active input handling remains the new Input System.
2. Disable or remove older movement/generation lab roots in the scene. Use one lab
   at a time to avoid competing cameras, UI, and input subscriptions.
3. Select **Arrow Cube > Stage 4 > Create Controls Demo**.
4. Enter **Play**, open the **Game** view, and click a visible black path. It uses the
   same complete preflight simulation and snake animation as the earlier stages.
5. Use the controls below to inspect and play the generated puzzle. The panel can
   scroll on small screens. In portrait it moves below the puzzle; in landscape it
   sits beside it. Its layout respects the platform's safe area.

No action asset, colliders, camera references, UI prefab, or EventSystem setup is
required. Runtime components create and wire these controls automatically. The lab
uses UGUI with `InputSystemUIInputModule`, including touch-capable buttons, toggles,
sliders, and scrolling. It reuses an existing EventSystem where possible. An active
legacy `StandaloneInputModule` is temporarily disabled to avoid polling the legacy
input API; an existing configured Input System UI module is preserved.

The generator's seed, starting profile, exit flags, and optional difficulty asset can
still be configured on `Stage3GenerationDemo` in the Inspector. The stage 4 panel can
select a profile and both independent exit switches for the next generation. The
active level keeps its original rules until a new puzzle is successfully generated.
Existing edge-only, gate-only, and hybrid mechanics are unchanged.

The control panel is built in `Start`, after the input and generation components
have completed `Awake`. This also supports loading a saved scene regardless of
component Awake order. Failed initial generation displays its error without loading
the four-arrow manual demo; correcting the custom profile and regenerating recovers
in the same session. Custom profiles support grids up to 64x64, with optional gate
budget scaling described in [Stage 3](Stage3-ProceduralGeneration.md).

## Controls

| Input | Action |
| --- | --- |
| Left click a visible path | Activate on clean release over the same path |
| Left drag from empty space | Orbit the camera |
| Left drag beginning on a path | Cancel selection; use right-drag to orbit from there |
| Right mouse drag | Orbit from any world location |
| Mouse wheel over puzzle | Zoom toward/away from cube center |
| R | Smoothly reset camera orientation and zoom |
| F11 | Toggle windowed / borderless fullscreen in a player |
| Tap a visible path | Activate on clean release over the same path |
| One-finger drag | Orbit, including when the touch began on a path |
| Two-finger pinch | Zoom; spreading fingers zooms in |
| Face buttons | Smoothly inspect Front, Right, Back, Left, Top, or Bottom |

Camera sensitivity is adjustable in the panel and Inspector. Rotation inertia is
optional and off by default. Window resizing is enabled in Player Settings. Desktop
resolution/graphics settings and platform builds remain later-stage work.

For faster/slower wheel zoom, select the puzzle root and adjust **Zoom Speed** on
**Orbit Camera Controller** in the Inspector. Its range is 0.05–2.0 and the default
is **0.35** (higher means faster). Changes apply immediately in Play mode. Set the
value outside Play mode to save it with the scene. This controls the distance per
wheel notch; pinch zoom keeps its direct finger-distance behavior. Normalized wheel
input is used directly, with conversion only for the optional legacy Windows range.

Mouse and one-finger drags now use the same direction by default. Desktop keeps
its existing behavior; touch reverses its previous behavior to match it. To reverse
either device independently, use **Invert mouse rotation** or **Invert touch
rotation** in the test scene panel or the game's **Settings** menu. Both toggles
start off and reverse both axes when enabled. The Inspector also exposes them on
**Puzzle Input Manager**. Game preferences persist locally, and lab choices travel
with a test puzzle into the game and are restored when returning to the lab.
Enabling **Invert touch rotation** restores the previous touch direction. Wheel
and pinch zoom directions are unchanged.

To thicken **all arrows**, select the puzzle root and expand its **Stage 2 Movement
Demo** component. Adjust **Arrow Thickness** in the Inspector (0.05–0.45 of a grid
cell; default 0.22). Arrows use thick line bodies with **filled triangular heads**.
The head scales to stay wider than the body, which joins at the triangle's base.
It changes every visible arrow body and arrowhead, including
arrows already on the cube when adjusted in Play mode. Picking follows the visible
width. Set it outside Play mode to retain the value in the saved scene.

Arrow bodies now use **surface-aligned ribbon meshes**, replacing camera-facing
LineRenderers. Every segment follows its face plane, seam cross-sections are shared,
and bounded miter joins keep right-angle bends square and consistent. There are no
rounded billboard joins that swell or twist as the camera rotates. Filled heads
use the same surface frames when folding around an edge or leaving a gate. Restart
Play mode after this renderer update to rebuild any existing runtime arrows.

H, Z, and Esc are not wired to placeholder gameplay behavior: hints, undo, and pause
belong to stage 5. The lab retains restart/regeneration buttons. R resets only the
camera, while **Restart exact puzzle** restores the same immutable puzzle definition.

## Gesture safety

`PointerGesture` tracks a press, its original path candidate, movement threshold,
duration, and whether activation was cancelled. Activation requires all of these:

- The press began in the puzzle viewport outside UI.
- Movement never reached the drag threshold, even if the pointer returned to its
  starting point. Final motion arriving only with the release is checked too.
- The gesture lasted no longer than 0.6 seconds.
- Release is outside UI and still over the same active path.
- The puzzle is idle and still matches the definition from the press.

Mouse and touch thresholds/tolerances account for DPI. A path is never activated on
pointer-down. Mouse actions handle press/release events individually, including both
events in one Input System update, rather than losing fast clicks through polling.
Duration uses device event timestamps, so a buffered long press does not become a
short click merely because both events are processed in the same rendered frame.

A second finger cancels tap recognition. A pinch stays a pinch until **all** fingers
lift; the remaining finger cannot become a tap or one-finger drag. A third finger
blocks the touch session until all contacts lift. If any participating touch begins
on UI, that session cannot manipulate the cube. Cancelled touches never activate.
Mouse input is suppressed during touch and briefly afterward to prevent duplicate
activation from emulated mouse events.

Focus loss, application pause, input disable/device reset, camera reset, viewport
layout changes, and puzzle replacement cancel pending gestures. A held press cannot
survive a reset and then activate the restarted puzzle. Input System action
cancellation is distinguished from physical release, including device reset while
the button is still held. UGUI is raycast at the pointer's actual position instead
of relying on UI hover state from a previous frame.

The camera remains usable while a path animates; path activation is serialized by
the existing movement driver. Camera presses stop residual motion before selecting,
so inertia cannot move another path under an otherwise stationary click.

## Camera architecture

`ArrowCube.Controls` references Unity's Input System and the existing presentation
assembly. It does not alter the engine-independent topology, movement, or generation
assemblies.

`OrbitCameraController` stores current/target quaternions. Screen-space yaw and pitch
increments are applied in the camera's local frame, without Euler pitch clamps. The
camera can pass over both poles, go upside down, rotate completely, and inspect the
underside and rear. There is no hard vertical limit hiding a face.

Quaternion interpolation and zoom use exponential smoothing with unscaled delta
time. Drag sensitivity is normalized by the viewport's shorter dimension, rather
than tied to a particular resolution. Optional angular inertia decays after release.
Reset cancels inertia. The camera follows the cube center and supports orthographic
or perspective projection, bounded zoom, and root transforms. The lab retains the
existing orthographic presentation.

`PuzzleScreenLayout` computes separate screen-pixel UI and camera rectangles. These
never overlap and stay inside the safe area. DPI-aware control scaling prevents a
high-resolution portrait device from shrinking the panel's text and touch targets
to desktop-pixel dimensions. The panel scrolls instead of covering the puzzle.

## Path picking

`PathPicker` caches immutable body and arrowhead polylines and filters removed paths
against the current occupancy grid. It projects the actual surface/seam geometry
into the active camera's viewport, choosing the nearest stroke inside the mouse or
touch tolerance. Both visible portions of one cross-face path select that same ID.
Arrowheads and single-cell paths are included.

Each candidate is checked against an analytic cube intersection in cube-local space.
A stroke on the rear face cannot be clicked through the opaque cube. Picking uses
the current camera and root transform and supports perspective-correct interpolation.
It does not add a collider per path or use physics to decide gameplay collisions.
No gate markers or other UI visuals are treated as paths.

The picker runs on press/release, not every movement frame. Cache rebuilding is tied
to the immutable puzzle definition; removal requires only the active-path lookup.
All activations go through `Stage2MovementDemo.TryActivate` and the existing simulator.

## Verification

Run the game's EditMode and PlayMode suites in **Window > General > Test Runner**, or:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\RunStage4Tests.ps1
```

The runner creates a separate Unity project and exports results/logs to
**TestResults/Stage4**. Input System test fixtures are enabled using the package's
documented `testables` configuration. Virtual Mouse, Keyboard, and Touchscreen
devices feed actual Input System state events and EnhancedTouch callbacks. The
fixture isolates tests from physical input and restores the previous system afterward.
The runner selects the game's test assemblies rather than running package self-tests.

Checks cover clean/fast clicks, drag/release suppression, empty-space and right-drag
orbit, touch taps and drags, pinch zoom, two-to-one and three-finger transitions,
UI-origin gestures, cancellation/focus loss, reset/disable during a press, wheel and
keyboard actions, all six camera faces, both poles, bounded zoom, inertia/reset,
portrait/landscape/high-DPI layouts, hidden-face occlusion, cross-face picking, and
integration with an existing EventSystem. The full prior-stage suites and the
1,008-puzzle generation/replay test remain included.

Batch tests use the null graphics device. They verify UI construction, input delivery,
selection, camera geometry, and logical state; physical device feel, rendered appearance,
and Windows/Android/iOS builds still need Editor/device/build testing. In particular,
the F11 action is observed through a test callback rather than changing the test
machine's display mode.

### Verified results

Verified on **2026-11-15**, Unity **6000.6.7f1**:

- **164/164 EditMode tests passed**.
- **19/19 PlayMode tests passed**.
- No failed or skipped tests, and no C# compilation errors or warnings.
- The **1,008-puzzle generation/replay regression** still passed with zero failures.

The final PlayMode run also covers a long mouse press whose down/up events are
buffered into one frame and a pre-existing legacy EventSystem. Results and logs are
in [TestResults/Stage4](../TestResults/Stage4/Summary.md).

The surface-ribbon fix additionally passed **174 EditMode + 22 PlayMode tests**
(196 total), including every cube seam, all six face planes, animated generated
ribbons, bounded corner widths, input, and the 1,008-puzzle regression. Actual
graphics renders were also inspected from
[front](../TestResults/SurfaceRibbons/ribbon-front.png),
[grazing](../TestResults/SurfaceRibbons/ribbon-grazing.png), and
[corner](../TestResults/SurfaceRibbons/ribbon-corner.png) views. These used a graphics
device in an isolated Unity project, in addition to the regular null-device suites.

## Remaining scope

This is the stage 4 playable control lab. Production menus/HUD, accessibility tuning,
mobile device performance, persistent sensitivity/settings, hint/undo/hammer buttons,
pause/hearts, progression/saves, final visual/audio polish, and platform builds remain
later stages. Generation remains synchronous and path meshes are still individual
test objects rather than a pooled/batched production renderer. Physical multi-touch
hardware has not been tested here.
