using System;
using System.Collections;
using System.IO;
using ArrowCube.Controls;
using ArrowCube.Game;
using ArrowCube.Gameplay;
using ArrowCube.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace ArrowCube.Tests
{
    public class GameplayIntegrationTests
    {
        private GameObject root;
        private GameplayController game;
        private string folder;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/Stage5Integration",Guid.NewGuid().ToString("N")));
            root=new GameObject("Stage 5 test"); root.SetActive(false); game=root.AddComponent<GameplayController>();
            game.SavePathOverride=Path.Combine(folder,"player.json"); root.SetActive(true); yield return null; yield return null;
            game.LoadPuzzle(ManualPuzzles.Create(2)); game.Movement.AnimationCellsPerSecond=4000;
        }
        [UnityTearDown]
        public IEnumerator Teardown()
        {
            Object.Destroy(root); yield return null;
            if(Directory.Exists(folder)) Directory.Delete(folder,true);
        }
        private IEnumerator Idle()
        {
            float deadline=Time.realtimeSinceStartup+5;
            while(game.Movement.IsAnimating && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(game.Movement.IsAnimating, Is.False);
        }
        [UnityTest]
        public IEnumerator RealAnimationsDeductHeartsOnceAndVictoryOccursOnlyAfterTheFinalRemovalCommits()
        {
            var options=game.Player.Options.Copy(); options.UnlimitedAttempts=false; options.StartingHearts=2; game.ApplyOptions(options); game.Restart();
            Assert.That(game.Movement.Grid, Is.SameAs(game.Session.Grid));
            Assert.That(game.ActivatePath(10), Is.True); Assert.That(game.Session.Hearts, Is.EqualTo(2)); yield return Idle();
            Assert.That(game.Session.Hearts, Is.EqualTo(1)); Assert.That(game.Movement.Grid.Paths.Count, Is.EqualTo(2));
            Assert.That(game.ActivatePath(20), Is.True); yield return Idle(); Assert.That(game.Page, Is.EqualTo(GamePage.Game));
            game.Input.Orbit.FocusFace(game.Session.Puzzle.Topology,ArrowCube.Topology.CubeFace.Front);
            for(int i=0;i<20;i++) game.Input.Orbit.Tick(.1f);
            var priorRotation=game.Input.Orbit.Rotation; float priorZoom=game.Input.Orbit.Zoom;
            Assert.That(game.ActivatePath(10), Is.True); Assert.That(game.Page, Is.EqualTo(GamePage.Game)); yield return Idle(); yield return null;
            Assert.That(game.Page, Is.EqualTo(GamePage.Victory)); Assert.That(game.Session.Grid.Paths, Is.Empty);
            Assert.That(game.ActivatePath(10), Is.False); Assert.That(game.Player.Progress.HighestUnlocked, Is.EqualTo(1),"Lab tests never advance the campaign.");
            Assert.That(game.Undo(), Is.True); Assert.That(game.Page, Is.EqualTo(GamePage.Game)); Assert.That(game.Session.Grid.Paths.Count, Is.EqualTo(1));
            Assert.That(Quaternion.Angle(priorRotation,game.Input.Orbit.Rotation), Is.LessThan(.01));
            Assert.That(game.Input.Orbit.Zoom, Is.EqualTo(priorZoom).Within(.001));
        }
        [UnityTest]
        public IEnumerator FailedOverlayBlocksInputAndUnlimitedToggleResumesTheSamePartiallyClearedPuzzle()
        {
            var options=game.Player.Options.Copy(); options.UnlimitedAttempts=false; options.StartingHearts=1; game.ApplyOptions(options); game.Restart();
            game.ActivatePath(10); yield return Idle(); yield return null;
            Assert.That(game.Page, Is.EqualTo(GamePage.Failed)); Assert.That(game.Input.enabled, Is.False); Assert.That(game.Movement.TryActivate(20), Is.False);
            var grid=game.Session.Grid; game.OpenSettings(); yield return null;
            Toggle unlimited=null; foreach(var toggle in root.GetComponentsInChildren<Toggle>()) if(toggle.name=="Unlimited attempts") unlimited=toggle;
            Assert.That(unlimited, Is.Not.Null); unlimited.isOn=true; game.CloseSettings(); yield return null;
            Assert.That(game.Session.Grid, Is.SameAs(grid)); Assert.That(game.Session.Hearts, Is.Zero); Assert.That(game.Page, Is.EqualTo(GamePage.Game));
            foreach(var text in root.GetComponentsInChildren<Text>(true)) if(text.text.StartsWith("HEARTS")) Assert.That(text.gameObject.activeSelf, Is.False);
            Assert.That(game.ActivatePath(20), Is.True); yield return Idle();
        }
        [UnityTest]
        public IEnumerator PauseFreezesInFlightMovementAndRestartCancelsItsStaleCallback()
        {
            game.Movement.AnimationCellsPerSecond=1; game.ActivatePath(20); yield return null; game.Pause();
            Assert.That(game.Movement.AnimationPaused, Is.True); Assert.That(game.Input.enabled, Is.False);
            var revision=game.Session.Grid.Revision; double seconds=game.Session.Seconds;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(game.Session.Grid.Revision, Is.EqualTo(revision)); Assert.That(game.Session.Seconds, Is.EqualTo(seconds));
            var pose=game.Input.Orbit.Rotation; var zoom=game.Input.Orbit.Zoom;
            game.Resume(); Assert.That(Quaternion.Angle(pose,game.Input.Orbit.Rotation), Is.LessThan(.01));
            Assert.That(game.Input.Orbit.Zoom, Is.EqualTo(zoom).Within(.001)); game.Pause();
            game.Restart(); game.Movement.AnimationCellsPerSecond=4000; yield return null;
            Assert.That(game.Session.HasPendingAttempt, Is.False); Assert.That(game.Session.Grid.Paths.Count, Is.EqualTo(2));
            game.ActivatePath(20); yield return Idle(); Assert.That(game.Session.Grid.Paths.Count, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator HammerUndoHintAndReloadShareExactOccupancyAndKeepCameraPose()
        {
            var original=game.Session.Puzzle;
            Assert.That(game.Hint(), Is.True); Assert.That(game.Session.Grid.Paths.Count, Is.EqualTo(2));
            for(int i=0;i<20;i++) game.Input.Orbit.Tick(.1f);
            var rotation=game.Input.Orbit.Rotation; var zoom=game.Input.Orbit.Zoom;
            game.ArmHammer(); Assert.That(game.ActivatePath(10), Is.True); yield return null;
            Assert.That(game.Session.Grid.Paths.ContainsKey(10), Is.False); Assert.That(game.Movement.Grid, Is.SameAs(game.Session.Grid));
            Assert.That(Quaternion.Angle(rotation,game.Input.Orbit.Rotation), Is.LessThan(.01)); Assert.That(game.Input.Orbit.Zoom, Is.EqualTo(zoom).Within(.001));
            game.Undo(); Assert.That(game.Session.Grid.Paths.Count, Is.EqualTo(2)); Assert.That(game.Session.Puzzle, Is.SameAs(original));
            game.ArmHammer(); game.ActivatePath(10); game.Save();
            var saved=new LocalSaveStore(game.SaveFilePath+".sandbox").Load(out var warning);
            Assert.That(saved, Is.Not.Null,warning); var restored=saved.Active.Restore();
            Assert.That(restored.Grid.Paths.Count, Is.EqualTo(1)); Assert.That(restored.Undo(), Is.True); Assert.That(restored.Grid.Paths.Count, Is.EqualTo(2));
            Assert.That(game.Player.Active, Is.Null);
        }
        [UnityTest]
        public IEnumerator CampaignGenerationProgressionAndContinueDoNotUseTheManualTestCube()
        {
            game.Menu(); Assert.That(game.StartLevel(1), Is.True); float deadline=Time.realtimeSinceStartup+30;
            while(game.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(game.IsGenerating, Is.False); Assert.That(game.Page, Is.EqualTo(GamePage.Game),game.Message);
            Assert.That(game.IsSandbox, Is.False); Assert.That(game.Session.Puzzle.Paths.Count, Is.GreaterThan(4));
            string fingerprint=game.Fingerprint; var definition=game.Session.Puzzle;
            while(game.Session.Grid.Paths.Count>0)
            {
                int id=-1; foreach(int key in game.Session.Grid.Paths.Keys) { id=key; break; }
                game.ArmHammer(); Assert.That(game.ActivatePath(id), Is.True);
            }
            Assert.That(game.Page, Is.EqualTo(GamePage.Victory)); Assert.That(game.Player.Progress.HighestUnlocked, Is.EqualTo(2));
            Assert.That(game.Player.Progress.Find(1).BestStars, Is.EqualTo(1));
            Assert.That(game.Player.Progress.Find(1).Recipe.Version, Is.EqualTo(5));
            game.Restart(); Assert.That(game.Fingerprint, Is.EqualTo(fingerprint)); Assert.That(game.Session.Puzzle, Is.SameAs(definition));
            var changed=Object.Instantiate(game.Settings); var prior=game.Settings;
            changed.controlDependencies=false; changed.directionalGates=true; changed.cubeEdgeExits=false; game.Settings=changed;
            Assert.That(game.StartLevel(1), Is.True); deadline=Time.realtimeSinceStartup+30;
            while(game.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
            game.Settings=prior; Object.Destroy(changed);
            Assert.That(game.Fingerprint, Is.EqualTo(fingerprint),"Replay uses its archived recipe, including the original exits and dependency settings.");
            Assert.That(game.Session.Puzzle.Rules.EnableDirectionalGates, Is.False);
            game.Movement.AnimationCellsPerSecond=4000;
            game.ActivatePath(game.Session.Hint().Value);
            yield return Idle(); game.Save();
            var store=new LocalSaveStore(game.SaveFilePath); var player=store.Load(out var warning); Assert.That(player, Is.Not.Null,warning);
            Assert.That(player.Active.Fingerprint, Is.EqualTo(fingerprint)); Assert.That(player.Progress.HighestUnlocked, Is.EqualTo(2));
            Assert.That(player.Active.Restore().Grid.Paths.Count, Is.EqualTo(game.Session.Grid.Paths.Count));
            Assert.That(game.Session.Grid.Paths.Count, Is.LessThan(game.Session.Puzzle.Paths.Count));
        }
        [UnityTest]
        public IEnumerator KeyboardHintUndoAndPauseUseTheGameplayStateAndKeepTheCameraPose()
        {
            // Install the simulated Input System while the shared input component is
            // disabled, so its EnhancedTouch subscriptions belong to this test runtime.
            game.Input.enabled=false;
            var devices=new InputTestFixture(); devices.Setup(); var keyboard=InputSystem.AddDevice<Keyboard>(); game.Input.enabled=true;
            try
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.H)); yield return null; yield return null;
                Assert.That(game.Session.HintUses, Is.EqualTo(1));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
                game.ArmHammer(); game.ActivatePath(10); Assert.That(game.Session.Grid.Paths.ContainsKey(10), Is.False);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Z)); yield return null; yield return null;
                Assert.That(game.Session.Grid.Paths.ContainsKey(10), Is.True);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape)); yield return null; yield return null;
                Assert.That(game.Page, Is.EqualTo(GamePage.Pause));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState()); yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape)); yield return null; yield return null;
                Assert.That(game.Page, Is.EqualTo(GamePage.Game));
            }
            finally { game.Input.enabled=false; devices.TearDown(); }
        }

        [UnityTest]
        public IEnumerator InversionSettingsPersistAcrossGameReloadAndSandboxResume()
        {
            game.OpenSettings(); yield return null;
            Toggle mouse=null,touch=null;
            foreach(var toggle in root.GetComponentsInChildren<Toggle>())
            { if(toggle.name=="Invert mouse rotation") mouse=toggle; if(toggle.name=="Invert touch rotation") touch=toggle; }
            Assert.That(mouse, Is.Not.Null); Assert.That(touch, Is.Not.Null);
            Assert.That(mouse.isOn || touch.isOn, Is.False);
            touch.isOn=true;
            Assert.That(game.Input.InvertTouchRotation, Is.True); Assert.That(game.Input.InvertMouseRotation, Is.False);
            game.CloseSettings(); game.Save();
            string path=game.SaveFilePath;
            var saved=new LocalSaveStore(path).Load(out var warning);
            Assert.That(saved, Is.Not.Null,warning); Assert.That(saved.InvertTouchRotation, Is.True); Assert.That(saved.InvertMouseRotation, Is.False);
            Object.Destroy(root); yield return null;
            root=new GameObject("Reloaded rotation preferences"); root.SetActive(false); game=root.AddComponent<GameplayController>();
            game.SavePathOverride=path; root.SetActive(true); yield return null; yield return null;
            Assert.That(game.Input.InvertTouchRotation, Is.True); Assert.That(game.Input.InvertMouseRotation, Is.False);
            Assert.That(game.ContinueSandbox(), Is.True);
            Assert.That(game.Input.InvertTouchRotation, Is.True); Assert.That(game.Input.InvertMouseRotation, Is.False);
        }
        [UnityTest]
        public IEnumerator MenuScrollIsSixAndAllGameplaySettingsRemainAccessible()
        {
            game.OpenSettings(); yield return null;
            var scroll=root.GetComponentInChildren<ScrollRect>(); Assert.That(scroll.scrollSensitivity, Is.EqualTo(6));
            Assert.That(root.GetComponentInChildren<GameplayHUD>().PuzzleScreenRect.height, Is.GreaterThan(0));
            bool zoom=false,thickness=false;
            foreach(var slider in root.GetComponentsInChildren<Slider>()) { if(slider.name=="Zoom speed") zoom=true; if(slider.name=="Arrow thickness") thickness=true; }
            Assert.That(zoom && thickness, Is.True);
        }
    }
}
