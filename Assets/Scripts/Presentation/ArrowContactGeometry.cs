using UnityEngine;

namespace ArrowCube.Presentation
{
    /// <summary>Finds where a moving tip first touches the blocker's visible ribbon or triangle.</summary>
    public static class ArrowContactGeometry
    {
        public static float FirstContact(Vector3 from,Vector3 to,Mesh mesh)
        {
            var vertices=mesh.vertices; var triangles=mesh.triangles;
            float first=float.PositiveInfinity;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=vertices[triangles[i]]; var b=vertices[triangles[i+1]]; var c=vertices[triangles[i+2]];
                var normal=Vector3.Cross(b-a,c-a);
                if(normal.sqrMagnitude<1e-16f) continue;
                normal.Normalize();
                float start=Vector3.Dot(normal,from-a),end=Vector3.Dot(normal,to-a);
                float enter=0,leave=1;
                if(Mathf.Abs(start)>1e-5f || Mathf.Abs(end)>1e-5f)
                {
                    float change=start-end;
                    if(Mathf.Abs(change)<1e-8f) continue;
                    enter=leave=start/change;
                    if(enter<0 || enter>1) continue;
                }
                if(Clip(a,b,normal,from,to,ref enter,ref leave)
                    && Clip(b,c,normal,from,to,ref enter,ref leave)
                    && Clip(c,a,normal,from,to,ref enter,ref leave)) first=Mathf.Min(first,enter);
            }
            return first;
        }

        private static bool Clip(Vector3 a,Vector3 b,Vector3 normal,Vector3 from,Vector3 to,ref float enter,ref float leave)
        {
            var inward=Vector3.Cross(normal,b-a);
            float start=Vector3.Dot(inward,from-a),change=Vector3.Dot(inward,to-from);
            if(Mathf.Abs(change)<1e-10f) return start>=-1e-8f;
            float crossing=-start/change;
            if(change>0) enter=Mathf.Max(enter,crossing); else leave=Mathf.Min(leave,crossing);
            return enter<=leave;
        }
    }
}
