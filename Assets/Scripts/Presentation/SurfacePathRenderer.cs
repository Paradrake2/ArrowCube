using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArrowCube.Presentation
{
    /// <summary>A fixed-width surface ribbon: flat ends, shared miter vertices, no camera-facing billboards.</summary>
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class SurfacePathRenderer : MonoBehaviour
    {
        private Mesh mesh;
        private readonly List<Vector3> positions=new List<Vector3>();
        private readonly List<Vector3> vertices=new List<Vector3>();
        private readonly List<int> triangles=new List<int>();
        public Material SharedMaterial => GetComponent<MeshRenderer>().sharedMaterial;
        public float Width { get; private set; }
        public int PositionCount => positions.Count;
        public Vector3 GetPosition(int index) => positions[index];

        public void Initialize(Material material)
        {
            mesh=new Mesh { name="Surface arrow ribbon" }; mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=GetComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        }
        public void Draw(IReadOnlyList<Vector3> points,IReadOnlyList<Vector3> segmentNormals,float width)
        {
            if(segmentNormals.Count!=Mathf.Max(0,points.Count-1)) throw new ArgumentException("A surface normal is required for every ribbon segment.");
            Width=width; positions.Clear(); vertices.Clear(); triangles.Clear();
            for(int i=0;i<points.Count;i++) positions.Add(points[i]);
            if(points.Count>=2)
                for(int i=0;i<points.Count;i++)
                {
                    var offset=SurfaceStrokeGeometry.Offset(points,segmentNormals,i,width*.5f);
                    vertices.Add(points[i]-offset); vertices.Add(points[i]+offset);
                    if(i==0) continue;
                    int a=(i-1)*2,b=i*2;
                    AddTriangle(a,b,a+1); AddTriangle(a+1,b,b+1);
                }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }
        private void AddTriangle(int a,int b,int c)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(c); triangles.Add(b); triangles.Add(a);
        }
        private void OnDestroy()
        { if(mesh!=null) { if(Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); } }
    }
}
