using System;
using System.Collections;
using System.IO;
using ArrowCube.Controls;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using NUnit.Framework;
using Object=UnityEngine.Object;

namespace ArrowCube.Tests
{
    public class GenericSurfacePlayTests
    {
        private GameObject root;
        private Stage2MovementDemo movement;
        private PuzzleInputManager input;
        private float previousCaptureDelta;
        private RenderPipelineAsset previousPipeline,previousQuality;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            previousCaptureDelta=Time.captureDeltaTime; Time.captureDeltaTime=1f/60;
            previousPipeline=GraphicsSettings.defaultRenderPipeline; previousQuality=QualitySettings.renderPipeline;
#if UNITY_EDITOR
            if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
            {
                var mobile=UnityEditor.AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
                if(mobile!=null) { GraphicsSettings.defaultRenderPipeline=mobile; QualitySettings.renderPipeline=mobile; }
            }
#endif
            root=new GameObject("Generic surface play test"); root.SetActive(false);
            movement=root.AddComponent<Stage2MovementDemo>(); movement.StartupOwnedExternally=true; movement.ShowControls=false;
            input=root.AddComponent<PuzzleInputManager>(); root.SetActive(true); input.NotifyFocus(true); yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup() { Object.Destroy(root); Time.captureDeltaTime=previousCaptureDelta; GraphicsSettings.defaultRenderPipeline=previousPipeline; QualitySettings.renderPipeline=previousQuality; yield return null; }

