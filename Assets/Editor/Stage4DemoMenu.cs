using ArrowCube.Controls;
using UnityEditor;
using UnityEngine;

public static class Stage4DemoMenu
{
    [MenuItem("Arrow Cube/Stage 4/Create Controls Demo")]
    public static void CreateDemo()
    {
        var root=new GameObject("Arrow Cube - Stage 4 Controls");
        Undo.RegisterCreatedObjectUndo(root,"Create controls demo"); root.AddComponent<Stage4ControlsDemo>(); Selection.activeGameObject=root;
        if(SceneView.lastActiveSceneView!=null) SceneView.lastActiveSceneView.Frame(new Bounds(root.transform.position,Vector3.one*5),false);
    }
}
