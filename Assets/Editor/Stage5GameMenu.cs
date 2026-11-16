using System.IO;
using ArrowCube.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Stage5GameMenu
{
    [MenuItem("Arrow Cube/Stage 5/Open Gameplay Scene")]
    public static void OpenGame() => Open("Assets/Scenes/Game.unity");
    [MenuItem("Arrow Cube/Stage 5/Open Settings Lab")]
    public static void OpenLab() => Open("Assets/Scenes/TestScene.unity");
    private static void Open(string path)
    {
        if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path);
    }
    [MenuItem("Arrow Cube/Stage 5/Create Gameplay Demo")]
    public static void CreateDemo()
    {
        var root=new GameObject("Arrow Cube - Gameplay"); Undo.RegisterCreatedObjectUndo(root,"Create gameplay demo");
        root.AddComponent<GameplayController>(); root.AddComponent<GameplayHUD>(); Selection.activeGameObject=root;
    }
    // Run only in a fresh validation project. The authoring scene is copied back after verification.
    public static void CreateSceneForValidation()
    {
        Directory.CreateDirectory("Assets/Scenes"); Directory.CreateDirectory("Assets/Resources"); AssetDatabase.Refresh();
        var settings=AssetDatabase.LoadAssetAtPath<GameplaySettingsAsset>("Assets/Resources/GameplaySettings.asset");
        if(settings==null)
        {
            settings=ScriptableObject.CreateInstance<GameplaySettingsAsset>(); AssetDatabase.CreateAsset(settings,"Assets/Resources/GameplaySettings.asset");
        }
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("Arrow Cube - Gameplay"); var game=root.AddComponent<GameplayController>(); game.Settings=settings; root.AddComponent<GameplayHUD>();
        root.GetComponent<ArrowCube.Presentation.Stage2MovementDemo>().ArrowThickness=.35f;
        root.GetComponent<ArrowCube.Presentation.Stage2MovementDemo>().AnimationCellsPerSecond=40;
        var camera=new GameObject("Menu background camera").AddComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.22f,.42f,.67f); camera.transform.position=new Vector3(0,0,-20);
        camera.orthographic=true; camera.depth=-10; camera.gameObject.tag="MainCamera";
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/Game.unity"); AssetDatabase.SaveAssets();
        Debug.Log("Stage 5 gameplay scene created.");
    }
}
