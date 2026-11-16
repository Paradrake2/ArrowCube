using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArrowCube.Presentation
{
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class SurfaceMeshRenderer : MonoBehaviour
    {
        private Mesh mesh;
        public void Initialize(ISurfaceGeometry geometry,Material[] materials)
        {
            int count=geometry.RenderQuads.Count;
            if(materials==null || materials.Length!=count) throw new ArgumentException("One material per rendering quad is required.");
            var vertices=new Vector3[count*4]; var normals=new Vector3[count*4];
            mesh=new Mesh {name="Opaque surface quads",indexFormat=count*4>65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,subMeshCount=count};
            for(int i=0;i<count;i++) for(int v=0;v<4;v++) { vertices[i*4+v]=geometry.RenderQuads[i].Corner(v); normals[i*4+v]=geometry.RenderQuads[i].Normal; }
            mesh.vertices=vertices; mesh.normals=normals;
            for(int i=0;i<count;i++) { int v=i*4; mesh.SetTriangles(new[] {v,v+1,v+2,v,v+2,v+3},i); }
            mesh.RecalculateBounds(); GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=GetComponent<MeshRenderer>(); renderer.sharedMaterials=materials;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        }
        private void OnDestroy() { if(mesh!=null) { if(Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); } }
    }
}
