using System.Collections.Generic;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using UnityEngine;

namespace ArrowCube.Controls
{
    /// <summary>Screen-space stroke picking with analytic cube occlusion; no path physics or colliders.</summary>
    public sealed class PathPicker
    {
        private readonly Stage2MovementDemo movement;
        private PuzzleDefinition cached;
        private readonly List<Stroke> strokes=new List<Stroke>();
        private readonly List<Head> heads=new List<Head>();
        private float cellSize;
        private sealed class Stroke { public int Id; public Vector3[] Points; }
        private sealed class Head { public int Id; public Vector3 Tip,Forward,Sideways; }
        public PathPicker(Stage2MovementDemo movement) { this.movement=movement; }

        public bool TryPick(Vector2 screenPoint,float tolerance,out int pathId)
        {
            pathId=-1;
            var camera=movement.InspectionCamera;
            if(camera==null || movement.Puzzle==null || movement.IsAnimating || !camera.pixelRect.Contains(screenPoint)) return false;
            if(!ReferenceEquals(cached,movement.Puzzle)) Rebuild();
            float best=float.PositiveInfinity,depth=float.PositiveInfinity;
            var scale=movement.transform.lossyScale;
            float worldRadius=movement.ArrowBodyWidth*.5f*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            foreach(var stroke in strokes)
            {
                if(!movement.Grid.Paths.ContainsKey(stroke.Id)) continue;
                for(int i=0;i<stroke.Points.Length;i++)
                {
                    var worldA=movement.transform.TransformPoint(stroke.Points[i]);
                    var worldB=movement.transform.TransformPoint(stroke.Points[Mathf.Min(i+1,stroke.Points.Length-1)]);
                    var a=camera.WorldToScreenPoint(worldA); var b=camera.WorldToScreenPoint(worldB);
                    if(a.z<camera.nearClipPlane || b.z<camera.nearClipPlane) continue;
                    Vector2 delta=(Vector2)(b-a);
                    float fraction=delta.sqrMagnitude<.0001f ? 0 : Mathf.Clamp01(Vector2.Dot(screenPoint-(Vector2)a,delta)/delta.sqrMagnitude);
                    var projected=Vector2.Lerp(a,b,fraction);
                    float squared=(projected-screenPoint).sqrMagnitude;
                    if(squared>best) continue;
                    float worldFraction=camera.orthographic ? fraction : fraction*a.z/((1-fraction)*b.z+fraction*a.z);
                    var point=Vector3.Lerp(worldA,worldB,worldFraction);
                    // Include the visible stroke width as well as the existing click/tap padding.
                    // Read it live so Inspector width changes do not require rebuilding the puzzle.
                    var edge=camera.WorldToScreenPoint(point+camera.transform.up*worldRadius);
                    float radius=Vector2.Distance(projected,edge);
                    float reach=Mathf.Max(0,tolerance)+radius;
                    if(squared>reach*reach) continue;
                    var ray=camera.ScreenPointToRay(projected);
                    float candidateDepth=Vector3.Dot(point-ray.origin,ray.direction);
                    if(candidateDepth<0 || IsOccluded(ray,candidateDepth)) continue;
                    if(squared<best-.001f || squared<=best+.001f && candidateDepth<depth)
                    { pathId=stroke.Id; best=squared; depth=candidateDepth; }
                }
            }
            foreach(var head in heads)
            {
                if(!movement.Grid.Paths.ContainsKey(head.Id)) continue;
                var center=head.Tip-head.Forward*TriangleArrowHead.Length(cellSize,movement.ArrowBodyWidth);
                var side=head.Sideways*(TriangleArrowHead.Width(cellSize,movement.ArrowBodyWidth)*.5f);
                var a=movement.transform.TransformPoint(head.Tip);
                var b=movement.transform.TransformPoint(center-side);
                var c=movement.transform.TransformPoint(center+side);
                var pa=camera.WorldToScreenPoint(a); var pb=camera.WorldToScreenPoint(b); var pc=camera.WorldToScreenPoint(c);
                if(pa.z<camera.nearClipPlane || pb.z<camera.nearClipPlane || pc.z<camera.nearClipPlane) continue;
                var nearest=ClosestTrianglePoint(screenPoint,pa,pb,pc);
                float squared=(nearest-screenPoint).sqrMagnitude;
                if(squared>Mathf.Max(0,tolerance)*Mathf.Max(0,tolerance) || squared>best) continue;
                var ray=camera.ScreenPointToRay(nearest);
                if(!new Plane(a,b,c).Raycast(ray,out float candidateDepth) || IsOccluded(ray,candidateDepth)) continue;
                if(squared<best-.001f || squared<=best+.001f && candidateDepth<depth)
                { pathId=head.Id; best=squared; depth=candidateDepth; }
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

        private bool IsOccluded(Ray ray,float candidateDepth)
        {
            var matrix=movement.transform.worldToLocalMatrix;
            var origin=matrix.MultiplyPoint3x4(ray.origin);
            var direction=matrix.MultiplyVector(ray.direction);
            float half=movement.SurfaceSideLength*.5f,near=0,far=float.PositiveInfinity;
            for(int axis=0;axis<3;axis++)
            {
                if(Mathf.Abs(direction[axis])<.000001f)
                { if(origin[axis]<-half || origin[axis]>half) return false; continue; }
                float a=(-half-origin[axis])/direction[axis],b=(half-origin[axis])/direction[axis];
                near=Mathf.Max(near,Mathf.Min(a,b)); far=Mathf.Min(far,Mathf.Max(a,b));
                if(near>far) return false;
            }
            return far>=0 && candidateDepth>near+.0005f;
        }

        private void Rebuild()
        {
            cached=movement.Puzzle; strokes.Clear(); heads.Clear();
            var geometry=new CubeSurfaceGeometry(cached.Topology,movement.SurfaceSideLength);
            cellSize=geometry.CellSize;
            var points=new List<Vector3>();
            foreach(var path in cached.Paths)
            {
                geometry.WritePolyline(path,points,geometry.CellSize*.04f);
                strokes.Add(new Stroke { Id=path.Id,Points=points.ToArray() });
                var head=points[0]; var forward=geometry.GetLocalDirection(path.Head.Face,path.HeadDirection);
                var normal=CubeSurfaceGeometry.ToVector(cached.Topology.GetOrientation(path.Head.Face).Normal);
                var sideways=Vector3.Cross(normal,forward);
                heads.Add(new Head { Id=path.Id,Tip=head,Forward=forward,Sideways=sideways });
            }
        }
    }
}
