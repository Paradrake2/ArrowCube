using ArrowCube.Presentation;
using UnityEngine;

namespace ArrowCube.Controls
{
    /// <summary>Picks the actual ribbon/head triangles and tests occlusion against the actual surface quads.</summary>
    public sealed class PathPicker
    {
        private readonly Stage2MovementDemo movement;
        public PathPicker(Stage2MovementDemo movement) { this.movement=movement; }
        public bool TryPick(Vector2 screenPoint,float tolerance,out int pathId)
        {
            pathId=-1; var camera=movement.InspectionCamera;
            if(camera==null || movement.Puzzle==null || movement.IsAnimating || !camera.pixelRect.Contains(screenPoint)) return false;
            float best=float.PositiveInfinity,depth=float.PositiveInfinity,reach=Mathf.Max(0,tolerance);
            var inverse=movement.transform.worldToLocalMatrix;
            var toWorld=movement.transform.localToWorldMatrix;
            foreach(var path in movement.Puzzle.Paths)
            {
                if(!movement.Grid.Paths.ContainsKey(path.Id)) continue;
                var root=movement.GetArrowVisual(path.Id); if(root==null) continue;
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh=filter.sharedMesh; if(mesh==null) continue;
                    var vertices=mesh.vertices; var triangles=mesh.triangles;
                    var toLocal=inverse*filter.transform.localToWorldMatrix;
                    var projected=new Vector3[vertices.Length];
                    for(int v=0;v<vertices.Length;v++)
                    { vertices[v]=toLocal.MultiplyPoint3x4(vertices[v]); projected[v]=camera.WorldToScreenPoint(toWorld.MultiplyPoint3x4(vertices[v])); }
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        var a=vertices[triangles[i]]; var b=vertices[triangles[i+1]]; var c=vertices[triangles[i+2]];
                        var pa=projected[triangles[i]]; var pb=projected[triangles[i+1]]; var pc=projected[triangles[i+2]];
                        if(pa.z<camera.nearClipPlane || pb.z<camera.nearClipPlane || pc.z<camera.nearClipPlane) continue;
                        if(screenPoint.x<Mathf.Min(pa.x,Mathf.Min(pb.x,pc.x))-reach || screenPoint.x>Mathf.Max(pa.x,Mathf.Max(pb.x,pc.x))+reach
                            || screenPoint.y<Mathf.Min(pa.y,Mathf.Min(pb.y,pc.y))-reach || screenPoint.y>Mathf.Max(pa.y,Mathf.Max(pb.y,pc.y))+reach) continue;
                        var nearest=ClosestTrianglePoint(screenPoint,pa,pb,pc); float squared=(nearest-screenPoint).sqrMagnitude;
                        if(squared>reach*reach+.00001f || squared>best+.001f) continue;
                        var worldRay=camera.ScreenPointToRay(nearest);
                        // Recover the nearest point with perspective-correct barycentrics.
                        // Ray/triangle intersection at an exact arrow tip is numerically fragile
                        // after nonuniform transforms, despite an already valid screen-space hit.
                        float area=Cross((Vector2)pb-(Vector2)pa,(Vector2)pc-(Vector2)pa);
                        if(Mathf.Abs(area)<.000001f) continue;
                        float wa=Mathf.Max(0,Cross((Vector2)pb-nearest,(Vector2)pc-nearest)/area);
                        float wb=Mathf.Max(0,Cross((Vector2)pc-nearest,(Vector2)pa-nearest)/area);
                        float wc=Mathf.Max(0,1-wa-wb);
                        if(!camera.orthographic) { wa/=pa.z; wb/=pb.z; wc/=pc.z; }
                        var point=(a*wa+b*wb+c*wc)/(wa+wb+wc);
                        var origin=inverse.MultiplyPoint3x4(worldRay.origin);
                        var ray=new Ray(origin,point-origin); float candidateDepth=Vector3.Distance(point,origin);
                        if(movement.SurfaceGeometry.Raycast(ray,out float surfaceDepth) && surfaceDepth+Mathf.Max(.0005f,movement.SurfaceGeometry.ReferenceCellSize*.002f)<candidateDepth) continue;
                        if(squared<best-.001f || squared<=best+.001f && candidateDepth<depth)
                        { best=squared; depth=candidateDepth; pathId=path.Id; }
                    }
                }
            }
            return pathId>=0;
        }
        private static Vector2 ClosestTrianglePoint(Vector2 point,Vector2 a,Vector2 b,Vector2 c)
        {
            float x=Cross(b-a,point-a),y=Cross(c-b,point-b),z=Cross(a-c,point-c);
            if(Mathf.Abs(Cross(b-a,c-a))>.0001f && (x>=0 && y>=0 && z>=0 || x<=0 && y<=0 && z<=0)) return point;
            var ab=ClosestEdge(point,a,b); var bc=ClosestEdge(point,b,c); var ca=ClosestEdge(point,c,a);
            var closest=(ab-point).sqrMagnitude<(bc-point).sqrMagnitude ? ab : bc;
            return (closest-point).sqrMagnitude<(ca-point).sqrMagnitude ? closest : ca;
        }
        private static float Cross(Vector2 a,Vector2 b) => a.x*b.y-a.y*b.x;
        private static Vector2 ClosestEdge(Vector2 point,Vector2 a,Vector2 b)
        { var delta=b-a; return a+delta*(delta.sqrMagnitude<.0001f ? 0 : Mathf.Clamp01(Vector2.Dot(point-a,delta)/delta.sqrMagnitude)); }
    }
}
