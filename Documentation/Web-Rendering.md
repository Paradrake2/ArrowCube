# Web player: arrows disappearing during camera rotation

The previous material factory searched by name for URP Unlit, then Unlit/Color,
then Sprites/Default. Name-only references can be stripped from a player build.
That made a transparent sprite shader a possible fallback for both the solid cube
and its arrows. Transparent renderers are sorted as whole objects, so a cross-face
arrow's bounds can place its visible front section before the cube. The cube then
covers it. Moving the camera changes that order and makes the section reappear.
An arrowhead and its body can also sort differently because they are separate
renderers. Camera rotation itself does not remove paths or change occupancy.

`PuzzleMaterials.Create` now loads **ArrowCubeOpaque.shader** explicitly from
Resources. The shader is included in player builds and has:

- Opaque Geometry queue, blending disabled, depth writes enabled and normal depth
  testing. The cube occludes rear-face arrows while visible front sections remain.
- A URP pass with full-precision vertex positions and a compatible material buffer.
- A Built-In pipeline pass for the isolated validation projects and compatibility.
  The URP package requirement prevents missing-include errors in non-URP projects.
- No transparent sprite fallback.

The cube, arrow ribbons, arrowheads and gate markers all use these materials.
This changes rendering only; saved layouts, generation, movement, thickness and
picking rules stay the same.

The cube now has six distinct light face colors through `ColoredCubeSurface`.
Each face uses this unlit opaque shader, with shadow reception disabled. Back and
bottom faces remain just as readable as the front, regardless of scene lights.
`CubeFeedbackVisualCapture.Capture` compares all six faces with lighting disabled
and with a strong shadow-casting directional light. It also captures opposite
corner views to inspect the boundaries and arrow contrast. It uses Mobile URP
when the project's `Assets/Settings` and URP package are supplied to the isolated
validation project; otherwise it checks the Built-In pipeline.

## Verification

`PuzzleMaterialTests` verifies resource inclusion, shader compilation, opaque queue
and color changes. `PuzzleRenderingTests` rotates through all six faces and checks
that occupancy, path counts, mesh vertices and opaque materials remain unchanged.
Movement, input and gameplay checks are included in the focused Unity run.
The run passed **23 EditMode + 32 PlayMode tests**; results are in
`TestResults/OpaqueRendering/Unity`.

`OpaqueSurfaceVisualCapture.Capture` performs an actual GPU check with a 16-bit
depth target. The old sprite fallback hid a visible front ribbon at two of three
camera angles; the fixed material rendered it at all three. The fixed shader also
passed the same check with the project's **Mobile_RPAsset / Mobile_Renderer**.
Pixel samples and before/after images are in `TestResults/OpaqueRendering/GPU`.

`OpaqueSurfaceVisualCapture.BuildMobileWeb` compiles a WebGL verification build with
that Mobile URP asset in an isolated project. Both Editor entry points are intended
for isolated validation projects, not the user's open authoring scene.
That WebGL build succeeded and its player data contains `ArrowCube/OpaqueSurface`.
The build log is in `TestResults/OpaqueRendering/web-build.log`.

## Applying the fix to itch.io

Make a fresh Web build, replace the uploaded build ZIP, and hard-refresh the embedded
game. The new shader is packaged automatically through Resources. There is no need
to change the level settings or regenerate an existing saved puzzle.

The Mobile RP asset separately uses 80% render scale and no MSAA. Those settings can
make edges look pixelated, but they do not account for whole front-facing arrow
sections disappearing with camera rotation.

Reference: [Unity Shader.Find and player shader inclusion](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/shader/find),
[Unity depth-write command](https://docs.unity.com/en-us/engine/6000.0/manual/materials-and-shaders/shaders/reference/sl-reference/sl-commands/sl-zwrite).
