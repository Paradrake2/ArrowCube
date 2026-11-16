using System;
using System.Collections;
using System.IO;
using ArrowCube.Game;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ArrowCube.Tests
{
    public class WebGenerationIntegrationTests
    {
        [UnityTest]
        public IEnumerator GameLeavesCreatingPuzzleWithTheSingleThreadBackendAndCancellationKeepsThePreviousLevel()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/WebGeneration",Guid.NewGuid().ToString("N")));
            var root=new GameObject("Web game test"); root.SetActive(false);
            var game=root.AddComponent<GameplayController>(); game.GenerateOnMainThread=true; game.SavePathOverride=Path.Combine(folder,"player.json");
            root.SetActive(true); yield return null; yield return null;
            try
            {
                Assert.That(game.StartLevel(1), Is.True); Assert.That(game.Page, Is.EqualTo(GamePage.Generating));
                int frames=0; float deadline=Time.realtimeSinceStartup+30;
                while(game.IsGenerating && Time.realtimeSinceStartup<deadline) { frames++; yield return null; }
                Assert.That(frames, Is.GreaterThan(0)); Assert.That(game.Page, Is.EqualTo(GamePage.Game),game.Message);
                Assert.That(game.Session.Puzzle.Paths.Count, Is.GreaterThan(4));
                var original=game.Session; string fingerprint=game.Fingerprint;
                Assert.That(game.StartLevel(1), Is.True); game.CancelGeneration(); yield return null;
                Assert.That(game.IsGenerating, Is.False); Assert.That(game.Session, Is.SameAs(original)); Assert.That(game.Fingerprint, Is.EqualTo(fingerprint));
                game.Resume(); Assert.That(game.Page, Is.EqualTo(GamePage.Game));
            }
            finally { Object.Destroy(root); }
            yield return null;
            if(Directory.Exists(folder)) Directory.Delete(folder,true);
        }
        [UnityTest]
        public IEnumerator LabUsesTheSingleThreadBackendAndOnlyTheNewestRequestCanReplaceThePuzzle()
        {
            var root=new GameObject("Web lab test"); root.SetActive(false);
            var lab=root.AddComponent<Stage3GenerationDemo>(); lab.GenerateOnMainThread=true;
            var asset=ScriptableObject.CreateInstance<DifficultyProfileAsset>(); asset.ApplyPreset(0); asset.advancedDifficulty=false; asset.controlDependencies=true;
            lab.CustomProfile=asset; root.SetActive(true); yield return null;
            try
            {
                lab.BeginGeneration(14,0,ExitRules.EdgeOnly); lab.BeginGeneration(15,0,ExitRules.EdgeOnly);
                float deadline=Time.realtimeSinceStartup+30;
                while(lab.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.That(lab.Generated, Is.Not.Null,lab.StatusMessage); Assert.That(lab.Generated.Recipe.Seed, Is.EqualTo(15));
                var old=lab.Generated;
                asset.minPathCount=1000; asset.maxPathCount=30000; asset.gridSize=4;
                Assert.That(lab.BeginGeneration(99,0,ExitRules.EdgeOnly), Is.True);
                deadline=Time.realtimeSinceStartup+30;
                while(lab.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.That(lab.IsGenerating, Is.False); Assert.That(lab.GenerationFailed, Is.True); Assert.That(lab.Generated, Is.SameAs(old));
            }
            finally { Object.Destroy(root); Object.Destroy(asset); }
            yield return null;
        }
    }
}
