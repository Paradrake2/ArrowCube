# ArrowCube

**A 3D surface-based arrow puzzle game built in Unity, with deterministic procedural generation and dependency-driven puzzles.**

ArrowCube challenges you to clear a shape covered in connected arrows. Select an arrow to send it forward along the surface: if its route is clear, the entire arrow slides along its path and exits. If another arrow blocks the route, the move fails. The challenge is working out which arrows must be removed first.

The project began on a cube, but now includes a shape-independent logical topology, rendering, and generation pipeline for **manually authored quadrilateral surfaces**, including L-shaped solids, T-shaped solids, and non-right-angle prisms. It is an active hobby project focused on puzzle generation, geometry, and game-system engineering.

## Gameplay

- **Clear the surface:** Remove every arrow to complete a puzzle.
- **Work out dependencies:** Many arrows cannot leave until others have been removed. Some have multiple blockers or form longer dependency chains.
- **Follow the surface:** Arrows bend around connected faces and folds as they slide toward an exit.
- **Choose exit rules:** Puzzles can use eligible geometric-edge exits, directional gates, or both.
- **Recover from mistakes:** Blocked attempts animate forward to the obstruction and then return without changing the puzzle.
- **Use optional assistance:** Hints, undo, and a hammer power-up are available.
- **Play a campaign or experiment:** The main game includes level progression and stars; separate labs expose generation settings and debug tools.

The default game setting allows unlimited attempts. A configurable hearts mode is also available.

## Procedural generation

ArrowCube does more than scatter random paths across a surface. The generator constructs candidate arrows, traces their potential escape routes with the **same movement simulator used by gameplay**, and builds a dependency structure in which arrows can block one another.

Its dependency-order construction can deliberately add an arrow that is initially blocked, provided the new relationships do not create an unsolvable cycle. Final puzzles are checked by a validator that replays a complete removal solution.

Generation settings include:

- Seeded, reproducible layouts and versioned recipes
- Arrow count, length, turns, and surface-coverage targets
- Dependency amount, branching, independent blockers, and opening-move limits
- Edge-only, gate-only, or hybrid exits
- Optional measured-difficulty search and candidate optimization
- Bounded attempts, cancellation, and progress reporting

The generator operates on connected surface-cell graphs rather than requiring a particular 3D shape. Historic cube generation is kept backward-compatible through dedicated adapters and versioned recipe handling.

## Supported surfaces

| Surface | Status | Notes |
| --- | --- | --- |
| Cube | Playable | Original gameplay and campaign surface |
| L-shaped solid | Playable | Authored geometry with concave and convex folds |
| T-shaped solid | Playable | Authored geometry spanning several branches |
| Oblique prism | Playable | Authored non-right-angle geometry |
| Imported arbitrary mesh | Planned | Automatic mesh-to-topology conversion is not yet implemented in the current repository |

The procedural surface lab lets you choose an authored shape and **change the logical grid density** with its *Cells per authored quad side* setting. This refines the authored quadrilateral patches; it does not currently resize the physical shape or infer a topology from an imported mesh. The editor lab exposes subdivision values from 2 to 8.

The logical system currently uses **four-connected, quadrilateral cells**. Triangular logical cells, unsupported/non-manifold meshes, and general open-surface traversal are outside the current implementation.

## Getting started

### Requirements

- **Unity Editor 6000.6.7f1** (the version recorded in `ProjectSettings/ProjectVersion.txt`)
- Unity Hub, or a compatible Unity Editor installation
- Git (to clone the project)

The project includes the Unity Input System, UGUI, Universal Render Pipeline, and Unity Test Framework in its package manifest.

### Run the game

1. Clone the repository:

   ```bash
   git clone https://github.com/Paradrake2/ArrowCube.git
   ```

2. Open the repository folder as a project in Unity Hub, using Unity **6000.6.7f1**.
3. Allow Unity to import assets and packages.
4. Open a scene and press **Play**:

   | Scene | Purpose |
   | --- | --- |
   | `Assets/Scenes/Game.unity` | Main game and campaign |
   | `Assets/Scenes/TestScene.unity` | Cube-generation settings and testing lab |
   | `Assets/Scenes/SurfaceGeneration.unity` | Procedural L, T, and oblique-prism puzzles |
   | `Assets/Scenes/SurfaceL.unity` | Manually authored L-shaped puzzle demo |
   | `Assets/Scenes/SurfacePrism.unity` | Manually authored oblique-prism puzzle demo |

For the procedural surface lab, you can also use **Arrow Cube > Phase 4 > Open Procedural Surface Lab** from the Unity menu. Expand **SURFACE LAB / Settings** during Play mode to select the shape, seed, exit rules, subdivisions, coverage, and blocker controls, then generate a new puzzle.

## Controls

