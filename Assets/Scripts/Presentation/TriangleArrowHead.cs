using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArrowCube.Presentation
{
    /// <summary>A filled triangle that can fold at route seams, joined to a thick line at its base.</summary>
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class TriangleArrowHead : MonoBehaviour
    {
        private Mesh mesh;
        private readonly List<Vector3> centers=new List<Vector3>(4);
        private readonly List<Vector3> vertices=new List<Vector3>(8);
        private readonly List<int> triangles=new List<int>(24);
        private readonly List<Vector3> normals=new List<Vector3>(4);
        private readonly List<Vector3> vertexNormals=new List<Vector3>(8);

        public static float Length(float cellSize,float bodyWidth) => Mathf.Max(cellSize*.35f,bodyWidth*.95f);
        public static float Width(float cellSize,float bodyWidth) => Mathf.Max(cellSize*.4f,bodyWidth*1.8f);

        public void Initialize(Material material)
        {
            mesh=new Mesh { name="Filled arrowhead" }; mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=GetComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        }

        public void DrawAndTrimBody(List<Vector3> body,List<Vector3> bodyNormals,Vector3 forward,Vector3 fallbackNormal,float cellSize,float bodyWidth)
        {
            float length=Length(cellSize,bodyWidth),halfWidth=Width(cellSize,bodyWidth)*.5f;
            centers.Clear(); normals.Clear(); centers.Add(body[0]);
            if(body.Count==1)
            {
                centers.Add(body[0]-forward*length); normals.Add(fallbackNormal); body.Clear(); bodyNormals.Clear();
            }
            else
            {
                float remaining=length; int index=0;
                while(index<body.Count-1)
                {
                    float segment=Vector3.Distance(body[index],body[index+1]);
                    if(segment>=remaining && segment>.000001f)
                    {
                        var baseCenter=Vector3.Lerp(body[index],body[index+1],remaining/segment);
                        centers.Add(baseCenter); normals.Add(bodyNormals[index]);
                        int remove=index;
                        if((baseCenter-body[index+1]).sqrMagnitude<.0000000001f) remove++;
                        if(remove>0) { body.RemoveRange(0,remove); bodyNormals.RemoveRange(0,remove); }
                        body[0]=baseCenter; break;
                    }
                    remaining-=segment; normals.Add(bodyNormals[index]); centers.Add(body[++index]);
                }
            }
            // At rest this is just three vertices. Extra rows preserve the filled
            // triangle's shape when its leading portion folds around a cube edge.
            vertices.Clear(); triangles.Clear(); vertexNormals.Clear(); vertices.Add(centers[0]); vertexNormals.Add(normals.Count>0 ? normals[0] : fallbackNormal); float travelled=0;
            for(int row=1;row<centers.Count;row++)
            {
                travelled+=Vector3.Distance(centers[row-1],centers[row]);
                float width=halfWidth*Mathf.Clamp01(travelled/length);
                var offset=SurfaceStrokeGeometry.Offset(centers,normals,row,width);
                vertices.Add(centers[row]-offset); vertices.Add(centers[row]+offset);
                var normal=row==centers.Count-1 ? normals[row-1] : (normals[row-1]+normals[row]).normalized;
                vertexNormals.Add(normal); vertexNormals.Add(normal);
                int left=vertices.Count-2,right=vertices.Count-1;
                if(row==1) AddTriangle(0,left,right);
                else { AddTriangle(left-2,left,right-2); AddTriangle(right-2,left,right); }
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.SetNormals(vertexNormals); mesh.RecalculateBounds();
        }

        private void AddTriangle(int a,int b,int c)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(c); triangles.Add(b); triangles.Add(a); // Visible from either side while wrapping/departing.
        }
        private void OnDestroy() { if(mesh!=null) { if(Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); } }
    }
}
