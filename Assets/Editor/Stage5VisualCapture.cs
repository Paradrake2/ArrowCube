using System;
using System.IO;
using System.Reflection;
using ArrowCube.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class Stage5VisualCapture
{
    private const string Key="ArrowCube.Stage5Capture";
    private static int phase,frames;
    private static double deadline;
    static Stage5VisualCapture() { EditorApplication.update+=Tick; }
    public static void Capture()
    {
        SessionState.SetBool(Key,true); phase=frames=0; deadline=0;
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity"); Resize(1280,800); EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying) return;
        try
        {
            if(deadline==0) deadline=EditorApplication.timeSinceStartup+120;
            if(EditorApplication.timeSinceStartup>deadline) throw new TimeoutException("Stage 5 visual capture timed out.");
            var game=UnityEngine.Object.FindFirstObjectByType<GameplayController>(); var hud=UnityEngine.Object.FindFirstObjectByType<GameplayHUD>();
            if(game?.Player==null || hud?.Canvas==null || ++frames<20 || game.IsGenerating) return;
            frames=0; hud.UpdateLayout(); Canvas.ForceUpdateCanvases();
            if(phase==0) { Save(game,hud,"stage5-menu"); game.StartLevel(1); }
            else if(phase==1)
            {
                if(game.Session==null || game.Page!=GamePage.Game) throw new InvalidOperationException(game.Message);
                var options=game.Player.Options.Copy(); options.UnlimitedAttempts=false; game.ApplyOptions(options);
            }
            else if(phase==2) { Save(game,hud,"stage5-standard"); var options=game.Player.Options.Copy(); options.UnlimitedAttempts=true; game.ApplyOptions(options); }
            else if(phase==3) { Save(game,hud,"stage5-unlimited"); game.OpenSettings(); }
            else if(phase==4) { Save(game,hud,"stage5-settings"); game.CloseSettings(); Resize(600,1000); }
            else if(phase==5) { Save(game,hud,"stage5-portrait"); game.Menu(); }
            else if(phase==6)
            {
                Save(game,hud,"stage5-portrait-menu"); SessionState.SetBool(Key,false); EditorApplication.Exit(0);
            }
            phase++;
        }
        catch(Exception error) { SessionState.SetBool(Key,false); Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static void Resize(int width,int height) => typeof(AdvancedDifficultyVisualCapture)
        .GetMethod("SetGameSize",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[] { width,height });
    private static void Save(GameplayController game,GameplayHUD hud,string name)
    {
        int width=Screen.width,height=Screen.height;
        var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32); target.Create();
        var previous=RenderTexture.active; var world=game.Movement.InspectionCamera;
        var canvas=hud.Canvas; var mode=canvas.renderMode;
        var obj=new GameObject("Stage 5 UI capture camera"); var ui=obj.AddComponent<Camera>();
        ui.transform.position=new Vector3(0,0,-100); ui.orthographic=true; ui.nearClipPlane=.1f; ui.farClipPlane=10; ui.clearFlags=CameraClearFlags.Depth;
        try
        {
            RenderTexture.active=target; GL.Clear(true,true,new Color(.22f,.42f,.67f));
            if(world!=null) { world.targetTexture=target; world.Render(); }
            canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=ui; canvas.planeDistance=1;
            ui.targetTexture=target; Canvas.ForceUpdateCanvases(); ui.Render(); RenderTexture.active=target;
            var texture=new Texture2D(width,height,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply();
            Directory.CreateDirectory("TestResults"); File.WriteAllBytes("TestResults/"+name+".png",texture.EncodeToPNG()); UnityEngine.Object.Destroy(texture);
        }
        finally
        {
            canvas.renderMode=mode; canvas.worldCamera=null; if(world!=null) world.targetTexture=null;
            ui.targetTexture=null; ui.enabled=false; RenderTexture.active=previous; target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(obj);
        }
    }
}
