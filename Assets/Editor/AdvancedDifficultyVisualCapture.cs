using System;
using System.IO;
using System.Reflection;
using ArrowCube.Controls;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Render the actual runtime UI in an isolated graphics-enabled batch Editor.</summary>
[InitializeOnLoad]
public static class AdvancedDifficultyVisualCapture
{
    private const string Key="ArrowCube.AdvancedVisualCapture";
    private static int phase,frames;
    private static double deadline;
    static AdvancedDifficultyVisualCapture() { EditorApplication.update+=Tick; }
    public static void Capture()
    {
        if(!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project for capture.");
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null) throw new InvalidOperationException("Capture requires graphics.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SetGameSize(1280,800);
        new GameObject("Advanced difficulty visual QA").AddComponent<Stage4ControlsDemo>();
        SessionState.SetBool(Key,true); EditorApplication.isPlaying=true;
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying) return;
        try
        {
            if(deadline==0) deadline=EditorApplication.timeSinceStartup+90;
            if(EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("Difficulty UI capture timed out.");
            var demo=UnityEngine.Object.FindFirstObjectByType<Stage4ControlsDemo>();
            if(demo==null || demo.Generation==null || demo.Generation.IsGenerating || demo.Generation.Generated==null) return;
            if(++frames<15) return;
            frames=0; demo.UpdateLayout(); Canvas.ForceUpdateCanvases();
            if(phase==0)
            {
                Save(demo,"difficulty-normal");
                foreach(var button in demo.GetComponentsInChildren<Button>()) if(button.name=="Show / hide advanced settings") button.onClick.Invoke();
            }
            else if(phase==1)
            {
                var scroll=demo.GetComponentInChildren<ScrollRect>(); scroll.verticalNormalizedPosition=.68f;
            }
            else if(phase==2)
            {
                Save(demo,"difficulty-advanced"); SetGameSize(600,1000);
            }
            else if(phase==3)
            {
                Save(demo,"difficulty-portrait");
                SessionState.SetBool(Key,false); Debug.Log("Captured normal, advanced, and portrait difficulty UI."); EditorApplication.Exit(0);
            }
            phase++;
        }
        catch(Exception error) { SessionState.SetBool(Key,false); Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static void Save(Stage4ControlsDemo demo,string name)
    {
        int width=Screen.width,height=Screen.height;
        var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32); target.Create();
        var previous=RenderTexture.active; var world=demo.Generation.Movement.InspectionCamera;
        var canvas=demo.GetComponentInChildren<Canvas>(); var mode=canvas.renderMode;
        var cameraObject=new GameObject("Temporary UI capture camera"); var ui=cameraObject.AddComponent<Camera>();
        ui.transform.position=new Vector3(0,0,-100); ui.orthographic=true; ui.nearClipPlane=.1f; ui.farClipPlane=10; ui.clearFlags=CameraClearFlags.Depth;
        try
        {
            RenderTexture.active=target; GL.Clear(true,true,new Color(.22f,.42f,.67f));
            world.targetTexture=target; world.Render();
            canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=ui; canvas.planeDistance=1;
            ui.targetTexture=target; Canvas.ForceUpdateCanvases(); ui.Render();
            RenderTexture.active=target;
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply();
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../TestResults")); Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory,name+".png"),texture.EncodeToPNG()); UnityEngine.Object.Destroy(texture);
        }
        finally
        {
            canvas.renderMode=mode; canvas.worldCamera=null; world.targetTexture=null; ui.targetTexture=null; ui.enabled=false;
            RenderTexture.active=previous; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(cameraObject);
        }
    }
    private static void SetGameSize(int width,int height)
    {
        // Screen.SetResolution does not resize the Editor's Game view. Set its isolated
        // fixed-resolution entry explicitly so portrait QA actually uses portrait pixels.
        var assembly=typeof(Editor).Assembly;
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var singleton=sizesType.GetProperty("instance",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.FlattenHierarchy).GetValue(null);
        var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType");
        var group=sizesType.GetMethod("GetGroup").Invoke(singleton,new[] { Enum.Parse(groupType,"Standalone") });
        var type=assembly.GetType("UnityEditor.GameViewSize"); var kind=assembly.GetType("UnityEditor.GameViewSizeType");
        var constructor=type.GetConstructor(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new[] { kind,typeof(int),typeof(int),typeof(string) },null);
        var size=constructor.Invoke(new object[] { Enum.Parse(kind,"FixedResolution"),width,height,"Difficulty QA "+width+"x"+height });
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new[] { size });
        int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
        var gameType=assembly.GetType("UnityEditor.GameView"); var game=EditorWindow.GetWindow(gameType);
        gameType.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(game,count-1);
        game.Repaint();
    }
}