        [UnityTest]
        public IEnumerator BothAuthoredPuzzlesAnimateBlockedAttemptsAndVerifiedRemovals()
        {
            foreach(bool folded in new[] {false,true})
            {
                var demo=folded ? AuthoredSurfaceDemos.CreateFolded() : AuthoredSurfaceDemos.CreateL();
                movement.LoadDefinition(demo.Puzzle,demo.Geometry); movement.InspectionCamera.rect=new Rect(0,0,1,1);
                movement.AnimationCellsPerSecond=8; yield return null;
                string prefix=folded ? "prism" : "L";
                Capture(prefix+"-front",new Vector3(8,7,10)); Capture(prefix+"-rear",new Vector3(-8,-6,-10));
                long revision=movement.Grid.Revision; var head=movement.GetArrowVisual(10).Find("Arrowhead").GetComponent<MeshFilter>().sharedMesh;
                var original=head.vertices; bool contact=false;
                Assert.That(movement.TryActivate(10), Is.True);
                float deadline=Time.realtimeSinceStartup+6;
                while(movement.IsAnimating && Time.realtimeSinceStartup<deadline)
                {
                    var marker=root.transform.Find("Stage 2 runtime objects/Block contact");
                    if(!contact && marker!=null && marker.gameObject.activeSelf) { contact=true; Capture(prefix+"-contact",new Vector3(8,7,10)); }
                    Assert.That(movement.Grid.Revision, Is.EqualTo(revision)); yield return null;
                }
                Assert.That(movement.IsAnimating, Is.False); Assert.That(contact, Is.True); Assert.That(head.vertices, Is.EqualTo(original));
                foreach(int id in new[] {20,10,30})
                {
                    Assert.That(movement.TryActivate(id), Is.True); Assert.That(movement.LastPlan.Succeeded, Is.True);
                    var plan=movement.LastPlan; var rail=SurfaceRail.FromPlan(plan); var geometry=new PathAnimationGeometry(demo.Geometry);
                    int initialHead=plan.Path.CellIds.Count-1;
                    var fold=geometry.Evaluate(rail,initialHead+geometry.BoundaryFraction(rail,initialHead),demo.Geometry.ReferenceCellSize*.04f);
                    var n1=demo.Geometry.GetCellNormal(rail[initialHead].CellId); var n2=demo.Geometry.GetCellNormal(rail[initialHead+1].CellId);
                    var axis=Vector3.Cross(n1,n2).normalized; if(Vector3.Dot(axis,Vector3.forward)<0) axis=-axis;
                    var foldView=demo.Geometry.Bounds.center+(n1+n2).normalized*10+axis*6;
                    bool captured=false; deadline=Time.realtimeSinceStartup+7;
                    while(movement.IsAnimating && Time.realtimeSinceStartup<deadline)
                    {
                        var visual=movement.GetArrowVisual(id);
                        foreach(var filter in visual.GetComponentsInChildren<MeshFilter>())
                            foreach(var vertex in filter.sharedMesh.vertices) Assert.That(float.IsNaN(vertex.x)||float.IsInfinity(vertex.x), Is.False);
                        var tip=visual.Find("Arrowhead").GetComponent<MeshFilter>().sharedMesh.vertices[0];
                        if(id==10 && !captured && Vector3.Distance(tip,fold)<demo.Geometry.ReferenceCellSize*.12f) { captured=true; Capture(prefix+"-moving-fold",foldView); }
                        Assert.That(movement.Grid.Paths.ContainsKey(id), Is.True,"Occupancy remains reserved until the tail has left.");
                        yield return null;
                    }
                    Assert.That(movement.IsAnimating, Is.False); Assert.That(movement.Grid.Paths.ContainsKey(id), Is.False);
                    if(id==10) Assert.That(captured, Is.True,"Record a frame at the first fold, including the concave L transition.");
                    Assert.That(geometry.Evaluate(rail,rail.Count-1), Is.Not.EqualTo(demo.Geometry.GetCellCenter(plan.Path.HeadCellId)));
                }
                Assert.That(movement.Grid.OccupiedCellCount+movement.Grid.OccupiedSegmentCount, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator PickingUsesActualSurfaceOcclusionAndWorksAfterOrbitAndRootTransforms()
        {
            foreach(bool folded in new[] {false,true})
            {
                var demo=folded ? AuthoredSurfaceDemos.CreateFolded() : AuthoredSurfaceDemos.CreateL();
                root.transform.SetPositionAndRotation(new Vector3(3,-2,1),Quaternion.Euler(11,27,-9)); root.transform.localScale=new Vector3(1.2f,.85f,1.1f);
                movement.LoadDefinition(demo.Puzzle,demo.Geometry); movement.InspectionCamera.rect=new Rect(0,0,1,1); yield return null;
                var picker=new PathPicker(movement);
                foreach(var path in demo.Puzzle.Paths)
                {
                    input.Orbit.FocusCell(demo.Geometry,path.HeadCellId); for(int i=0;i<25;i++) input.Orbit.Tick(.1f);
                    var world=root.transform.TransformPoint(demo.Geometry.GetCellCenter(path.HeadCellId,demo.Geometry.ReferenceCellSize*.04f));
                    var point=(Vector2)movement.InspectionCamera.WorldToScreenPoint(world);
                    Assert.That(picker.TryPick(point,2,out int selected), Is.True,$"Could not pick {path.Id} on {demo.Puzzle.Name}"); Assert.That(selected, Is.EqualTo(path.Id));
                }
                input.Orbit.FocusCell(demo.Geometry,folded ? 200 : 202); for(int i=0;i<25;i++) input.Orbit.Tick(.1f);
                var hidden=demo.Puzzle.Paths[2];
                var behind=(Vector2)movement.InspectionCamera.WorldToScreenPoint(root.transform.TransformPoint(demo.Geometry.GetCellCenter(hidden.HeadCellId,demo.Geometry.ReferenceCellSize*.04f)));
                picker.TryPick(behind,1,out int id); Assert.That(id, Is.Not.EqualTo(hidden.Id),"Rear arrows must be occluded by the real surface.");
            }
        }

        [UnityTest]
        public IEnumerator HybridDeparturesAndPauseUseTheSameRenderer()
        {
            var demo=AuthoredSurfaceDemos.CreateL(ExitRules.Hybrid); movement.LoadDefinition(demo.Puzzle,demo.Geometry); yield return null;
            movement.AnimationCellsPerSecond=1;
            Assert.That(movement.TryActivate(20), Is.True); Assert.That(movement.LastPlan.ExitType, Is.EqualTo(ExitType.CubeEdge));
            movement.AnimationPaused=true; var head=movement.GetArrowVisual(20).Find("Arrowhead").GetComponent<MeshFilter>().sharedMesh; var before=head.vertices;
            yield return new WaitForSecondsRealtime(.1f); Assert.That(head.vertices, Is.EqualTo(before));
            movement.AnimationPaused=false; movement.AnimationCellsPerSecond=100;
            float deadline=Time.realtimeSinceStartup+4; while(movement.IsAnimating && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(movement.IsAnimating, Is.False); Assert.That(movement.TryActivate(10), Is.True);
            Assert.That(movement.LastPlan.GetCellPosition(1,0).IsOnSurface, Is.True,"The inside corner must be crossed before the eligible outside edge.");
            while(movement.IsAnimating && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(movement.Grid.Paths.ContainsKey(10), Is.False);
        }

        private void Capture(string name,Vector3 position)
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null) return;
            var camera=movement.InspectionCamera; var rotation=camera.transform.rotation; var oldPosition=camera.transform.position; var oldRect=camera.rect;
            var previous=RenderTexture.active; var previousTarget=camera.targetTexture;
            var target=new RenderTexture(768,768,24,RenderTextureFormat.ARGB32); target.Create();
            try
            {
                camera.rect=new Rect(0,0,1,1); camera.targetTexture=target;
                camera.transform.position=root.transform.TransformPoint(position); camera.transform.LookAt(root.transform.TransformPoint(movement.SurfaceBounds.center),root.transform.up);
                if(GraphicsSettings.currentRenderPipeline==null) camera.Render();
                else RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest {destination=target});
                RenderTexture.active=target; var image=new Texture2D(768,768,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,768,768),0,0); image.Apply();
                int black=0; foreach(var pixel in image.GetPixels32()) if(pixel.r<25 && pixel.g<25 && pixel.b<25 || pixel.r>180 && pixel.g<100 && pixel.b<80) black++;
                Assert.That(black, Is.GreaterThan(20),"Visible arrow pixels must survive actual depth-tested rendering.");
                string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../TestResults/SurfaceGeometryVisuals")); Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder,(GraphicsSettings.currentRenderPipeline==null ? "builtin-" : "urp-")+name+".png"),image.EncodeToPNG()); Object.Destroy(image);
            }
            finally { camera.targetTexture=previousTarget; camera.rect=oldRect; camera.transform.SetPositionAndRotation(oldPosition,rotation); RenderTexture.active=previous; target.Release(); Object.Destroy(target); }
        }
    }
}
