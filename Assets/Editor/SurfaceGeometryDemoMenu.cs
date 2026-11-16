using System.IO;
using ArrowCube.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SurfaceGeometryDemoMenu
{
    [MenuItem("Arrow Cube/Phase 3/Open L-Shaped Demo")]
    public static void OpenL() => Open(false);
    [MenuItem("Arrow Cube/Phase 3/Open Oblique Prism Demo")]
    public static void OpenPrism() => Open(true);
    private static void Open(bool folded)
    {
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string path=folded ? "Assets/Scenes/SurfacePrism.unity" : "Assets/Scenes/SurfaceL.unity";
        if(File.Exists(path)) EditorSceneManager.OpenScene(path);
        else
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            CreateRoot(folded); EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),path);
        }
    }
    private static void CreateRoot(bool folded)
    {
        var root=new GameObject("Arrow Cube - Authored surface"); root.SetActive(false);
        root.AddComponent<SurfaceGeometryDemo>().FoldedPrism=folded; root.SetActive(true);
        var camera=new GameObject("Background camera").AddComponent<Camera>(); camera.depth=-10;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.14f,.22f,.34f); camera.transform.position=new Vector3(0,0,-20);
    }
    public static void CreateScenesForValidation()
    {
        if(!Application.isBatchMode) throw new System.InvalidOperationException("Use this in an isolated validation project.");
        Directory.CreateDirectory("Assets/Scenes");
        foreach(bool folded in new[] {false,true})
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); CreateRoot(folded);
            EditorSceneManager.SaveScene(scene,folded ? "Assets/Scenes/SurfacePrism.unity" : "Assets/Scenes/SurfaceL.unity");
        }
        AssetDatabase.SaveAssets();
    }
}
