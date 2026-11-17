# Test Scene: shape selection

Open `Assets/Scenes/TestScene.unity` and press Play. Near the top of the existing scrollable settings panel, click **Shape**, choose the desired surface, then click **Regenerate this seed** or **Next seed**.

The list includes the classic cube, authored L/T/oblique surfaces, and all converted assets under `Resources/ArrowCubeSurfaces` (including the four Phase 5 examples). Selecting a shape preserves the difficulty/profile edits and affects the next generation; it does not replace the current puzzle immediately.

| Surface | Density control |
|---|---|
| Classic cube | **Grid size** in Advanced settings, unchanged |
| Authored L, T, oblique prism | **Surface resolution** beneath the shape picker, 2?12 cells per parent quad side |
| Converted mesh | Baked density; the panel shows its cell count. Reconvert/rebake with Phase 5 tools to change density |

For Inspector configuration, use **Stage3GenerationDemo > Surface for next generation**: set **Desired Shape**, **Surface Subdivisions**, or choose **Converted Surface** and assign its asset. Assigned assets outside Resources are included in the runtime picker too.

All existing puzzle controls use the same generator and selected profile: seed, exit rules, path lengths/counts, coverage, turns, blocking requirements, starting-free limits, blocker amount, measured search, proposal/mutation budgets, targeting, distributions and score weights. Cross-face settings describe regions on non-cube surfaces. Optional gate-budget scaling uses actual surface area above 384 cells (the historical 8x8 cube reference). The cube keeps its original scaling and recipe behavior.

Constraints are not automatically weakened when a smaller shape cannot satisfy them. Failure/cancellation keeps the old puzzle and reports the problem. Increase authored resolution, rebake a converted asset, or explicitly change the conflicting settings.

Use **Focus next arrow** to inspect generic surfaces. Cube face buttons appear when the displayed puzzle is a cube. Debugging, solution playback and report export work with the generic cell-ID data.

**Play this puzzle in game scene** launches the generated surface through the normal game systems. Returning to the Test Scene retains the selected shape, density, converted asset and edited settings.

Changed implementation: `Stage3GenerationDemo`, `Stage4ControlsDemo`, `DifficultyProfileAsset` and `LabGameplayBridge`. Cube generation algorithms/recipes are unchanged. `LabShapeSelectionTests` exercises all eight built-in choices, settings preservation, authored resolution, measured/cancellable snapshots, errors, classic cube fingerprints and the actual TestScene ? Game ? TestScene round trip.
