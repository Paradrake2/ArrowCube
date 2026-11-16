using System;
using System.Collections;
using System.IO;
using ArrowCube.Controls;
using ArrowCube.Game;
using ArrowCube.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace ArrowCube.Tests
{
    public class GameplaySceneTests
    {
        private string folder;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/Stage5SceneTests",Guid.NewGuid().ToString("N")));
            SceneManager.sceneLoaded+=Loaded; yield return null;
        }
        private void Loaded(Scene scene,LoadSceneMode mode)
        {
            foreach(var root in scene.GetRootGameObjects())
            {
                var game=root.GetComponent<GameplayController>(); if(game!=null) game.SavePathOverride=Path.Combine(folder,"scene-player.json");
            }
        }
        [UnityTearDown]
        public IEnumerator Teardown()
        {
            SceneManager.sceneLoaded-=Loaded;
            GameSceneLaunch.ReturningToLab=false; GameSceneLaunch.LabJson=null; GameSceneLaunch.Take();
            GameSceneLaunch.LabInvertMouseRotation=false; GameSceneLaunch.LabInvertTouchRotation=false;
            var cleanup=SceneManager.CreateScene("Stage 5 test cleanup "+Guid.NewGuid().ToString("N")); SceneManager.SetActiveScene(cleanup);
            for(int i=SceneManager.sceneCount-1;i>=0;i--)
            {
                var scene=SceneManager.GetSceneAt(i);
                if(scene.name=="Game" || scene.name=="TestScene") yield return SceneManager.UnloadSceneAsync(scene);
            }
            if(Directory.Exists(folder)) Directory.Delete(folder,true);
        }
        [UnityTest]
        public IEnumerator GameplaySceneCanGenerateSaveAndReloadAnExactPartiallyClearedLevel()
        {
            yield return SceneManager.LoadSceneAsync("Game",LoadSceneMode.Additive); yield return null; yield return null;
            var game=Object.FindFirstObjectByType<GameplayController>(); Assert.That(game, Is.Not.Null);
            Assert.That(game.Page, Is.EqualTo(GamePage.Menu)); Assert.That(Object.FindFirstObjectByType<Stage4ControlsDemo>(), Is.Null);
            Assert.That(game.StartLevel(1), Is.True); float deadline=Time.realtimeSinceStartup+30;
            while(game.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(game.Page, Is.EqualTo(GamePage.Game),game.Message);
            string fingerprint=game.Fingerprint; int id=game.Session.Puzzle.Paths[0].Id;
            game.ArmHammer(); game.ActivatePath(id); int remaining=game.Session.Grid.Paths.Count; game.Save();
            yield return SceneManager.UnloadSceneAsync("Game");
            yield return SceneManager.LoadSceneAsync("Game",LoadSceneMode.Additive); yield return null; yield return null;
            game=Object.FindFirstObjectByType<GameplayController>(); Assert.That(game.Continue(), Is.True); yield return null;
            Assert.That(game.Fingerprint, Is.EqualTo(fingerprint)); Assert.That(game.Session.Grid.Paths.Count, Is.EqualTo(remaining));
            Assert.That(game.Session.Grid.Paths.ContainsKey(id), Is.False); Assert.That(game.Undo(), Is.True);
            Assert.That(game.Session.Grid.Paths.ContainsKey(id), Is.True);
        }
        [UnityTest]
        public IEnumerator SettingsLabLaunchesItsExactPuzzleAndKeepsGenerationSettingsOnReturn()
        {
            var profile=ScriptableObject.CreateInstance<DifficultyProfileAsset>();
            profile.gridSize=6; profile.minPathCount=8; profile.minCrossFacePaths=5; profile.minimumCoverage=.4f; profile.targetCoverage=.65f;
            profile.minimumBlockedFraction=.05f; profile.minimumAverageTurns=.4f; profile.advancedDifficulty=false; profile.dependencyAmount=.82f;
            GameSceneLaunch.LabJson=JsonUtility.ToJson(profile); GameSceneLaunch.LabSeed=17;
            GameSceneLaunch.LabInvertMouseRotation=true; GameSceneLaunch.LabInvertTouchRotation=false;
            GameSceneLaunch.LabEdges=true; GameSceneLaunch.LabGates=false; GameSceneLaunch.ReturningToLab=true; Object.Destroy(profile);
            yield return SceneManager.LoadSceneAsync("TestScene",LoadSceneMode.Additive); yield return null;
            var lab=Object.FindFirstObjectByType<Stage4ControlsDemo>(); Assert.That(lab, Is.Not.Null);
            float deadline=Time.realtimeSinceStartup+30;
            while(lab.Generation.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(lab.Generation.Generated, Is.Not.Null,lab.Generation.StatusMessage);
            Assert.That(lab.GetComponentInChildren<ScrollRect>().scrollSensitivity, Is.EqualTo(6));
            Assert.That(lab.Input.InvertMouseRotation, Is.True); Assert.That(lab.Input.InvertTouchRotation, Is.False);
            lab.Input.InvertMouseRotation=false; lab.Input.InvertTouchRotation=true;
            string fingerprint=lab.Generation.Generated.Fingerprint;
            Button play=null; foreach(var button in lab.GetComponentsInChildren<Button>(true)) if(button.name=="Play this puzzle in game scene") play=button;
            Assert.That(play, Is.Not.Null); play.onClick.Invoke(); yield return null; yield return null;
            var game=Object.FindFirstObjectByType<GameplayController>(); Assert.That(game, Is.Not.Null);
            Assert.That(game.IsSandbox, Is.True); Assert.That(game.Fingerprint, Is.EqualTo(fingerprint));
            Assert.That(game.Input.InvertMouseRotation, Is.False); Assert.That(game.Input.InvertTouchRotation, Is.True);
            Assert.That(game.Player.Active, Is.Null); Assert.That(game.Player.Progress.HighestUnlocked, Is.EqualTo(1));
            game.ReturnToLab(); yield return null; yield return null;
            lab=Object.FindFirstObjectByType<Stage4ControlsDemo>(); Assert.That(lab, Is.Not.Null);
            Assert.That(lab.Generation.EditingProfile.dependencyAmount, Is.EqualTo(.82f).Within(.00001));
            Assert.That(lab.Generation.EditingProfile.advancedDifficulty, Is.False);
            Assert.That(lab.Generation.Seed, Is.EqualTo(17));
            Assert.That(lab.Input.InvertMouseRotation, Is.False); Assert.That(lab.Input.InvertTouchRotation, Is.True);
        }
    }
}
