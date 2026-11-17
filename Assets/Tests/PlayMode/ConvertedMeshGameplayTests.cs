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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace ArrowCube.Tests
{
    public class ConvertedMeshGameplayTests
    {
        private readonly InputTestFixture devices=new InputTestFixture();
        private Mouse mouse; private Touchscreen touch; private string folder;
        private RenderPipelineAsset previousPipeline,previousQuality;
        [UnitySetUp] public IEnumerator Setup()
        {
            devices.Setup(); mouse=InputSystem.AddDevice<Mouse>(); touch=InputSystem.AddDevice<Touchscreen>();
            folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/ConvertedMeshTests",Guid.NewGuid().ToString("N")));
            previousPipeline=GraphicsSettings.defaultRenderPipeline; previousQuality=QualitySettings.renderPipeline;
#if UNITY_EDITOR
            if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
            { var mobile=UnityEditor.AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset"); if(mobile!=null) { GraphicsSettings.defaultRenderPipeline=mobile; QualitySettings.renderPipeline=mobile; } }
#endif
            SceneManager.sceneLoaded+=Loaded; yield return null;
        }
        private void Loaded(Scene scene,LoadSceneMode mode)
        { foreach(var root in scene.GetRootGameObjects()) { var game=root.GetComponent<GameplayController>(); if(game!=null) { game.SavePathOverride=Path.Combine(folder,"converted.json"); game.GenerateOnMainThread=true; } } }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded-=Loaded; if(SceneManager.GetSceneByName("SurfaceMeshConversion").isLoaded) yield return SceneManager.UnloadSceneAsync("SurfaceMeshConversion");
            GraphicsSettings.defaultRenderPipeline=previousPipeline; QualitySettings.renderPipeline=previousQuality; yield return null; devices.TearDown();
        }
        private IEnumerator Idle(GameplayController game)
        { float deadline=Time.realtimeSinceStartup+7; while(game.Movement.IsAnimating && Time.realtimeSinceStartup<deadline) yield return null; Assert.That(game.Movement.IsAnimating,Is.False); }
        private Vector2 Point(GameplayController game,int id)
        {
            var path=game.Session.Grid.Paths[id]; var geometry=game.Movement.SurfaceGeometry;
            game.Input.Orbit.FocusCell(geometry,path.HeadCellId); for(int i=0;i<25;i++) game.Input.Orbit.Tick(.1f);
            var world=game.transform.TransformPoint(geometry.GetCellCenter(path.HeadCellId,geometry.ReferenceCellSize*.04f));
            // Inspect directly along the cell's actual normal, keeping the target away from HUD overlays.
            var normal=game.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(geometry.GetCellNormal(path.HeadCellId)).normalized;
            game.Movement.InspectionCamera.transform.position=world+normal*geometry.Bounds.extents.magnitude*3;
            game.Movement.InspectionCamera.transform.LookAt(world,game.Movement.InspectionCamera.transform.up);
            return game.Movement.InspectionCamera.WorldToScreenPoint(world);
        }
        private void MouseClick(Vector2 p)
        { InputSystem.QueueStateEvent(mouse,new MouseState {position=p,buttons=1}); InputSystem.QueueStateEvent(mouse,new MouseState {position=p,buttons=0}); InputSystem.Update(); }
        [UnityTest] public IEnumerator DeliveredMeshLabPlaysAllFourBakedSurfacesWithRealMouseAndTouch()
        {
            yield return SceneManager.LoadSceneAsync("SurfaceMeshConversion",LoadSceneMode.Additive); yield return null;
            var demo=Object.FindFirstObjectByType<SurfaceGenerationDemo>(); var game=demo.GetComponent<GameplayController>();
            Assert.That(demo.ConvertedExamples.Length,Is.EqualTo(4));
            foreach(var asset in demo.ConvertedExamples)
            {
                if(demo.ConvertedSurface!=asset) { demo.ConvertedSurface=asset; demo.Generate(); }
                float deadline=Time.realtimeSinceStartup+25; while(game.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.That(game.Page,Is.EqualTo(GamePage.Game),game.Message); game.Input.NotifyFocus(true);
                var puzzle=(GeneratedPuzzle)game.Session.Puzzle; Assert.That(puzzle.DependencyGraph.IsValid,Is.True); Assert.That(puzzle.Statistics.DependencyCount,Is.GreaterThan(0));
                var renderer=game.GetComponentInChildren<SurfaceMeshRenderer>(); Assert.That(renderer.GetComponent<MeshRenderer>().sharedMaterials.Length,Is.LessThanOrEqualTo(8),"Surface rendering is batched by palette material, not by logical cell.");
                Capture(game,asset.name+"-front",new Vector3(10,8,12)); Capture(game,asset.name+"-rear",new Vector3(-10,-8,-12));
                int blocked=puzzle.DependencyGraph.Nodes.First(n=>!n.InitiallyRemovable).PathId; long revision=game.Session.Grid.Revision;
                // Respect the normal touch-to-mouse synthetic-event suppression window between models.
                yield return new WaitForSecondsRealtime(.3f);
                game.Movement.AnimationCellsPerSecond=1000; int accepted=game.Input.AcceptedActivations;
                MouseClick(Point(game,blocked)); Assert.That(game.Input.AcceptedActivations,Is.EqualTo(accepted+1)); Assert.That(game.Movement.LastPlan.Outcome,Is.EqualTo(MovementOutcome.BlockedByPath)); yield return Idle(game);
                Assert.That(game.Session.Grid.Revision,Is.EqualTo(revision));
                int crossing=puzzle.Paths.First(p=>p.GeometricTransitionCount>0).Id; bool animatedCapture=false; int index=0;
                foreach(int id in puzzle.VerifiedSolution)
                {
                    game.Movement.AnimationCellsPerSecond=id==crossing ? 15 : 1000; var point=Point(game,id); accepted=game.Input.AcceptedActivations;
                    if(index++==0) MouseClick(point); else { devices.BeginTouch(index,point,screen:touch); devices.EndTouch(index,point,screen:touch); }
                    Assert.That(game.Input.AcceptedActivations,Is.EqualTo(accepted+1)); Assert.That(game.Movement.LastPlan.Succeeded,Is.True);
                    if(id==crossing)
                    {
                        deadline=Time.realtimeSinceStartup+7;
                        while(game.Movement.IsAnimating && Time.realtimeSinceStartup<deadline)
                        {
                            foreach(var filter in game.Movement.GetArrowVisual(id).GetComponentsInChildren<MeshFilter>())
                                foreach(var v in filter.sharedMesh.vertices) Assert.That(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z),Is.False);
                            if(!animatedCapture) { Capture(game,asset.name+"-moving",new Vector3(10,8,12)); animatedCapture=true; } yield return null;
                        }
                    }
                    yield return Idle(game); Assert.That(game.Session.Grid.Paths.ContainsKey(id),Is.False);
                }
                Assert.That(animatedCapture,Is.True); Assert.That(game.Page,Is.EqualTo(GamePage.Victory)); Assert.That(game.Session.Grid.Paths,Is.Empty);
                Assert.That(game.Undo(),Is.True); Assert.That(game.Page,Is.EqualTo(GamePage.Game));
                Assert.That(game.ContinueSandbox(),Is.True); Assert.That(game.Movement.SurfaceGeometry,Is.InstanceOf<QuadSurfaceGeometry>());
            }
        }
        [UnityTest] public IEnumerator ConvertedRuntimeSelectorAndMeasuredDifficultyRetainBakedIdentity()
        {
            yield return SceneManager.LoadSceneAsync("SurfaceMeshConversion",LoadSceneMode.Additive); yield return null;
            var demo=Object.FindFirstObjectByType<SurfaceGenerationDemo>(); var game=demo.GetComponent<GameplayController>();
            float deadline=Time.realtimeSinceStartup+25; while(game.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
            var before=demo.ConvertedSurface; demo.GetComponentsInChildren<Button>(true).First(b=>b.name.StartsWith("Shape:",StringComparison.Ordinal)).onClick.Invoke(); Assert.That(demo.ConvertedSurface,Is.Not.SameAs(before));
            demo.Seed=71; demo.MeasuredDifficultySearch=true; demo.Generate();
            deadline=Time.realtimeSinceStartup+40; while(game.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(game.Page,Is.EqualTo(GamePage.Game),game.Message); var generated=(GeneratedPuzzle)game.Session.Puzzle;
            Assert.That(generated.Difficulty,Is.Not.Null); Assert.That(generated.Recipe.Shape.Id,Is.EqualTo(demo.ConvertedSurface.ShapeId));
            Assert.That(game.ContinueSandbox(),Is.True); Assert.That(game.Session.Puzzle.SurfaceAssetId,Is.EqualTo(demo.ConvertedSurface.ShapeId));
        }
        [UnityTest] public IEnumerator TooCoarseAConvertedSurfaceReportsTheConflictAndKeepsThePreviousPuzzle()
        {
            yield return SceneManager.LoadSceneAsync("SurfaceMeshConversion",LoadSceneMode.Additive); yield return null;
            var demo=Object.FindFirstObjectByType<SurfaceGenerationDemo>(); var game=demo.GetComponent<GameplayController>();
            float deadline=Time.realtimeSinceStartup+25; while(game.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
            var old=game.Session; var mesh=MeshConversionFixtures.Create(MeshConversionFixture.RectangularPrism); var asset=ScriptableObject.CreateInstance<ConvertedSurfaceAsset>();
            try
            {
                asset.Initialize(MeshSurfaceConverter.Convert(mesh,new MeshConversionSettings {Subdivisions=1})); demo.ConvertedSurface=asset; demo.Generate(); yield return null;
                Assert.That(game.IsGenerating,Is.False); Assert.That(game.Session,Is.SameAs(old)); StringAssert.Contains("6 cells",demo.LastGenerationError); StringAssert.Contains("density",demo.LastGenerationError);
            }
            finally { demo.ConvertedSurface=demo.ConvertedExamples[0]; Object.Destroy(mesh); Object.Destroy(asset); }
        }
        private void Capture(GameplayController game,string name,Vector3 position)
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null) return;
            var camera=game.Movement.InspectionCamera; var rotation=camera.transform.rotation; var oldPosition=camera.transform.position; var oldRect=camera.rect;
            var previous=RenderTexture.active; var previousTarget=camera.targetTexture; var target=new RenderTexture(768,768,24,RenderTextureFormat.ARGB32); target.Create(); var watch=System.Diagnostics.Stopwatch.StartNew();
            try
            {
                camera.rect=new Rect(0,0,1,1); camera.targetTexture=target; camera.transform.position=game.transform.TransformPoint(position); camera.transform.LookAt(game.transform.TransformPoint(game.Movement.SurfaceBounds.center),game.transform.up);
                if(GraphicsSettings.currentRenderPipeline==null) camera.Render(); else RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest {destination=target});
                double renderMilliseconds=watch.Elapsed.TotalMilliseconds; RenderTexture.active=target; var image=new Texture2D(768,768,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,768,768),0,0); image.Apply();
                Assert.That(image.GetPixels32().Count(p=>p.r<25 && p.g<25 && p.b<25),Is.GreaterThan(20));
                string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../TestResults")); Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output,"phase5-"+name+".png"),image.EncodeToPNG()); Object.Destroy(image);
                Debug.Log($"MESH RENDER {name}: cells={game.Session.Puzzle.Surface.CellCount} surface_draw_groups={game.GetComponentInChildren<SurfaceMeshRenderer>().GetComponent<MeshRenderer>().sharedMaterials.Length} render_submit_ms={renderMilliseconds:F2}");
            }
            finally { camera.targetTexture=previousTarget; camera.rect=oldRect; camera.transform.SetPositionAndRotation(oldPosition,rotation); RenderTexture.active=previous; target.Release(); Object.Destroy(target); }
        }
    }
}
