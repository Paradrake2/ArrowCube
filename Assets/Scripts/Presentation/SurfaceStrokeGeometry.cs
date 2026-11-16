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
            // A fold shares the cube-edge tangent; it needs no in-plane corner enlargement.
            if(Vector3.Dot(segmentNormals[index-1],segmentNormals[index])<.999f) return join*halfWidth;
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
