using System;
using System.Collections;
using System.IO;
using System.Linq;
using ArrowCube.Controls;
using ArrowCube.Game;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using ArrowCube.Topology;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace ArrowCube.Tests
{
    public class LabShapeSelectionTests
    {
        private GameObject root;
        private Stage4ControlsDemo controls;
        private Stage3GenerationDemo lab;
        private DifficultyProfileAsset profile;
        public static DifficultyProfileAsset Profile()
        {
            var p=ScriptableObject.CreateInstance<DifficultyProfileAsset>();
            p.gridSize=6; p.minPathCount=4; p.maxPathCount=50; p.minCrossFacePaths=3; p.minPathLength=2; p.maxPathLength=12;
            p.minimumCoverage=.4f; p.targetCoverage=.65f; p.minimumBlockedFraction=.15f; p.minimumAverageTurns=.25f; p.turnProbability=.55f;
            p.maxGates=60; p.scaleGateBudgetWithGridSize=false; p.advancedDifficulty=false; p.controlDependencies=true; p.dependencyAmount=.5f; p.maximumStartingFreeArrows=3;
            return p;
        }
        [UnitySetUp] public IEnumerator Setup()
        {
            profile=Profile(); root=new GameObject("Lab shape selection test"); root.SetActive(false); controls=root.AddComponent<Stage4ControlsDemo>(); lab=controls.Generation??root.GetComponent<Stage3GenerationDemo>();
            lab.CustomProfile=profile; lab.DesiredShape=GenerationLabShape.L; lab.Seed=12345; lab.EnableCubeEdgeExits=false; lab.EnableDirectionalGates=true; lab.GenerateOnMainThread=true;
            root.SetActive(true); yield return null; yield return Wait();
        }
        private IEnumerator Wait()
        { float deadline=Time.realtimeSinceStartup+35; while(lab.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null; Assert.That(lab.IsGenerating,Is.False); Assert.That(lab.Generated,Is.Not.Null,lab.StatusMessage); Assert.That(lab.GenerationFailed,Is.False,lab.StatusMessage); }
        [UnityTearDown] public IEnumerator Cleanup() { if(root!=null) Object.Destroy(root); if(profile!=null) Object.Destroy(profile); yield return null; }
        [UnityTest] public IEnumerator AllAvailableShapesHonorTheSameCustomSettingsAndVerifiedSolution()
        {
            var editing=lab.EditingProfile; lab.UseEditedProfile(); Assert.That(lab.ShapeOptions.Count,Is.GreaterThanOrEqualTo(8));
            foreach(var option in lab.ShapeOptions.ToArray())
            {
                int index=lab.ShapeOptions.ToList().FindIndex(o=>o.Shape==option.Shape && o.Asset==option.Asset); lab.SelectShape(index);
                Assert.That(lab.Generate(12345,0,ExitRules.GateOnly),Is.True,lab.StatusMessage); yield return null;
                var puzzle=lab.Generated; Assert.That(lab.EditingProfile,Is.SameAs(editing));
                Assert.That(puzzle.Recipe.Profile.MinPathCount,Is.EqualTo(editing.minPathCount)); Assert.That(puzzle.Recipe.Profile.MaxPathCount,Is.EqualTo(editing.maxPathCount));
                Assert.That(puzzle.Recipe.Profile.MaxPathLength,Is.EqualTo(editing.maxPathLength)); Assert.That(puzzle.Recipe.Profile.TurnProbability,Is.EqualTo(editing.turnProbability).Within(1e-7));
                Assert.That(puzzle.Recipe.Profile.MinimumCoverage,Is.EqualTo(editing.minimumCoverage).Within(1e-7)); Assert.That(puzzle.Recipe.DependencyControls.MaximumStartingFreeArrows,Is.EqualTo(3));
                Assert.That(puzzle.Statistics.ImmediatelyRemovablePaths,Is.InRange(1,3)); Assert.That(puzzle.Recipe.Difficulty,Is.Null);
                Assert.That(new PuzzleValidator().Validate(puzzle,puzzle.VerifiedSolution).Succeeded,Is.True); Assert.That(lab.Movement.SurfaceGeometry.Surface,Is.SameAs(puzzle.Surface)); Assert.That(lab.FocusNextArrow(),Is.True);
                Assert.That(puzzle.Recipe.GenerationVersion,Is.EqualTo(option.Shape==GenerationLabShape.Cube ? 5 : 6));
                if(option.Shape==GenerationLabShape.ConvertedSurface) Assert.That(puzzle.Surface.CellCount,Is.EqualTo(option.Asset.Build().Shape.Topology.CellCount));
            }
        }
        [UnityTest] public IEnumerator RuntimeShapePickerAndResolutionKeepAdvancedEdits()
        {
            var edited=lab.EditingProfile; lab.UseEditedProfile(); edited.turnProbability=.73f;
            controls.GetComponentsInChildren<Button>(true).First(b=>b.name=="Choose shape").onClick.Invoke();
            controls.GetComponentsInChildren<Button>(true).First(b=>b.name=="Authored T").onClick.Invoke(); yield return null;
            Assert.That(lab.DesiredShape,Is.EqualTo(GenerationLabShape.T)); Assert.That(lab.EditingProfile,Is.SameAs(edited));
            var resolution=controls.GetComponentsInChildren<Slider>(true).First(s=>s.name=="Surface resolution"); resolution.value=5;
            Assert.That(lab.SurfaceSubdivisions,Is.EqualTo(5)); Assert.That(edited.turnProbability,Is.EqualTo(.73f)); Assert.That(edited.gridSize,Is.EqualTo(6));
            Assert.That(lab.Generate(12345,0,ExitRules.GateOnly),Is.True,lab.StatusMessage); Assert.That(lab.Generated.Surface.CellCount,Is.EqualTo(18*25));
            Assert.That(lab.Generated.Recipe.Profile.TurnProbability,Is.EqualTo(.73f).Within(1e-7)); yield return null; Assert.That(root.GetComponentsInChildren<Text>(true).Any(t=>t.text.Contains("450 cells")),Is.True);
        }
        [UnityTest] public IEnumerator MeasuredBackgroundGenerationSnapshotsItsShapeAndProfile()
        {
            var edited=lab.EditingProfile; lab.UseEditedProfile(); edited.advancedDifficulty=true; edited.difficultySettings.MaxCandidates=1; edited.difficultySettings.OptimizationIterations=0; edited.difficultySettings.MultiBlocker.Enabled=false;
            lab.DesiredShape=GenerationLabShape.ObliquePrism; Assert.That(lab.BeginGeneration(12345,0,ExitRules.GateOnly),Is.True);
            lab.DesiredShape=GenerationLabShape.T; edited.minimumCoverage=1;
            yield return Wait(); Assert.That(lab.Generated.Recipe.Shape.Id,Does.Contain("/prism/")); Assert.That(lab.DisplayedShapeName,Is.EqualTo("Authored oblique prism"));
            Assert.That(lab.Generated.Difficulty,Is.Not.Null); Assert.That(lab.Generated.Recipe.Profile.MinimumCoverage,Is.EqualTo(.4f).Within(1e-7)); Assert.That(lab.DesiredShape,Is.EqualTo(GenerationLabShape.T));
        }
        [UnityTest] public IEnumerator RejectedShapeAndCancellationKeepTheCurrentPuzzleAndCubeRecipesAreUnchanged()
        {
            var previous=lab.Generated; var grid=lab.Movement.Grid; lab.DesiredShape=GenerationLabShape.ConvertedSurface; lab.ConvertedSurface=null;
            Assert.That(lab.BeginGeneration(1,0),Is.False); Assert.That(lab.StatusMessage,Does.Contain("converted surface asset")); Assert.That(lab.Generated,Is.SameAs(previous)); Assert.That(lab.Movement.Grid,Is.SameAs(grid));
            lab.DesiredShape=GenerationLabShape.T; Assert.That(lab.BeginGeneration(12345,0,ExitRules.GateOnly),Is.True); lab.CancelGeneration(); yield return null; Assert.That(lab.Generated,Is.SameAs(previous));
            lab.DesiredShape=GenerationLabShape.Cube; lab.UseEditedProfile();
            Assert.That(lab.Generate(7,0,ExitRules.Hybrid),Is.True,lab.StatusMessage);
            var expected=new PuzzleGenerator().Generate(new PuzzleRecipe(7,lab.EditingProfile.ToProfile(),ExitRules.Hybrid,dependencyControls:lab.EditingProfile.ToDependencyControls()));
            Assert.That(lab.Generated.Fingerprint,Is.EqualTo(expected.Fingerprint)); Assert.That(lab.Generated.Topology.GridSize,Is.EqualTo(6));
        }
        [UnityTest] public IEnumerator GateScalingUsesActualSurfaceAreaAndImpossibleSettingsAreNotWeakened()
        {
            var edited=lab.EditingProfile; lab.UseEditedProfile(); edited.scaleGateBudgetWithGridSize=true; edited.gridSize=30;
            var converted=edited.ToSurfaceProfile(768); Assert.That(converted.MaxGates,Is.EqualTo(120));
            lab.DesiredShape=GenerationLabShape.L; Assert.That(lab.Generate(12345,0,ExitRules.GateOnly),Is.True,lab.StatusMessage); Assert.That(lab.Generated.Surface.CellCount,Is.EqualTo(224)); Assert.That(lab.Generated.Recipe.Profile.MaxGates,Is.EqualTo(60));
            var old=lab.Generated; edited.maxPathLength=300;
            Assert.That(lab.Generate(12345,0,ExitRules.GateOnly),Is.False); Assert.That(lab.Generated,Is.SameAs(old)); Assert.That(lab.StatusMessage,Does.Contain("224 cells")); yield return null;
        }
    }
    public class LabShapeSceneTests
    {
        private string folder;
        [UnitySetUp] public IEnumerator Setup()
        { folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/LabShapeScene",Guid.NewGuid().ToString("N"))); SceneManager.sceneLoaded+=Loaded; yield return null; }
        private void Loaded(Scene scene,LoadSceneMode mode)
        { foreach(var root in scene.GetRootGameObjects()) { var game=root.GetComponent<GameplayController>(); if(game!=null) game.SavePathOverride=Path.Combine(folder,"game.json"); var lab=root.GetComponent<Stage3GenerationDemo>(); if(lab!=null) lab.GenerateOnMainThread=true; } }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded-=Loaded; GameSceneLaunch.ReturningToLab=false; GameSceneLaunch.LabJson=null; GameSceneLaunch.LabShape=GenerationLabShape.Cube; GameSceneLaunch.LabSurfaceSubdivisions=4; GameSceneLaunch.LabConvertedSurface=null; GameSceneLaunch.Take();
            var empty=SceneManager.CreateScene("Lab shape cleanup "+Guid.NewGuid().ToString("N")); SceneManager.SetActiveScene(empty);
            for(int i=SceneManager.sceneCount-1;i>=0;i--) { var scene=SceneManager.GetSceneAt(i); if(scene.name=="Game" || scene.name=="TestScene") yield return SceneManager.UnloadSceneAsync(scene); }
        }
        [UnityTest] public IEnumerator DeliveredTestSceneRetainsNonCubeSelectionThroughTheGameSceneRoundTrip()
        {
            var profile=LabShapeSelectionTests.Profile(); GameSceneLaunch.LabJson=JsonUtility.ToJson(profile); Object.Destroy(profile);
            GameSceneLaunch.LabShape=GenerationLabShape.T; GameSceneLaunch.LabSurfaceSubdivisions=4; GameSceneLaunch.LabSeed=12345; GameSceneLaunch.LabEdges=false; GameSceneLaunch.LabGates=true; GameSceneLaunch.ReturningToLab=true;
            yield return SceneManager.LoadSceneAsync("TestScene",LoadSceneMode.Additive); yield return null;
            var lab=Object.FindFirstObjectByType<Stage4ControlsDemo>(); float deadline=Time.realtimeSinceStartup+35; while(lab.Generation.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(lab.Generation.Generated,Is.Not.Null,lab.Generation.StatusMessage); Assert.That(lab.Generation.Generated.Recipe.Shape.Id,Does.Contain("/t/")); string fingerprint=lab.Generation.Generated.Fingerprint;
            lab.GetComponentsInChildren<Button>(true).First(b=>b.name=="Play this puzzle in game scene").onClick.Invoke(); yield return null; yield return null;
            var game=Object.FindFirstObjectByType<GameplayController>(); Assert.That(game.Fingerprint,Is.EqualTo(fingerprint)); Assert.That(game.Movement.SurfaceGeometry,Is.InstanceOf<QuadSurfaceGeometry>());
            game.ReturnToLab(); yield return null; yield return null; lab=Object.FindFirstObjectByType<Stage4ControlsDemo>();
            deadline=Time.realtimeSinceStartup+35; while(lab.Generation.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(lab.Generation.DesiredShape,Is.EqualTo(GenerationLabShape.T)); Assert.That(lab.Generation.SurfaceSubdivisions,Is.EqualTo(4)); Assert.That(lab.Generation.Generated.Fingerprint,Is.EqualTo(fingerprint));
        }
    }
}
