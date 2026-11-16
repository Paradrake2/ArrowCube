# Stage 5: gameplay systems

Web builds now use cooperative generation instead of managed worker threads. See
[Web generation and rebuilding](Web-Generation.md) for the creating-puzzle fix.

## Two separate scenes

- **Assets/Scenes/TestScene.unity** keeps the existing settings lab, custom difficulty
  asset, generation controls, reports and camera tools. Menu scroll sensitivity is **6**.
- **Assets/Scenes/Game.unity** runs the game: menu, level selection, puzzle HUD,
  hearts, power-ups, pause, victory and failure. It is the first enabled build scene;
  the lab is the second.

Open either scene directly, or use **Arrow Cube > Stage 5 > Open Gameplay Scene** /
**Open Settings Lab**. One active puzzle controller belongs in each scene.
The existing lab scene and its tuned profile were kept.

In Play mode, generate a puzzle in the lab and select **Play this puzzle in game
scene**. This launches the **exact verified layout**, with its exits and arrow
thickness, as a lab test. Lab tests have separate checkpoints and do not unlock
campaign levels or replace the campaign checkpoint. **Open settings lab** returns
to the lab and restores its runtime generation settings. The game menu also offers
**Resume last lab test** when a sandbox checkpoint exists.

## Playing

Click/tap an arrow to attempt removal. Existing desktop/touch orbit, drag suppression,
wheel/pinch zoom, camera reset and fullscreen controls are shared with the lab.

- **Unlimited attempts** is enabled by default. Mistakes still animate and count
  toward the star rating; they do not consume hearts or end the game. Hearts are hidden.
- Disable unlimited attempts in **Settings** for standard mode. Starting hearts
  default to **3** and are configurable. A completed failed animation deducts exactly
  one heart. At zero hearts, moves stop and the failure panel offers restart,
  settings and return to menu.
- Changing attempt mode preserves all removals and the current heart count.
  Enabling unlimited attempts after failure lets you continue the same puzzle.
  Disabling it again with zero hearts restores the failure state.
- **Pause / Esc** suspends movement animation, puzzle input and the level timer.
  Resume continues an in-flight attempt. Restart cancels it and restores the original
  puzzle. Opening menus never changes global `Time.timeScale`.
- **Restart same puzzle** restores the original paths, gates and rules, replenishes
  configured hearts/allowances, and resets that run's mistakes, power-up use and timer.
  It does not generate a new seed or read a changed difficulty profile.

Hearts and per-level power-up allowance edits apply on the next restart or level.
Attempt-mode and unlimited-power-up switches apply immediately. Camera sensitivity,
zoom speed and arrow thickness also update immediately and are saved locally.

## Power-ups

- **Hint / H** asks the actual simulator for a currently removable arrow, highlights
  it green and turns the camera to its head's face. It never uses an outdated solution
  index or removes the arrow.
- **Hammer** arms selection; then click/tap an arrow to remove it regardless of
  blockers. Cancel with the button or Esc. A charge is spent only on a valid selection.
- **Undo / Z** restores the most recently removed arrow, including a hammered arrow,
  to its exact original cells and segments. Undoing a limited hammer removal refunds
  that hammer; undo itself consumes a limited undo charge. Camera pose is preserved.

Unlimited power-ups are enabled by default. Disable that switch for per-level
allowances, each defaulting to three. Failed/blocked actions and unavailable hints
do not spend charges. Undo restores occupancy, not past mistakes, elapsed time or
the cumulative power-up-use counts used for stars. Undo is also available on the
victory screen if an undo remains; already unlocked levels stay unlocked.

## Progression and replay

Level 1 starts unlocked. Clearing every path unlocks the next level and records the
best star rating, fewest mistakes and best completion time. Level selection permits
replay of unlocked levels. Completions are recorded once per completed run, including
after save/reload; restarting creates a fresh run.

