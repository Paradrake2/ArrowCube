using System;
using System.Collections;
using System.IO;
using System.Linq;
using ArrowCube.Controls;
using ArrowCube.Game;
using ArrowCube.Gameplay;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace ArrowCube.Tests
{
    public class GenericSurfaceGenerationPlayTests
    {
        private string folder;
        private RenderPipelineAsset previousPipeline,previousQuality;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/ProceduralSurfaceTests",Guid.NewGuid().ToString("N")));
            previousPipeline=GraphicsSettings.defaultRenderPipeline; previousQuality=QualitySettings.renderPipeline;
#if UNITY_EDITOR
            if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
            {
                var mobile=UnityEditor.AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
                if(mobile!=null) { GraphicsSettings.defaultRenderPipeline=mobile; QualitySettings.renderPipeline=mobile; }
            }
#endif
            SceneManager.sceneLoaded+=Loaded; yield return null;
        }
        private void Loaded(Scene scene,LoadSceneMode mode)
        {
            foreach(var root in scene.GetRootGameObjects())
            { var game=root.GetComponent<GameplayController>(); if(game!=null) { game.SavePathOverride=Path.Combine(folder,"game.json"); game.GenerateOnMainThread=true; } }
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded-=Loaded;
            if(SceneManager.GetSceneByName("SurfaceGeneration").isLoaded) yield return SceneManager.UnloadSceneAsync("SurfaceGeneration");
            GraphicsSettings.defaultRenderPipeline=previousPipeline; QualitySettings.renderPipeline=previousQuality; yield return null;
        }
        [UnityTest]
        public IEnumerator DeliveredLabGeneratesPicksBlocksAnimatesAndCompletesAllThreeShapes()
        {
            yield return SceneManager.LoadSceneAsync("SurfaceGeneration",LoadSceneMode.Additive); yield return null;
            var demo=Object.FindFirstObjectByType<SurfaceGenerationDemo>(); Assert.That(demo,Is.Not.Null);
            var game=demo.GetComponent<GameplayController>();
            foreach(var shape in new[] {AuthoredGenerationShape.L,AuthoredGenerationShape.ObliquePrism,AuthoredGenerationShape.T})
            {
                if(shape!=AuthoredGenerationShape.L) { demo.Shape=shape; demo.Generate(); }
                float deadline=Time.realtimeSinceStartup+20;
                while(game.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.That(game.IsGenerating,Is.False); Assert.That(game.Page,Is.EqualTo(GamePage.Game),game.Message);
                var puzzle=game.Session.Puzzle as GeneratedPuzzle; Assert.That(puzzle,Is.Not.Null);
                Assert.That(game.IsSandbox,Is.True); Assert.That(game.Input.enabled,Is.True);
                Assert.That(game.Movement.GetComponentInChildren<SurfaceMeshRenderer>(),Is.Not.Null);
                Assert.That(game.Hint(),Is.True); yield return null;
                var path=puzzle.Paths.First(p=>p.Id==puzzle.VerifiedSolution[0]);
                game.Input.Orbit.FocusCell(game.Movement.SurfaceGeometry,path.HeadCellId); for(int i=0;i<25;i++) game.Input.Orbit.Tick(.1f);
                var world=game.transform.TransformPoint(game.Movement.SurfaceGeometry.GetCellCenter(path.HeadCellId,game.Movement.SurfaceGeometry.ReferenceCellSize*.04f));
                var point=(Vector2)game.Movement.InspectionCamera.WorldToScreenPoint(world);
                Assert.That(new PathPicker(game.Movement).TryPick(point,3,out int selected),Is.True); Assert.That(selected,Is.EqualTo(path.Id));
                Capture(game,shape+"-front",new Vector3(9,8,12)); Capture(game,shape+"-rear",new Vector3(-9,-8,-12));
                game.Movement.AnimationCellsPerSecond=1000;
                int blocked=puzzle.DependencyGraph.Nodes.First(n=>!n.InitiallyRemovable).PathId;
                long revision=game.Session.Grid.Revision;
                Assert.That(game.ActivatePath(blocked),Is.True); Assert.That(game.Movement.LastPlan.Outcome,Is.EqualTo(MovementOutcome.BlockedByPath));
                deadline=Time.realtimeSinceStartup+6; while(game.Movement.IsAnimating && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.That(game.Movement.IsAnimating,Is.False); Assert.That(game.Session.Grid.Revision,Is.EqualTo(revision));
                foreach(int id in puzzle.VerifiedSolution)
                {
                    Assert.That(game.ActivatePath(id),Is.True); Assert.That(game.Movement.LastPlan.Succeeded,Is.True);
                    deadline=Time.realtimeSinceStartup+6; while(game.Movement.IsAnimating && Time.realtimeSinceStartup<deadline) yield return null;
                    Assert.That(game.Movement.IsAnimating,Is.False); Assert.That(game.Session.Grid.Paths.ContainsKey(id),Is.False);
                }
                Assert.That(game.Session.State,Is.EqualTo(PlayState.Victory)); Assert.That(game.Page,Is.EqualTo(GamePage.Victory));
            }
            Assert.That(demo.GetComponentsInChildren<Slider>(true).Length,Is.GreaterThanOrEqualTo(8));
            var scroll=demo.GetComponentInChildren<ScrollRect>(true);
            Assert.That(scroll.scrollSensitivity,Is.EqualTo(6));
            Assert.That(scroll.content.rect.width,Is.LessThanOrEqualTo(scroll.viewport.rect.width+1),"Settings must fit the panel without clipping their labels and handles.");
            var session=game.Session; demo.Generate(); Assert.That(game.IsGenerating,Is.True); game.CancelGeneration();
            Assert.That(game.Session,Is.SameAs(session),"Cancellation keeps the previous playable puzzle.");
        }
        [UnityTest]
        public IEnumerator RuntimeRecipeControlsAndMeasuredSearchProduceAPlayableSavedPuzzle()
        {
            yield return SceneManager.LoadSceneAsync("SurfaceGeneration",LoadSceneMode.Additive); yield return null;
            var demo=Object.FindFirstObjectByType<SurfaceGenerationDemo>(); var game=demo.GetComponent<GameplayController>();
            float deadline=Time.realtimeSinceStartup+20; while(game.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
            var buttons=demo.GetComponentsInChildren<Button>(true);
            buttons.First(b=>b.name=="Shape: L").onClick.Invoke(); Assert.That(demo.Shape,Is.EqualTo(AuthoredGenerationShape.ObliquePrism));
            var input=demo.GetComponentInChildren<InputField>(true); input.onEndEdit.Invoke("71"); Assert.That(demo.Seed,Is.EqualTo(71));
            demo.MeasuredDifficultySearch=true; demo.Generate();
            deadline=Time.realtimeSinceStartup+30; while(game.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(game.Page,Is.EqualTo(GamePage.Game),game.Message);
            var puzzle=(GeneratedPuzzle)game.Session.Puzzle; Assert.That(puzzle.Difficulty,Is.Not.Null);
            Assert.That(game.ContinueSandbox(),Is.True); Assert.That(game.Session.Grid.Paths.Count,Is.EqualTo(puzzle.Paths.Count));
            Assert.That(game.Movement.SurfaceGeometry,Is.InstanceOf<QuadSurfaceGeometry>());
        }
        private void Capture(GameplayController game,string name,Vector3 position)
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null) return;
            var camera=game.Movement.InspectionCamera; var rotation=camera.transform.rotation; var oldPosition=camera.transform.position; var oldRect=camera.rect;
            var previous=RenderTexture.active; var previousTarget=camera.targetTexture; var target=new RenderTexture(768,768,24,RenderTextureFormat.ARGB32); target.Create();
            try
            {
                camera.rect=new Rect(0,0,1,1); camera.targetTexture=target; camera.transform.position=game.transform.TransformPoint(position); camera.transform.LookAt(game.transform.TransformPoint(game.Movement.SurfaceBounds.center),game.transform.up);
                if(GraphicsSettings.currentRenderPipeline==null) camera.Render(); else RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest {destination=target});
                RenderTexture.active=target; var image=new Texture2D(768,768,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,768,768),0,0); image.Apply();
                Assert.That(image.GetPixels32().Count(p=>p.r<25 && p.g<25 && p.b<25),Is.GreaterThan(20));
                string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../TestResults")); Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output,"phase4-"+name+".png"),image.EncodeToPNG()); Object.Destroy(image);
            }
            finally { camera.targetTexture=previousTarget; camera.rect=oldRect; camera.transform.SetPositionAndRotation(oldPosition,rotation); RenderTexture.active=previous; target.Release(); Object.Destroy(target); }
        }
    }
}
