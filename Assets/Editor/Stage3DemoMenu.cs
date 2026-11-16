using ArrowCube.Presentation;
using UnityEditor;
using UnityEngine;

public static class Stage3DemoMenu
{
    [MenuItem("Arrow Cube/Stage 3/Create Generation Demo")]
    public static void CreateDemo()
    {
        var demo=new GameObject("Arrow Cube - Stage 3 Generation Lab");
        Undo.RegisterCreatedObjectUndo(demo,"Create generation demo");
        demo.AddComponent<Stage3GenerationDemo>(); Selection.activeGameObject=demo;
        if(SceneView.lastActiveSceneView!=null)
            SceneView.lastActiveSceneView.Frame(new Bounds(demo.transform.position,Vector3.one*5),false);
    }
}
