using System.Collections.Generic;
using UnityEngine;

namespace ArrowCube.Presentation
{
    /// <summary>Camera-independent, bounded miter joins on a piecewise planar surface.</summary>
    public static class SurfaceStrokeGeometry
    {
        public static Vector3 Offset(IReadOnlyList<Vector3> points,IReadOnlyList<Vector3> segmentNormals,int index,float halfWidth)
        {
            if(points.Count<2) return Vector3.zero;
            if(index==0) return Side(points,segmentNormals,0)*halfWidth;
            if(index==points.Count-1) return Side(points,segmentNormals,index-1)*halfWidth;
            var incoming=Side(points,segmentNormals,index-1);
            var outgoing=Side(points,segmentNormals,index);
            var join=(incoming+outgoing).normalized;
            if(join.sqrMagnitude<.1f) return outgoing*halfWidth;
            // A fold's shared vertices lie on the intersection of BOTH patch planes.
            // Using the average side vector would lift one edge off a non-right-angle seam.
            if(Vector3.Dot(segmentNormals[index-1],segmentNormals[index])<.999f)
            {
                var edge=Vector3.Cross(segmentNormals[index-1],segmentNormals[index]).normalized;
                if(Vector3.Dot(edge,incoming+outgoing)<0) edge=-edge;
                float projection=Mathf.Min(Vector3.Dot(edge,incoming),Vector3.Dot(edge,outgoing));
                return edge*(halfWidth/Mathf.Max(.5f,projection));
            }
            float denominator=Vector3.Dot(join,outgoing);
            return join*(halfWidth/Mathf.Max(.5f,denominator)); // Maximum miter length is 2 * halfWidth.
        }

        private static Vector3 Side(IReadOnlyList<Vector3> points,IReadOnlyList<Vector3> normals,int segment)
        {
            var tangent=points[segment+1]-points[segment];
            if(tangent.sqrMagnitude<.0000000001f)
            {
                for(int i=0;i<points.Count-1;i++)
                    if((points[i+1]-points[i]).sqrMagnitude>.0000000001f)
                        return Vector3.Cross(normals[i],(points[i+1]-points[i]).normalized).normalized;
                return Vector3.zero;
            }
            return Vector3.Cross(normals[segment],tangent.normalized).normalized;
        }
    }
}
