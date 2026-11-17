using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ArrowCube.Presentation
{
    /// <summary>Bounded sweep broad phase and conservative triangle intersection checks.
    /// Shared manifold edges/vertices are allowed; extra contacts and overlaps are rejected.</summary>
    internal static class MeshIntersectionValidation
    {
        private sealed class Triangle
        {
            public int A,B,C; public Vector3 Min,Max;
            public int Vertex(int i) => i==0 ? A : i==1 ? B : C;
        }
        public static void Validate(MeshSurfaceData data,CancellationToken token)
        {
            var triangles=new List<Triangle>();
            foreach(var f in data.Faces) for(int i=1;i+1<f.Vertices.Length;i++)
            {
                var t=new Triangle {A=f.Vertices[0],B=f.Vertices[i],C=f.Vertices[i+1]}; var a=data.Positions[t.A]; var b=data.Positions[t.B]; var c=data.Positions[t.C];
                t.Min=Vector3.Min(a,Vector3.Min(b,c)); t.Max=Vector3.Max(a,Vector3.Max(b,c)); triangles.Add(t);
            }
            triangles.Sort((a,b)=> { int c=a.Min.x.CompareTo(b.Min.x); if(c==0) c=a.A.CompareTo(b.A); if(c==0) c=a.B.CompareTo(b.B); if(c==0) c=a.C.CompareTo(b.C); return c; });
            int tests=0;
            for(int i=0;i<triangles.Count;i++) for(int j=i+1;j<triangles.Count;j++)
            {
                var a=triangles[i]; var b=triangles[j]; if(b.Min.x>a.Max.x+1e-6f) break;
                if(b.Min.y>a.Max.y+1e-6f || b.Max.y<a.Min.y-1e-6f || b.Min.z>a.Max.z+1e-6f || b.Max.z<a.Min.z-1e-6f) continue;
                if((++tests&255)==0) token.ThrowIfCancellationRequested();
                if(tests>2000000) throw new MeshConversionException("IntersectionBudget","Intersection validation exceeded its bounded work budget. Supply coarser quad patches or preprocess the mesh.");
                int shared=0; for(int x=0;x<3;x++) for(int y=0;y<3;y++) if(a.Vertex(x)==b.Vertex(y)) shared++;
                if(shared>=2) continue;
                var av=new[] {data.Positions[a.A],data.Positions[a.B],data.Positions[a.C]}; var bv=new[] {data.Positions[b.A],data.Positions[b.B],data.Positions[b.C]};
                if(Intersects(av,bv)) throw new MeshConversionException("SelfIntersection",$"Triangles ({a.A},{a.B},{a.C}) and ({b.A},{b.B},{b.C}) intersect away from their shared manifold boundary. Separate near-contact surfaces or repair self-intersections.",av[0],bv[0]);
            }
        }
        private static bool Intersects(Vector3[] a,Vector3[] b)
        {
            float scale=0; foreach(var p in a) foreach(var q in b) scale=Mathf.Max(scale,Vector3.Distance(p,q)); float epsilon=Mathf.Max(1e-7f,scale*1e-6f);
            var n=Vector3.Cross(a[1]-a[0],a[2]-a[0]).normalized; var m=Vector3.Cross(b[1]-b[0],b[2]-b[0]).normalized;
            bool Allowed(Vector3 p) { foreach(var x in a) foreach(var y in b) if((x-y).sqrMagnitude<=epsilon*epsilon && (p-x).sqrMagnitude<=epsilon*epsilon*64) return true; return false; }
            if(Mathf.Abs(Vector3.Dot(n,m))>.999999f && Mathf.Abs(Vector3.Dot(n,b[0]-a[0]))<=epsilon && Mathf.Abs(Vector3.Dot(n,b[1]-a[0]))<=epsilon && Mathf.Abs(Vector3.Dot(n,b[2]-a[0]))<=epsilon)
            {
                int axis=Mathf.Abs(n.x)>Mathf.Abs(n.y) ? (Mathf.Abs(n.x)>Mathf.Abs(n.z) ? 0 : 2) : (Mathf.Abs(n.y)>Mathf.Abs(n.z) ? 1 : 2);
                Vector2 P(Vector3 p) => axis==0 ? new Vector2(p.y,p.z) : axis==1 ? new Vector2(p.x,p.z) : new Vector2(p.x,p.y);
                for(int i=0;i<3;i++)
                {
                    if(Inside(P(a[i]),P(b[0]),P(b[1]),P(b[2]),epsilon) && !Allowed(a[i])) return true;
                    if(Inside(P(b[i]),P(a[0]),P(a[1]),P(a[2]),epsilon) && !Allowed(b[i])) return true;
                    for(int j=0;j<3;j++) if(Crossing(P(a[i]),P(a[(i+1)%3]),P(b[j]),P(b[(j+1)%3]),epsilon,out float t) && !Allowed(Vector3.Lerp(a[i],a[(i+1)%3],t))) return true;
                }
                return false;
            }
            for(int i=0;i<3;i++)
            {
                if(Hit(a[i],a[(i+1)%3],b,epsilon,out var p) && !Allowed(p)) return true;
                if(Hit(b[i],b[(i+1)%3],a,epsilon,out p) && !Allowed(p)) return true;
            }
            return false;
        }
        private static float Cross(Vector2 a,Vector2 b) => a.x*b.y-a.y*b.x;
        private static bool Inside(Vector2 p,Vector2 a,Vector2 b,Vector2 c,float epsilon)
        { float x=Cross(b-a,p-a),y=Cross(c-b,p-b),z=Cross(a-c,p-c); return x>=-epsilon && y>=-epsilon && z>=-epsilon || x<=epsilon && y<=epsilon && z<=epsilon; }
        private static bool Crossing(Vector2 a,Vector2 b,Vector2 c,Vector2 d,float epsilon,out float t)
        {
            var u=b-a; var v=d-c; float denominator=Cross(u,v); t=0;
            if(Mathf.Abs(denominator)<epsilon*epsilon) return false;
            t=Cross(c-a,v)/denominator; float s=Cross(c-a,u)/denominator;
            return t>=-epsilon && t<=1+epsilon && s>=-epsilon && s<=1+epsilon;
        }
        private static bool Hit(Vector3 from,Vector3 to,Vector3[] triangle,float epsilon,out Vector3 point)
        {
            point=default; var direction=to-from; var e1=triangle[1]-triangle[0]; var e2=triangle[2]-triangle[0]; var p=Vector3.Cross(direction,e2); float determinant=Vector3.Dot(e1,p);
            if(Mathf.Abs(determinant)<=Mathf.Max(1e-20f,direction.magnitude*e1.magnitude*e2.magnitude*1e-7f)) return false;
            float inverse=1/determinant; var t=from-triangle[0]; float u=Vector3.Dot(t,p)*inverse; if(u<-epsilon || u>1+epsilon) return false;
            var q=Vector3.Cross(t,e1); float v=Vector3.Dot(direction,q)*inverse; if(v<-epsilon || u+v>1+epsilon) return false;
            float distance=Vector3.Dot(e2,q)*inverse; if(distance<-epsilon || distance>1+epsilon) return false; point=from+direction*distance; return true;
        }
    }
}
