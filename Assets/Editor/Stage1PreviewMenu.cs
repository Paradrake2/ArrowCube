using UnityEditor;
using UnityEngine;

public static class Stage1PreviewMenu
{
    [MenuItem("Arrow Cube/Stage 1/Create Topology Preview")]
    public static void CreatePreview()
    {
        var preview = new GameObject("Arrow Cube - Stage 1 Topology");
        Undo.RegisterCreatedObjectUndo(preview, "Create topology preview");
        preview.AddComponent<GameManager>();
        preview.AddComponent<CubeTopologyPreview>();
        Selection.activeGameObject = preview;
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.drawGizmos = true;
            SceneView.lastActiveSceneView.Frame(new Bounds(preview.transform.position, Vector3.one * 5), false);
        }
    }
}
