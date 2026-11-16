using UnityEditor;
using UnityEngine;
using ArrowCube.Presentation;

public static class Stage2DemoMenu
{
    [MenuItem("Arrow Cube/Stage 2/Create Movement Demo")]
    public static void CreateDemo()
    {
        var demo = new GameObject("Arrow Cube - Stage 2 Movement Lab");
        Undo.RegisterCreatedObjectUndo(demo, "Create movement demo");
        demo.AddComponent<Stage2MovementDemo>();
        Selection.activeGameObject = demo;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.Frame(new Bounds(demo.transform.position, Vector3.one * 5), false);
    }
}
