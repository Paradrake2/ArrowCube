using System;
using System.IO;
using System.Linq;
using ArrowCube.Game;
using ArrowCube.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MeshConversionDemoMenu
{
    [MenuItem("Arrow Cube/Phase 5/Open Converted Mesh Lab")]
    public static void Open()
    {
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        const string scene="Assets/Scenes/SurfaceMeshConversion.unity";
        if(!File.Exists(scene)) CreateAssets(); EditorSceneManager.OpenScene(scene);
    }
    [MenuItem("Arrow Cube/Phase 5/Create Missing Example Assets")]
    public static void CreateAssets()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory("Assets/Resources/ArrowCubeMeshInputs"); Directory.CreateDirectory("Assets/Resources/ArrowCubeSurfaces"); Directory.CreateDirectory("Assets/Scenes"); AssetDatabase.Refresh();
        var surfaces=new ConvertedSurfaceAsset[4];
        foreach(MeshConversionFixture kind in Enum.GetValues(typeof(MeshConversionFixture)))
        {
            string input="Assets/Resources/ArrowCubeMeshInputs/"+kind+".asset",output="Assets/Resources/ArrowCubeSurfaces/Converted-"+kind+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(input);
            if(mesh==null) { mesh=MeshConversionFixtures.Create(kind); AssetDatabase.CreateAsset(mesh,input); }
            var surface=AssetDatabase.LoadAssetAtPath<ConvertedSurfaceAsset>(output);
            if(surface==null) { surface=ScriptableObject.CreateInstance<ConvertedSurfaceAsset>(); surface.Initialize(MeshSurfaceConverter.Convert(mesh)); AssetDatabase.CreateAsset(surface,output); }
            surfaces[(int)kind]=surface;
        }
        AssetDatabase.SaveAssets();
        if(!File.Exists("Assets/Scenes/SurfaceMeshConversion.unity"))
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); SceneManager.SetActiveScene(scene); CreateRoot(surfaces,12345); EditorSceneManager.SaveScene(scene,"Assets/Scenes/SurfaceMeshConversion.unity");
        }
        var scenes=EditorBuildSettings.scenes.ToList(); if(!scenes.Any(s=>s.path=="Assets/Scenes/SurfaceMeshConversion.unity")) { scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/SurfaceMeshConversion.unity",true)); EditorBuildSettings.scenes=scenes.ToArray(); }
    }
    public static void CreateRoot(ConvertedSurfaceAsset[] surfaces,int seed)
    {
        var root=new GameObject("ArrowCube - Converted mesh lab"); root.SetActive(false); var demo=root.AddComponent<SurfaceGenerationDemo>(); demo.ConvertedExamples=surfaces; demo.ConvertedSurface=surfaces[0]; demo.Seed=seed; root.SetActive(true);
        var camera=new GameObject("Background camera").AddComponent<Camera>(); camera.depth=-10; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.14f,.22f,.34f); camera.transform.position=new Vector3(0,0,-20);
    }
    public static void CreateAssetsForValidation()
    { if(!Application.isBatchMode) throw new InvalidOperationException("Run this method in an isolated validation project."); CreateAssets(); }
}
