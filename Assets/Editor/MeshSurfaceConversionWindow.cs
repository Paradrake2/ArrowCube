using System;
using System.Collections.Generic;
using System.IO;
using ArrowCube.Game;
using ArrowCube.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class MeshSurfaceConversionWindow : EditorWindow
{
    [SerializeField] private Mesh source;
    [SerializeField] private MeshFilter sourceFilter;
    [SerializeField] private bool bakeTransform;
    [SerializeField] private MeshQuadDefinition companion;
    [SerializeField] private MeshConversionSettings settings=new MeshConversionSettings();
    [SerializeField] private int seed=12345;
    private ConvertedMeshSurface result;
    private ConvertedSurfaceAsset saved;
    private Mesh ownedObj;
    private MeshConversionException failure;
    private string message;
    private Vector2 scroll;
    private bool ids,directions,normals,connections;
    private int maximumDrawn=500;
    private GameObject preview,hiddenSource;
    private readonly List<Material> materials=new List<Material>();
    [MenuItem("Arrow Cube/Phase 5/Mesh Conversion Tool")]
    public static void Open() => GetWindow<MeshSurfaceConversionWindow>("ArrowCube Mesh Converter").Show();
    public static void Inspect(ConvertedSurfaceAsset asset)
    { var window=GetWindow<MeshSurfaceConversionWindow>("ArrowCube Mesh Converter"); window.source=null; window.sourceFilter=null; window.companion=null; window.saved=asset; window.result=asset.Build(); window.message=asset.Summary; window.failure=null; window.CreatePreview(); window.Show(); }
    private void OnEnable() { SceneView.duringSceneGui+=Draw; EditorApplication.playModeStateChanged+=PlayChanged; }
    private void OnDisable() { SceneView.duringSceneGui-=Draw; EditorApplication.playModeStateChanged-=PlayChanged; ClearPreview(); if(ownedObj!=null) DestroyImmediate(ownedObj); }
    private void PlayChanged(PlayModeStateChange state) { if(state==PlayModeStateChange.ExitingEditMode) ClearPreview(); }
    private void OnGUI()
    {
        scroll=EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Convert a closed quad-compatible surface",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Native quads or quad OBJ are the reliable input path. Triangle pairing must have exactly one complete solution. Red diagnostics identify the first invalid/unmatched boundary. Open, intersecting or non-manifold surfaces need preprocessing.",MessageType.Info);
        EditorGUI.BeginChangeCheck();
        source=(Mesh)EditorGUILayout.ObjectField("Source mesh",source,typeof(Mesh),false);
        sourceFilter=(MeshFilter)EditorGUILayout.ObjectField("Optional MeshFilter",sourceFilter,typeof(MeshFilter),true);
        bakeTransform=EditorGUILayout.Toggle("Bake filter world transform",bakeTransform);
        companion=(MeshQuadDefinition)EditorGUILayout.ObjectField("Optional quad definition",companion,typeof(MeshQuadDefinition),false);
        var serialized=new SerializedObject(this); serialized.Update(); EditorGUILayout.PropertyField(serialized.FindProperty("settings"),true); serialized.ApplyModifiedProperties();
        if(EditorGUI.EndChangeCheck()) { result=null; saved=null; failure=null; ClearPreview(); }
        if(GUILayout.Button("Use selected MeshFilter"))
        { var selected=Selection.activeGameObject==null ? null : Selection.activeGameObject.GetComponent<MeshFilter>(); if(selected!=null) { sourceFilter=selected; source=selected.sharedMesh; result=null; saved=null; ClearPreview(); } }
        if(GUILayout.Button("Load explicit-quad OBJ?")) LoadObj();
        using(new EditorGUI.DisabledScope(EditorApplication.isPlaying)) if(GUILayout.Button("Convert & validate")) Convert();
        if(failure!=null) EditorGUILayout.HelpBox(failure.Message,MessageType.Error);
        else if(!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message,MessageType.Info);
        if(result!=null)
        {
            EditorGUILayout.LabelField("Shape ID",result.Shape.Id,EditorStyles.wordWrappedLabel);
            foreach(string warning in result.Report.Warnings) EditorGUILayout.HelpBox(warning,MessageType.Warning);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if(GUILayout.Button("Save converted surface?")) Save();
                seed=EditorGUILayout.IntField("Puzzle seed",seed);
                if(GUILayout.Button("Play generated puzzle (standard controls)")) Play();
            }
        }
        EditorGUILayout.Space(); EditorGUILayout.LabelField("Scene view debug overlay",EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck(); ids=EditorGUILayout.Toggle("Cell IDs",ids); directions=EditorGUILayout.Toggle("Four cell directions",directions); normals=EditorGUILayout.Toggle("Surface normals",normals); connections=EditorGUILayout.Toggle("Seam connectivity",connections);
        maximumDrawn=EditorGUILayout.IntSlider("Maximum debug cells",maximumDrawn,1,4000);
        if(EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
        EditorGUILayout.HelpBox("Gray: coplanar. Green: convex. Magenta: concave. Gold ticks: ordinary exits allowed. Cyan: normal / connection. Red: invalid boundary. Debug overlays are limited to the first selected count of cells.",MessageType.None);
        EditorGUILayout.EndScrollView();
    }
    private void Convert()
    {
        ClearPreview(); saved=null; result=null; failure=null;
        try
        {
            if(sourceFilter!=null && sourceFilter.sharedMesh!=source) throw new MeshConversionException("Input","The selected MeshFilter must reference the source mesh.");
            var matrix=bakeTransform && sourceFilter!=null ? sourceFilter.transform.localToWorldMatrix : Matrix4x4.identity;
            result=MeshSurfaceConverter.Convert(source,matrix,settings,companion); message=result.Report.ToString(); CreatePreview();
        }
        catch(MeshConversionException error) { failure=error; message=null; SceneView.RepaintAll(); }
    }
    private void LoadObj()
    {
        string path=EditorUtility.OpenFilePanel("Explicit-quad OBJ","","obj"); if(string.IsNullOrEmpty(path)) return;
        try
        {
            var loaded=QuadObjReader.Read(File.ReadAllText(path),Path.GetFileNameWithoutExtension(path)); if(ownedObj!=null) DestroyImmediate(ownedObj);
            ownedObj=loaded; source=loaded; sourceFilter=null; companion=null; result=null; saved=null; failure=null; ClearPreview(); Convert();
        }
        catch(Exception error) when(error is ArgumentException || error is IOException) { failure=error as MeshConversionException??new MeshConversionException("OBJ",error.Message); }
    }
    private void CreatePreview()
    {
        ClearPreview(); if(result==null || EditorApplication.isPlaying) return;
        preview=new GameObject("ArrowCube conversion preview") {hideFlags=HideFlags.HideAndDontSave}; var pool=new Dictionary<Color,Material>(); var assigned=new Material[result.Geometry.RenderQuads.Count];
        for(int i=0;i<assigned.Length;i++)
        { var color=result.Geometry.RenderQuads[i].Color; if(!pool.TryGetValue(color,out var m)) { m=PuzzleMaterials.Create(color); m.hideFlags=HideFlags.HideAndDontSave; pool.Add(color,m); materials.Add(m); } assigned[i]=m; }
        preview.AddComponent<SurfaceMeshRenderer>().Initialize(result.Geometry,assigned);
        if(sourceFilter!=null && !SceneVisibilityManager.instance.IsHidden(sourceFilter.gameObject)) { hiddenSource=sourceFilter.gameObject; SceneVisibilityManager.instance.Hide(hiddenSource,false); }
        if(SceneView.lastActiveSceneView!=null) SceneView.lastActiveSceneView.Frame(result.Geometry.Bounds,false); SceneView.RepaintAll();
    }
    private void ClearPreview() { if(hiddenSource!=null) SceneVisibilityManager.instance.Show(hiddenSource,false); hiddenSource=null; if(preview!=null) DestroyImmediate(preview); preview=null; foreach(var m in materials) if(m!=null) DestroyImmediate(m); materials.Clear(); }
    private void Draw(SceneView view)
    { if(EditorApplication.isPlaying) return; if(result!=null) MeshSurfaceDebugDrawing.Draw(result,ids,directions,normals,connections,maximumDrawn); else if(failure!=null) { MeshSurfaceDebugDrawing.Source(source,bakeTransform && sourceFilter!=null ? sourceFilter.transform.localToWorldMatrix : Matrix4x4.identity,maximumDrawn); MeshSurfaceDebugDrawing.Error(failure); } }
    private void Save()
    {
        string path=EditorUtility.SaveFilePanelInProject("Save self-contained converted surface","ConvertedSurface","asset","Store under Assets/Resources/ArrowCubeSurfaces for automatic save restoration.","Assets/Resources/ArrowCubeSurfaces");
        if(string.IsNullOrEmpty(path)) return;
        var existing=AssetDatabase.LoadAssetAtPath<ConvertedSurfaceAsset>(path);
        if(existing==null) { existing=CreateInstance<ConvertedSurfaceAsset>(); existing.Initialize(result); AssetDatabase.CreateAsset(existing,path); }
        else { Undo.RecordObject(existing,"Bake converted surface"); existing.Initialize(result); EditorUtility.SetDirty(existing); }
        AssetDatabase.SaveAssets(); saved=existing; Selection.activeObject=saved;
    }
    private void Play()
    {
        if(saved==null) Save(); if(saved==null) return;
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ClearPreview(); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        MeshConversionDemoMenu.CreateRoot(new[] {saved},seed); EditorApplication.isPlaying=true;
    }
}
[CustomEditor(typeof(ConvertedSurfaceAsset))]
public sealed class ConvertedSurfaceInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var asset=(ConvertedSurfaceAsset)target;
        try { var result=asset.Build(); EditorGUILayout.HelpBox(asset.Summary,MessageType.Info); EditorGUILayout.SelectableLabel(result.Shape.Id,EditorStyles.wordWrappedLabel,GUILayout.Height(38)); if(GUILayout.Button("Inspect converted surface")) MeshSurfaceConversionWindow.Inspect(asset); }
        catch(ArgumentException error) { EditorGUILayout.HelpBox(error.Message,MessageType.Error); }
    }
}
