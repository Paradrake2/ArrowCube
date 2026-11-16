# Web build: creating-puzzle fix

For arrows appearing/disappearing while orbiting in the player, see
[Web rendering and opaque materials](Web-Rendering.md).

The game and settings lab previously started generation through `Task.Run`.
Unity 6000.6 Web players do not support managed C# worker threads. A queued
generation task could therefore remain incomplete while the loading menu waited.
This is an application scheduling issue; enabling native Web multithreading does
not provide a C# thread pool.

`GenerationOperation` now chooses the execution path automatically:

- Desktop/mobile native players keep the background worker.
- Web players advance `PuzzleGenerator.GenerateSteps` from the main-thread Update
  loop, with a **6 ms target budget per frame**. There is no web `Task.Run` call.
- Construction, tail filling, blocker tuning and measured-layout search expose
  cooperative work boundaries. UI rendering and cancellation run between chunks.
- Both scenes share this implementation. Errors complete the operation and display
  the existing rejection/error UI, preserving the previous playable puzzle.

The generator uses the same seeds, random-call order, difficulty controls and
validators. Versions 1 through 5 retain their fingerprint behavior. Browser
generation does not silently disable measured difficulty or relax blocker limits.
The frame budget is a scheduling target: an individual analysis/validation chunk
can exceed it, especially on large measured profiles.

The creating-puzzle panel now displays the current generation phase. Web saves
also request Unity's IndexedDB synchronization after a successful file write,
so they can persist with the default web template. Unity coalesces those requests.
Browser storage restrictions or clearing site data can still affect local saves.

## Rebuild and upload

1. Let Unity recompile these scripts.
2. Make a fresh **Web** build using the existing build profile, with `Game.unity`
   and `TestScene.unity` in the scene list.
3. Replace the itch.io HTML build with the new build's complete ZIP, including
   `index.html`, `Build` and `TemplateData`.
4. Reload the embedded game after uploading. If the browser retains the old
   build, use a hard reload or a private window to verify the new upload.

No extra thread/hosting setting is needed for this fix. This work does not publish
or replace the user's hosted build automatically.

## Verification

`GenerationOperationTests` checks single-thread yielding, cancellation, explicit
errors and fingerprint/solution equality against synchronous generation, across
all exit modes with measured difficulty on and off. `WebGenerationIntegrationTests`
forces the same backend in the Editor for both gameplay and lab scene controllers,
including cancellation, replacement requests and failed generation.

An isolated Unity WebGL development build compiles the actual `UNITY_WEBGL`
player branch, including the native IndexedDB sync symbol. `Assets/Editor/WebGenerationBuild.cs`
is its build entry point and is intended for an isolated validation project.
The compile log is kept in `TestResults/WebGeneration/web-build.log`.
Final verification passed **273 EditMode and 38 PlayMode tests**, including the
1,008-puzzle stress suite, legacy fingerprints and both single-thread scene paths.
Results are in `TestResults/WebGeneration/UnityFinal`.
Live browser/itch.io testing was unavailable because the browser access host was
not connected in this session.

References: [Unity 6.6 web threading limitations](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/webgl/intro/technical-overview),
[Unity web persistence configuration](https://docs.unity.com/en-us/engine/6000.0/manual/platform-specific/webgl/building-distribution/templates/web-templates-build-configuration).
