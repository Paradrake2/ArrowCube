using System.Collections;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArrowCube.Tests
{
    public class GenerationDemoTests
    {
        private GameObject root;
        private Stage3GenerationDemo demo;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root=new GameObject("Generation demo test"); demo=root.AddComponent<Stage3GenerationDemo>();
            yield return null;
            float deadline=Time.realtimeSinceStartup+60;
            while(demo.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(demo.Generated, Is.Not.Null,demo.StatusMessage);
            demo.Movement.AnimationCellsPerSecond=2000;
        }
        [UnityTearDown]
        public IEnumerator TearDown() { Object.Destroy(root); yield return null; }

        [UnityTest]
        public IEnumerator RuntimeBlockerControlsWorkWithMeasuredSearchDisabledAndKeepTheLevelOnFailure()
        {
            var asset=ScriptableObject.CreateInstance<DifficultyProfileAsset>();
            try
            {
                asset.advancedDifficulty=false; demo.CustomProfile=asset;
                root.AddComponent<ArrowCube.Controls.Stage4ControlsDemo>(); yield return null;
                UnityEngine.UI.Toggle dependency=null,measured=null;
                foreach(var toggle in root.GetComponentsInChildren<UnityEngine.UI.Toggle>(true))
                {
                    if(toggle.name=="Control blockers / few starting moves") dependency=toggle;
                    if(toggle.name=="Use measured difficulty search") measured=toggle;
                }
                Assert.That(dependency, Is.Not.Null); Assert.That(measured, Is.Not.Null);
                dependency.isOn=true;
                foreach(var slider in root.GetComponentsInChildren<UnityEngine.UI.Slider>(true))
                {
                    if(slider.name=="Dependency amount") slider.value=.9f;
                    if(slider.name=="Maximum starting free arrows") slider.value=2;
                }
                Assert.That(demo.EditingProfile.controlDependencies, Is.True); Assert.That(measured.isOn, Is.False);
                Assert.That(demo.EditingProfile.dependencyAmount, Is.EqualTo(.9f).Within(.000001));
                Assert.That(demo.Generate(1,1,ExitRules.EdgeOnly), Is.True,demo.StatusMessage);
                var puzzle=demo.Generated;
                Assert.That(puzzle.Difficulty, Is.Null); Assert.That(puzzle.Recipe.DependencyControls.MaximumStartingFreeArrows, Is.EqualTo(2));
                Assert.That(puzzle.Statistics.ImmediatelyRemovablePaths, Is.InRange(1,2));
                Assert.That(new PuzzleValidator().Validate(puzzle,puzzle.VerifiedSolution,requireClearInitialExit:true).Succeeded, Is.True);
                demo.EditingProfile.minPathCount=1000;
                Assert.That(demo.Generate(1,1), Is.False); Assert.That(demo.Generated, Is.SameAs(puzzle));
                demo.Restart(); Assert.That(demo.Movement.Grid.Paths.Count, Is.EqualTo(puzzle.Paths.Count));
            }
            finally { Object.Destroy(asset); }
        }

        [UnityTest]
        public IEnumerator AllThreeModesPlayACompleteGeneratedSolutionThroughTheSharedMovementRenderer()
        {
            foreach(var rules in new[] { ExitRules.EdgeOnly,ExitRules.GateOnly,ExitRules.Hybrid })
            {
                Assert.That(demo.Generate(7,0,rules), Is.True);
                var generated=demo.Generated;
                Assert.That(demo.Movement.Puzzle, Is.SameAs(generated));
                Assert.That(demo.PlayVerifiedSolution(), Is.True);
                float deadline=Time.realtimeSinceStartup+10;
                while(demo.IsAutoPlaying && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.That(demo.IsAutoPlaying, Is.False);
                Assert.That(demo.Movement.Grid.Paths, Is.Empty);
                Assert.That(demo.Movement.Grid.OccupiedCellCount, Is.Zero);
                Assert.That(demo.Movement.Grid.OccupiedSegmentCount, Is.Zero);
                demo.Restart();
                Assert.That(demo.Movement.Grid.Paths.Count, Is.EqualTo(generated.Paths.Count));
                Assert.That(demo.Generated.Fingerprint, Is.EqualTo(generated.Fingerprint));
                Assert.That(demo.Generate(7,0,rules), Is.True);
                Assert.That(demo.Generated.Fingerprint, Is.EqualTo(generated.Fingerprint));
            }
        }

        [UnityTest]
        public IEnumerator FailedStartupNeverLoadsTheManualCubeAndCanRecoverWithALargeCustomProfile()
        {
            Object.Destroy(root); yield return null;
            var asset=ScriptableObject.CreateInstance<DifficultyProfileAsset>();
            try
            {
                // Valid recipe, impossible quality floor: exercise the generator's actual failure path.
                asset.gridSize=4; asset.minPathCount=1000;
                root=new GameObject("Failed generation startup"); root.SetActive(false);
                var controls=root.AddComponent<ArrowCube.Controls.Stage4ControlsDemo>();
                demo=root.GetComponent<Stage3GenerationDemo>(); demo.CustomProfile=asset;
                root.SetActive(true); yield return null;
                float deadline=Time.realtimeSinceStartup+60;
                while(demo.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.That(demo.Generated, Is.Null);
                Assert.That(demo.Movement.Puzzle, Is.Null,"A failure must not display the four-arrow manual puzzle.");
                Assert.That(demo.StatusMessage, Does.Contain("Generation rejected"));
                string error=demo.StatusMessage;
                demo.Restart(); Assert.That(demo.StatusMessage, Is.EqualTo(error));

                asset.gridSize=30; asset.minPathCount=14;
                Assert.That(demo.Generate(1,1,ExitRules.Hybrid), Is.True,demo.StatusMessage);
                yield return null;
                Assert.That(demo.Movement.Puzzle, Is.SameAs(demo.Generated));
                Assert.That(demo.Generated.Topology.GridSize, Is.EqualTo(30));
                Assert.That(demo.Movement.Grid.Paths.Count, Is.GreaterThan(100));
                Assert.That(controls.Input.Orbit.View, Is.SameAs(demo.Movement.InspectionCamera));
                Assert.That(root.GetComponentsInChildren<SurfacePathRenderer>().Length, Is.EqualTo(demo.Generated.Paths.Count));
            }
            finally { Object.Destroy(asset); }
        }

        [UnityTest]
        public IEnumerator BackgroundCancellationAndFailureKeepTheCurrentLevelAndNewerRequestsWin()
        {
            var original=demo.Generated; var oldGrid=demo.Movement.Grid;
            Assert.That(demo.BeginGeneration(123,4), Is.True); demo.CancelGeneration();
            yield return null;
            Assert.That(demo.IsGenerating, Is.False); Assert.That(demo.Generated, Is.SameAs(original));
            Assert.That(demo.Movement.Grid, Is.SameAs(oldGrid));
            Assert.That(demo.BeginGeneration(124,3), Is.True);
            Assert.That(demo.BeginGeneration(125,0), Is.True);
            float deadline=Time.realtimeSinceStartup+60;
            while(demo.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(demo.Generated.Recipe.Seed, Is.EqualTo(125));
            Assert.That(demo.Generated.Difficulty, Is.Not.Null);
            Assert.That(demo.Movement.DebugDifficulty, Is.False); Assert.That(demo.Movement.DebugDependencies, Is.False);
            var current=demo.Generated;
            var asset=ScriptableObject.CreateInstance<DifficultyProfileAsset>();
            try
            {
                asset.gridSize=4; asset.minPathCount=1000;
                asset.difficultySettings.MaxCandidates=1; asset.difficultySettings.MaxRestarts=1;
                demo.CustomProfile=asset;
                Assert.That(demo.BeginGeneration(126,0), Is.True);
                deadline=Time.realtimeSinceStartup+60;
                while(demo.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.That(demo.GenerationFailed, Is.True); Assert.That(demo.StatusMessage, Does.Contain("Generation rejected"));
                Assert.That(demo.Generated, Is.SameAs(current));
                string path=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../TestResults/advanced-report.json"));
                Assert.That(demo.ExportDifficultyReport(path), Is.EqualTo(path));
                var exported=JsonUtility.FromJson<PuzzleDifficultyReport>(System.IO.File.ReadAllText(path));
                Assert.That(exported.MeasuredScore, Is.EqualTo(current.Difficulty.MeasuredScore).Within(1e-10));
                Assert.That(exported.Solvers.Length, Is.EqualTo(4)); Assert.That(exported.Weights.Dependencies, Is.EqualTo(25));
                Assert.That(exported.MultiBlocker, Is.Not.Null); Assert.That(exported.DependencyNodes.Length, Is.EqualTo(current.Paths.Count));
                string dot=System.IO.Path.ChangeExtension(path,"dot"); demo.ExportDependencyGraph(dot);
                Assert.That(System.IO.File.ReadAllText(dot), Does.Contain("digraph ArrowCube"));
            }
            finally { Object.Destroy(asset); }
        }

        [UnityTest]
        public IEnumerator RegenerationCancelsAnOldAttemptAndRejectedRulesKeepTheCurrentLevel()
        {
            var original=demo.Generated; var oldGrid=demo.Movement.Grid;
            demo.Movement.AnimationCellsPerSecond=1;
            Assert.That(demo.PlayNextVerifiedMove(), Is.True);
            yield return null;
            Assert.That(demo.Generate(99,0,ExitRules.GateOnly), Is.True);
            Assert.That(demo.Movement.IsAnimating, Is.False);
            Assert.That(oldGrid.Paths.Count, Is.EqualTo(original.Paths.Count));
            var current=demo.Generated; var grid=demo.Movement.Grid;
            Assert.That(demo.Generate(100,0,ExitRules.None), Is.False);
            Assert.That(demo.Generated, Is.SameAs(current));
            Assert.That(demo.Movement.Grid, Is.SameAs(grid));
            Assert.That(demo.Generated.Rules.EnableCubeEdgeExits, Is.False);
            Assert.That(demo.Generated.Rules.EnableDirectionalGates, Is.True);
            yield return null;
        }
    }
}
