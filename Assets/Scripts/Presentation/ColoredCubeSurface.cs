using System;
using ArrowCube.Topology;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArrowCube.Presentation
{
    /// <summary>Six opaque, evenly visible face colors, independent of scene lighting.</summary>
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class ColoredCubeSurface : MonoBehaviour
    {
        private Mesh mesh;
        public static Color FaceColor(CubeFace face)
        {
            switch(face)
            {
                case CubeFace.Front: return new Color(.72f,.84f,.98f);
                case CubeFace.Right: return new Color(.62f,.83f,.72f);
                case CubeFace.Back: return new Color(.99f,.76f,.63f);
                case CubeFace.Left: return new Color(.82f,.70f,.95f);
                case CubeFace.Top: return new Color(1f,.90f,.64f);
                case CubeFace.Bottom: return new Color(.94f,.69f,.80f);
                default: throw new ArgumentOutOfRangeException(nameof(face));
            }
        }

        public void Initialize(CubeTopology topology,float sideLength,Material[] materials)
        {
            if(materials==null || materials.Length!=6) throw new ArgumentException("One material per cube face is required.",nameof(materials));
            var vertices=new Vector3[24]; var normals=new Vector3[24];
            mesh=new Mesh { name="Colored cube faces",subMeshCount=6 };
            for(int face=0;face<6;face++)
            {
                var frame=topology.GetOrientation((CubeFace)face);
                var normal=CubeSurfaceGeometry.ToVector(frame.Normal);
                var center=normal*(sideLength*.5f);
                var right=CubeSurfaceGeometry.ToVector(frame.Right)*(sideLength*.5f);
                var up=CubeSurfaceGeometry.ToVector(frame.Up)*(sideLength*.5f);
                int first=face*4;
                vertices[first]=center-right-up; vertices[first+1]=center+right-up;
                vertices[first+2]=center+right+up; vertices[first+3]=center-right+up;
                for(int i=0;i<4;i++) normals[first+i]=normal;
            }
            mesh.vertices=vertices; mesh.normals=normals;
            for(int face=0;face<6;face++)
            {
                int first=face*4;
                mesh.SetTriangles(new[] { first,first+1,first+2,first,first+2,first+3 },face);
            }
            mesh.RecalculateBounds(); GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=GetComponent<MeshRenderer>(); renderer.sharedMaterials=materials;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        }

        private void OnDestroy()
        { if(mesh!=null) { if(Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); } }
    }
}
