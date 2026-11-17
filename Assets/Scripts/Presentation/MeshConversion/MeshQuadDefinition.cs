using UnityEngine;

namespace ArrowCube.Presentation
{
    /// <summary>Four source vertex indices per outward-wound quad, in boundary order.
    /// For a triangulated mesh each group must cover exactly two of its triangles.</summary>
    [CreateAssetMenu(menuName="Arrow Cube/Mesh Quad Definition")]
    public sealed class MeshQuadDefinition : ScriptableObject
    {
        public Mesh sourceMesh;
        public int[] quadVertices=System.Array.Empty<int>();
    }
}
