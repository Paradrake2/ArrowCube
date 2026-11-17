# Test Scene shape selection verification

Unity 6000.6.7f1, Windows, 2026-11-16.

- Full PlayMode regression suite: **62/62 passed**.
- Six new integration tests cover all eight built-in shape choices, shared profile values, starting-free limits, verified solutions, resolution changes, runtime picker buttons, measured generation snapshots, cancellation/failure, actual-area gate scaling and unchanged classic cube fingerprints.
- The actual delivered TestScene ? Game ? TestScene round trip retains the non-cube puzzle, desired shape, resolution and profile, and reproduces the same fingerprint.
- Existing converted-mesh, authored-surface, mouse/touch, renderer, controls, gameplay/save and generation-demo tests also pass.
- `git diff --check` passes.

The initial run had one UI-test timing failure: the settings label was checked before the next frame's refresh. The test now waits for the normal UI update; all tests pass. No generation algorithm was changed.

Usage: [Test Scene shape selection](../../Documentation/TestScene-ShapeSelection.md).
