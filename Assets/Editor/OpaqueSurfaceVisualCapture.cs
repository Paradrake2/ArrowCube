using System;
using System.Collections.Generic;
using System.IO;
using ArrowCube.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>GPU regression for the old sprite fallback and the fixed depth-writing material.</summary>
public static class OpaqueSurfaceVisualCapture
{
    public static void BuildMobileWeb()
    {
        var mobile=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        if(mobile==null) throw new InvalidOperationException("Copy the Mobile URP settings into the isolated validation project first.");
        GraphicsSettings.defaultRenderPipeline=mobile; QualitySettings.renderPipeline=mobile;
        AssetDatabase.SaveAssets(); WebGenerationBuild.Build();
    }
    public static void Capture()
    {
        if(!Application.isBatchMode || SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)
            throw new InvalidOperationException("Run this graphics capture in an isolated graphics-enabled batch project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var mobile=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        if(mobile!=null) { GraphicsSettings.defaultRenderPipeline=mobile; QualitySettings.renderPipeline=mobile; }
        string pipeline=mobile==null ? "builtin" : "urp";
        string directory=Path.GetFullPath("TestResults/OpaqueRendering"); Directory.CreateDirectory(directory);
        var root=new GameObject("Opaque render regression");
        var camera=new GameObject("Regression camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform,false);
        camera.orthographic=true; camera.orthographicSize=3.6f; camera.nearClipPlane=.05f; camera.farClipPlane=100;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.22f,.42f,.67f);
        var target=new RenderTexture(512,512,16,RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture=target;
        var cube=GameObject.CreatePrimitive(PrimitiveType.Cube); cube.transform.SetParent(root.transform,false); cube.transform.localScale=Vector3.one*4;
        var body=new GameObject("Cross-face ribbon").AddComponent<SurfacePathRenderer>(); body.transform.SetParent(root.transform,false);
        var points=new List<Vector3> { new Vector3(-1,1,2.02f),new Vector3(-1,-1,2.02f),new Vector3(-2.02f,-1,2.02f),
            new Vector3(-2.02f,-1,-2.02f),new Vector3(1,-1,-2.02f) };
        var normals=new[] { Vector3.forward,Vector3.forward,Vector3.left,Vector3.back };
        var materials=new List<Material>(); var previous=RenderTexture.active;
        var report=new System.Text.StringBuilder("pipeline,material,angle,front_r,front_g,front_b\n");
        try
        {
            var angles=new[] { new Vector3(7,6,9),new Vector3(-7,6,9),new Vector3(2,-6,9) };
            foreach(bool opaque in mobile==null ? new[] { false,true } : new[] { true })
            {
                var pale=opaque ? PuzzleMaterials.Create(new Color(.83f,.92f,1)) : new Material(Shader.Find("Sprites/Default")) { color=new Color(.83f,.92f,1) };
                var black=opaque ? PuzzleMaterials.Create(Color.black) : new Material(Shader.Find("Sprites/Default")) { color=Color.black };
                materials.Add(pale); materials.Add(black); cube.GetComponent<Renderer>().sharedMaterial=pale;
                body.Initialize(black); body.Draw(points,normals,.2f);
                for(int angle=0;angle<angles.Length;angle++)
                {
                    camera.transform.position=angles[angle]; camera.transform.LookAt(Vector3.zero);
                    if(mobile==null) camera.Render();
                    else RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest { destination=target });
                    RenderTexture.active=target;
                    var image=new Texture2D(512,512,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,512,512),0,0); image.Apply();
                    string name=opaque ? "opaque" : "sprite-fallback";
                    File.WriteAllBytes(Path.Combine(directory,$"{pipeline}-{name}-{angle}.png"),image.EncodeToPNG());
                    var projected=camera.WorldToScreenPoint(new Vector3(-1,0,2.02f));
                    var pixel=image.GetPixel(Mathf.RoundToInt(projected.x),Mathf.RoundToInt(projected.y));
                    report.AppendLine(FormattableString.Invariant($"{pipeline},{name},{angle},{pixel.r:F3},{pixel.g:F3},{pixel.b:F3}"));
                    UnityEngine.Object.DestroyImmediate(image);
                    if(opaque && (pixel.r>.1f || pixel.g>.1f || pixel.b>.1f))
                        throw new InvalidOperationException($"Visible front ribbon disappeared at angle {angle}: {pixel}.");
                }
            }
            File.WriteAllText(Path.Combine(directory,$"{pipeline}-pixels.csv"),report.ToString());
            Debug.Log("Opaque surface GPU checks passed: "+pipeline);
        }
        finally
        {
            RenderTexture.active=previous; camera.targetTexture=null; target.Release(); UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(root); foreach(var material in materials) UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