- **3 stars:** no mistakes or power-ups used.
- **2 stars:** at most two mistakes and no hammer used.
- **1 star:** any other completed run.

`GameplaySettings.asset` in **Assets/Resources** configures the initial campaign seed,
default mode/hearts/allowances, independent exit switches and blocker controls.
Campaign generation uses basic packing by default, with dependency amount **0.75**
and at most **3** starting free arrows. Measured difficulty search is optional.
The five existing difficulty presets supply the progression bands. Assign an
optional **Profile Override** to test a particular difficulty asset in the campaign;
otherwise leave it empty. Lab launches are convenient for testing one exact layout.

New level seeds use `unchecked(baseSeed + level * 104729)`. The base seed is stored
in the player save. A completed level archives its complete original generation
recipe and fingerprint. Replay uses that archived recipe and verifies the resulting
fingerprint, so later asset edits do not silently replace a completed level. Future
unplayed levels use the currently configured profile. Existing generation versions
1 through 5 remain supported.

## Local saving

No network services, accounts or purchases are used. Files live in
`Application.persistentDataPath`:

- `arrow-cube-player.json`: preferences, progression, completed-level recipes and
  the active campaign checkpoint.
- `arrow-cube-player.json.sandbox`: the independent lab-test checkpoint.
- `.bak` siblings: last-good backups. Writes use a temporary file and replacement.

Each file is a versioned JSON envelope with a SHA-256 checksum. A backup is tried
when the main file is invalid; unreadable files are kept and a visible warning is
shown. A write failure leaves the current puzzle playable and reports that it could
not save. Settings sliders debounce disk writes; completed moves, power-ups, menu
changes, application suspension and exit save checkpoints.

The active checkpoint stores **all original geometry and exits**, the removal/hammer
history, remaining inventory and hearts, counters and generation recipe. Reload
reconstructs and validates the saved layout and replays its removal history; it does
not regenerate the active level. An interrupted animation reloads from the last
committed removal. Undo history remains available after reload.

Generation recipes include a lossless binary payload inside JSON. This retains
floating-point bits and explicit optional settings, which Unity JSON alone does not
preserve reliably enough for exact generation fingerprints. It is independent of
the puzzle snapshot, which also preserves manual lab fixtures.

## Implementation and verification

`ArrowCube.Gameplay` is engine-independent: `GameplaySession`, checkpoint DTOs,
recipe encoding and `CampaignProgress`. `ArrowCube.Game` supplies the Unity
controller, local file store, HUD, gameplay settings asset and lab bridge. The
existing renderer and session share one `SurfaceGrid`. Plans are reserved before
animation; hearts, history and victory update once after the renderer commits or
finishes a failed attempt. Stale/duplicate callbacks are rejected.

`GameplaySessionTests`, `GameplayIntegrationTests`, `GameplaySceneAssetTests` and
`GameplaySceneTests` cover mode changes, hearts, pending attempts, hints, hammer,
undo, stars, replay, corruption recovery, exact recipe/geometry reload, real scene
transitions and preservation of the settings lab. The normal movement, input,
generation and 1,008-puzzle stress tests remain part of the full suite.

Verification passed **264 EditMode + 35 PlayMode tests** in the full regression
run. Final camera-pose, backup-recovery and keyboard changes passed **22 focused
EditMode + 10 focused PlayMode tests**. Results are in `TestResults/Stage5/Final`
and `TestResults/Stage5/FinalGameplay`. Six rendered desktop/portrait menu, settings
and HUD captures were inspected in `TestResults/Stage5/Visual`.

```powershell
./Tools/RunUnityTests.ps1 -TestPlatform All -OutputDirectory ./Temp/Stage5Tests -ArtifactDirectory ./TestResults/Stage5/Final
```

Stage 5 supplies functional menus/HUD for these systems. Stage 6 remains responsible
for the fuller visual treatment, sound effects and presentation polish; platform
builds remain Stage 7.
