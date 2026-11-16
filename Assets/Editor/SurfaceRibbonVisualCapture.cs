using System;
using System.Collections.Generic;
using System.IO;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Graphics QA entry point for an isolated batch project, never the user's open scene.</summary>
public static class SurfaceRibbonVisualCapture
{
    public static void Capture()
    {
        if(!Application.isBatchMode) throw new InvalidOperationException("Capture must run in an isolated batch project.");
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null) throw new InvalidOperationException("Capture requires a graphics device.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("Surface ribbon visual QA");
        var black=new Material(Shader.Find("Unlit/Color")) { color=Color.black };
        var pale=new Material(Shader.Find("Unlit/Color")) { color=new Color(.83f,.92f,1) };
        var target=new RenderTexture(1000,1000,24,RenderTextureFormat.ARGB32) { antiAliasing=4 };
        var previous=RenderTexture.active;
        try
        {
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube); cube.transform.SetParent(root.transform,false);
            cube.transform.localScale=Vector3.one*4; cube.GetComponent<Renderer>().sharedMaterial=pale;
            var puzzle=new PuzzleGenerator().Generate(new PuzzleRecipe(19,DifficultyProfiles.All[2],ExitRules.EdgeOnly));
            var geometry=new CubeSurfaceGeometry(puzzle.Topology,4); var animation=new PathAnimationGeometry(geometry);
            var points=new List<Vector3>(); var normals=new List<Vector3>();
            foreach(var path in puzzle.Paths)
            {
                var rail=new List<PathPosition>(); for(int i=path.Cells.Count-1;i>=0;i--) rail.Add(new PathPosition(path.Cells[i]));
                animation.WritePolyline(rail,path.Cells.Count,0,points,geometry.CellSize*.04f,normals,path.HeadDirection);
                var headObject=new GameObject("Triangle"); headObject.transform.SetParent(root.transform,false);
                var head=headObject.AddComponent<TriangleArrowHead>(); head.Initialize(black);
                head.DrawAndTrimBody(points,normals,geometry.GetLocalDirection(path.Head.Face,path.HeadDirection),
                    CubeSurfaceGeometry.ToVector(puzzle.Topology.GetOrientation(path.Head.Face).Normal),geometry.CellSize,geometry.CellSize*.28f);
                var bodyObject=new GameObject("Ribbon"); bodyObject.transform.SetParent(root.transform,false);
                var body=bodyObject.AddComponent<SurfacePathRenderer>(); body.Initialize(black); body.Draw(points,normals,geometry.CellSize*.28f);
            }
            var cameraObject=new GameObject("Capture camera"); cameraObject.transform.SetParent(root.transform,false);
            var camera=cameraObject.AddComponent<Camera>(); camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.68f,.8f,.92f); camera.orthographic=true; camera.nearClipPlane=.05f; camera.farClipPlane=100;
            camera.targetTexture=target;
            var positions=new[] { new Vector3(0,0,10),new Vector3(10,1,2),new Vector3(7,6,9) };
            var names=new[] { "front","grazing","corner" };
            var directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../TestResults")); Directory.CreateDirectory(directory);
            for(int i=0;i<positions.Length;i++)
            {
                camera.orthographicSize=i==0 ? 2.4f : 3.35f;
                camera.transform.position=positions[i]; camera.transform.LookAt(Vector3.zero,Vector3.up); camera.Render();
                RenderTexture.active=target;
                var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply();
                File.WriteAllBytes(Path.Combine(directory,$"ribbon-{names[i]}.png"),image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            Debug.Log("Rendered surface ribbon QA: front, grazing, corner.");
        }
        finally
        {
            RenderTexture.active=previous; target.Release(); UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(black); UnityEngine.Object.DestroyImmediate(pale);
        }
    }
}