| Input | Action |
| --- | --- |
| Left click / tap an arrow | Attempt removal |
| Right mouse drag | Orbit the camera |
| Left drag from empty space | Orbit the camera |
| Mouse wheel / two-finger pinch | Zoom |
| One-finger drag | Orbit on touch devices |
| `R` | Reset camera |
| `H` | Hint: highlight a currently removable arrow |
| `Z` | Undo the most recently removed arrow |
| `Esc` | Pause / cancel a relevant active control |
| `F11` | Toggle fullscreen in a supported desktop player |

Additional buttons in the HUD provide restart, hammer, settings, and other gameplay controls. Controls and allowances can be customized in Settings.

## How it works

The project keeps **puzzle logic independent of Unity geometry and physics**:

```text
Shape definition / authored quadrilateral patches
                    |
                    v
   Surface topology (cells + directed boundaries)
                    |
                    v
   Arrow paths + occupancy + exit rules
                    |
                    v
  Movement simulator <--> Puzzle generator
                    |                 |
                    +---- Validator --+
                    |
                    v
  Geometry / arrow rendering / animation
                    |
                    v
          Input + gameplay + saves
```

- **Topology:** An immutable graph of cell IDs and four directional boundaries. Each connection defines its destination, transported heading, geometric-fold status, and edge-exit eligibility.
- **Movement:** A deterministic, discrete snake-style simulator handles collisions, route sweeps, gates, and complete removal. Tail cells vacate as the head moves.
- **Generation:** Candidate selection and dependency analysis work against generic cell IDs and simulated removal trajectories.
- **Geometry:** A separate quad-surface representation provides cell positions, normals, edge geometry, and folded arrow paths.
- **Presentation:** Surface ribbons and arrowheads are rendered using opaque materials and depth testing; picking and camera controls work with the playable shapes.
- **Persistence:** Versioned recipes, topology identity, and local saves preserve progress and enable deterministic replay.

### Main source directories

```text
Assets/
  Scenes/              Gameplay and demo scenes
  Scripts/
    Topology/           Cell graphs, boundaries, cube adapter, arrow paths
    Movement/           Simulator, occupancy, exit gates, puzzle definitions
    Generation/         Procedural generation, validators, dependencies, difficulty
    Presentation/       Surface geometry, meshes, ribbons, animations, materials
    Controls/           Camera, picking, mouse/touch input
    Gameplay/           Engine-independent session, campaign, and save models
    Game/               Unity gameplay controllers, HUD, surface labs
  Tests/
    EditMode/           Core logic and generation tests
    PlayMode/           Gameplay, scene, rendering, and input integration tests
Documentation/         Design notes, architecture, setup, and verification
TestResults/           Recorded validation and regression artifacts
Tools/                 Unity test scripts and generation benchmarks
```

## Tests and verification

The repository includes EditMode, PlayMode, integration, generation, and regression tests. On Windows, run the provided test runner from the project root:

```powershell
.\Tools\RunUnityTests.ps1 -TestPlatform All
```

The runner uses an isolated validation project so it does not need to take over your open Unity scene. It can also be configured with an explicit `-UnityPath` if the editor is installed elsewhere.

The documented **Phase 4 verification run** reported **352 EditMode and 53 PlayMode tests passed**, along with a matching historical corpus of **1,008 cube-generation fingerprints**. These are recorded results, not a claim that tests were rerun for this README; see [`TestResults/SurfaceGeneration/Summary.md`](TestResults/SurfaceGeneration/Summary.md).

## Documentation

- [Shape-independent procedural generation (Phase 4)](Documentation/ArbitrarySurface-Phase4.md)
- [Generic surface geometry and rendering (Phase 3)](Documentation/ArbitrarySurface-Phase3.md)
- [Generic puzzle model and movement (Phase 2)](Documentation/ArbitrarySurface-Phase2.md)
- [Surface topology architecture](Documentation/SurfaceTopology.md)
- [Core movement rules](Documentation/Stage2-CoreMovement.md)
- [Advanced procedural difficulty](Documentation/Advanced-Procedural-Difficulty.md)
- [Multi-blocker dependencies](Documentation/MultiBlocker-Dependencies.md)
- [Gameplay, saves, and progression](Documentation/Stage5-GameplaySystems.md)
- [Web-player generation](Documentation/Web-Generation.md)
- [Web rendering and material fixes](Documentation/Web-Rendering.md)

## Development status and next steps

ArrowCube is under active development. The current repository supports procedural puzzles on several **authored non-cube surfaces** and retains the original cube campaign.

A major planned direction is **automatic mesh-to-topology conversion**: taking a suitable Unity/Blender mesh, reconstructing or supplying quadrilateral patches, subdividing them into logical cells, determining adjacency and fold classifications, and then generating a puzzle using the existing pipeline. Configurable target cell size and compatible subdivision across neighboring patches are important design goals for that work.

This feature is a goal, **not a completed importer** in the version of the repository used for this README.

---

*ArrowCube is an experimental game and engineering project exploring procedural puzzles on connected 3D surfaces.*
