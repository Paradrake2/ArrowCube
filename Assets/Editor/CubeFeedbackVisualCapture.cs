using System;
using System.Collections.Generic;
using System.IO;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using ArrowCube.Topology;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Runs in an isolated graphics-enabled batch project, including Mobile URP when supplied.</summary>
public static class CubeFeedbackVisualCapture
{
    public static void Capture()
    {
        if(!Application.isBatchMode || SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)
            throw new InvalidOperationException("Run this capture in an isolated graphics-enabled batch project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var mobile=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline=mobile; QualitySettings.renderPipeline=mobile;
        string pipeline=mobile==null ? "builtin" : "urp";
        string directory=Path.GetFullPath("TestResults/CubeFeedback/Visuals"); Directory.CreateDirectory(directory);
        var root=new GameObject("Cube feedback visual verification");
        var materials=new List<Material>(); var faceMaterials=new Material[6];
        var topology=new CubeTopology(6); var geometry=new CubeSurfaceGeometry(topology,4);
        for(int face=0;face<6;face++) { faceMaterials[face]=PuzzleMaterials.Create(ColoredCubeSurface.FaceColor((CubeFace)face)); materials.Add(faceMaterials[face]); }
        var cube=new GameObject("Puzzle surface"); cube.transform.SetParent(root.transform,false);
        cube.AddComponent<ColoredCubeSurface>().Initialize(topology,4,faceMaterials);
        var black=PuzzleMaterials.Create(Color.black); materials.Add(black);
        for(int face=0;face<6;face++)
        {
            var path=new ArrowPath(face,topology,new[] { Cell(4,3),Cell(3,3),Cell(2,3),Cell(2,2),Cell(2,1) },SurfaceDirection.Right);
            var points=new List<Vector3>(); geometry.WritePolyline(path,points,geometry.CellSize*.04f);
            var normal=CubeSurfaceGeometry.ToVector(topology.GetOrientation((CubeFace)face).Normal);
            var normals=new List<Vector3>(); for(int i=1;i<points.Count;i++) normals.Add(normal);
            var body=new GameObject("Body").AddComponent<SurfacePathRenderer>(); body.transform.SetParent(root.transform,false); body.Initialize(black);
            var head=new GameObject("Head").AddComponent<TriangleArrowHead>(); head.transform.SetParent(root.transform,false); head.Initialize(black);
            head.DrawAndTrimBody(points,normals,geometry.GetLocalDirection((CubeFace)face,path.HeadDirection),normal,geometry.CellSize,geometry.CellSize*.3f);
            body.Draw(points,normals,geometry.CellSize*.3f);
            SurfaceCoordinate Cell(int x,int y) => new SurfaceCoordinate((CubeFace)face,x,y);
        }
        var camera=new GameObject("Capture camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform,false);
        camera.orthographic=true; camera.orthographicSize=3.3f; camera.nearClipPlane=.05f; camera.farClipPlane=100;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.14f,.22f,.34f);
        var target=new RenderTexture(768,768,16,RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture=target;
        var previous=RenderTexture.active;
        var light=new GameObject("Strong one-sided light").AddComponent<Light>(); light.transform.SetParent(root.transform,false);
        light.type=LightType.Directional; light.intensity=3; light.shadows=LightShadows.Soft; light.transform.rotation=Quaternion.Euler(50,-30,0);
        RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=Color.black; RenderSettings.ambientIntensity=0;
        var report=new System.Text.StringBuilder("face,dark_r,dark_g,dark_b,lit_r,lit_g,lit_b\n");
        try
        {
            for(int face=0;face<6;face++)
            {
                var frame=topology.GetOrientation((CubeFace)face);
                camera.transform.position=CubeSurfaceGeometry.ToVector(frame.Normal)*10;
                camera.transform.LookAt(Vector3.zero,CubeSurfaceGeometry.ToVector(frame.Up));
                light.enabled=false; var dark=Read(false,null);
                light.enabled=true; var lit=Read(false,null);
                if(Mathf.Min(dark.r,dark.g,dark.b)<.25f || Mathf.Abs(dark.r-lit.r)+Mathf.Abs(dark.g-lit.g)+Mathf.Abs(dark.b-lit.b)>.02f)
                    throw new InvalidOperationException($"Face {(CubeFace)face} darkened or changed with lighting: {dark} / {lit}.");
                report.AppendLine(FormattableString.Invariant($"{(CubeFace)face},{dark.r:F3},{dark.g:F3},{dark.b:F3},{lit.r:F3},{lit.g:F3},{lit.b:F3}"));
            }
            var views=new[] { new Vector3(7,6,9),new Vector3(-7,-6,-9) };
            for(int view=0;view<views.Length;view++)
            { camera.transform.position=views[view]; camera.transform.LookAt(Vector3.zero); Read(true,Path.Combine(directory,$"{pipeline}-cube-{view}.png")); }
            File.WriteAllText(Path.Combine(directory,$"{pipeline}-lighting.csv"),report.ToString());
            Debug.Log("All six cube faces stay visible with no lighting and strong directional lighting: "+pipeline);
        }
        finally
        {
            RenderTexture.active=previous; camera.targetTexture=null; target.Release(); UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(root); foreach(var material in materials) UnityEngine.Object.DestroyImmediate(material);
        }

        Color Read(bool save,string file)
        {
            if(mobile==null) camera.Render();
            else RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest { destination=target });
            RenderTexture.active=target;
            var image=new Texture2D(768,768,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,768,768),0,0); image.Apply();
            var pixel=image.GetPixel(384,384); if(save) File.WriteAllBytes(file,image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image); return pixel;
        }
    }
}
