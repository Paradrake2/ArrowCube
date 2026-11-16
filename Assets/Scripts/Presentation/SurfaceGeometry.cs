using System;
using System.Collections.Generic;
using ArrowCube.Topology;
using UnityEngine;

namespace ArrowCube.Presentation
{
    public interface ISurfaceGeometry
    {
        ISurfaceTopology Surface { get; }
        Bounds Bounds { get; }
        float ReferenceCellSize { get; }
        SurfaceQuad GetCell(int cellId);
        IReadOnlyList<SurfaceQuad> RenderQuads { get; }
    }

    /// <summary>One convex planar quadrilateral, ordered bottom-left, bottom-right,
    /// top-right, top-left as viewed from its outward side. Coordinates are root-local.</summary>
    public sealed class SurfaceQuad
    {
        private readonly Vector3[] corners;
        public Vector3 Center { get; }
        public Vector3 Normal { get; }
        public Color Color { get; }
        public float Size { get; }
        public SurfaceQuad(Vector3 bottomLeft,Vector3 bottomRight,Vector3 topRight,Vector3 topLeft,Color color)
        {
            corners=new[] {bottomLeft,bottomRight,topRight,topLeft}; Color=color;
            foreach(var p in corners) if(!Finite(p)) throw new ArgumentException("Quad coordinates must be finite.");
            Center=(bottomLeft+bottomRight+topRight+topLeft)*.25f;
            var n=Vector3.Cross(bottomRight-bottomLeft,topLeft-bottomLeft);
            if(n.magnitude<1e-20f) throw new ArgumentException("Degenerate quad."); Normal=n/n.magnitude;
            float scale=Mathf.Max((topRight-bottomLeft).magnitude,(topLeft-bottomRight).magnitude);
            Size=float.PositiveInfinity;
            for(int i=0;i<4;i++)
            {
                var edge=corners[(i+1)%4]-corners[i]; var next=corners[(i+2)%4]-corners[(i+1)%4];
                if(edge.sqrMagnitude<scale*scale*1e-12f || Vector3.Dot(Vector3.Cross(edge,next),Normal)<=scale*scale*1e-7f
                    || Mathf.Abs(Vector3.Dot(corners[i]-bottomLeft,Normal))>Mathf.Max(1e-5f,scale*1e-5f))
                    throw new ArgumentException("Cells must be nondegenerate convex planar quadrilaterals with consistent winding.");
                Size=Mathf.Min(Size,2*Vector3.Cross(edge/edge.magnitude,Center-corners[i]).magnitude);
            }
        }
        private static bool Finite(Vector3 v) => !(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z));
        public Vector3 Corner(int index) => (uint)index<4 ? corners[index] : throw new ArgumentOutOfRangeException(nameof(index));
        public void Boundary(SurfaceDirection direction,out Vector3 a,out Vector3 b)
        {
            SurfaceDirections.Validate(direction);
            int i=direction==SurfaceDirection.Right ? 1 : direction==SurfaceDirection.Up ? 2 : direction==SurfaceDirection.Left ? 3 : 0;
            a=corners[i]; b=corners[(i+1)%4];
        }
        public Vector3 BoundaryCenter(SurfaceDirection direction) { Boundary(direction,out var a,out var b); return (a+b)*.5f; }
        public Vector3 Direction(SurfaceDirection direction) => (BoundaryCenter(direction)-Center).normalized;
    }

    /// <summary>Explicit geometry for an already authored topology; no topology extraction.</summary>
    public sealed class QuadSurfaceGeometry : ISurfaceGeometry
    {
        private readonly Dictionary<int,SurfaceQuad> cells;
        public ISurfaceTopology Surface { get; }
        public Bounds Bounds { get; }
        public float ReferenceCellSize { get; }
        public IReadOnlyList<SurfaceQuad> RenderQuads { get; }
        public QuadSurfaceGeometry(ISurfaceTopology surface,IReadOnlyDictionary<int,SurfaceQuad> quads)
        {
            Surface=surface??throw new ArgumentNullException(nameof(surface));
            if(quads==null || quads.Count!=surface.CellCount) throw new ArgumentException("Exactly one quad per logical cell is required.");
            cells=new Dictionary<int,SurfaceQuad>(); var render=new List<SurfaceQuad>();
            var bounds=new Bounds(); bool first=true; float size=float.PositiveInfinity;
            foreach(int id in surface.EnumerateCellIds())
            {
                if(!quads.TryGetValue(id,out var q) || q==null) throw new ArgumentException("Missing cell geometry: "+id);
                cells.Add(id,q); render.Add(q); size=Mathf.Min(size,q.Size);
                for(int i=0;i<4;i++) { if(first) {bounds=new Bounds(q.Corner(i),Vector3.zero); first=false;} else bounds.Encapsulate(q.Corner(i)); }
            }
            Bounds=bounds; ReferenceCellSize=size; RenderQuads=render.AsReadOnly();
            foreach(int id in surface.EnumerateCellIds())
            for(int d=0;d<4;d++)
            {
                var direction=(SurfaceDirection)d; var link=surface.GetBoundary(id,direction);
                if(link.Kind!=BoundaryConnectionKind.Connected) throw new NotSupportedException("Open geometry connections are not supported yet.");
                var from=cells[id]; var to=cells[link.NeighborCellId.Value];
                from.Boundary(direction,out var a,out var b); to.Boundary(link.NewDirection.Opposite(),out var c,out var e);
                float tolerance=Mathf.Max(1e-5f,bounds.size.magnitude*1e-5f);
                if(Vector3.Distance(a,e)>tolerance || Vector3.Distance(b,c)>tolerance) throw new ArgumentException($"Geometry does not share the logical boundary {id}/{direction}.");
                float dot=Vector3.Dot(from.Normal,to.Normal);
                if(dot<-.9999f) throw new ArgumentException("An antiparallel zero-thickness fold needs an explicit bridge surface.");
                if(dot<.9999f && !link.IsGeometricEdge) throw new ArgumentException("A physical fold must be marked as a geometric edge.");
                if(dot<.9999f && Vector3.Dot(to.Normal,from.Direction(direction))<0 && link.AllowsEdgeExit)
                    throw new ArgumentException("A concave boundary cannot allow an ordinary edge exit.");
            }
        }
        public SurfaceQuad GetCell(int cellId) => cells.TryGetValue(cellId,out var quad) ? quad : throw new ArgumentOutOfRangeException(nameof(cellId));
    }

    public static class SurfaceGeometry
    {
        public static Vector3 GetCellCenter(this ISurfaceGeometry geometry,int id,float lift=0) => geometry.GetCell(id).Center+geometry.GetCell(id).Normal*lift;
        public static Vector3 GetCellNormal(this ISurfaceGeometry geometry,int id) => geometry.GetCell(id).Normal;
        public static Vector3 GetDirection(this ISurfaceGeometry geometry,int id,SurfaceDirection direction) => geometry.GetCell(id).Direction(direction);
        public static Vector3 GetBoundaryPosition(this ISurfaceGeometry geometry,int id,SurfaceDirection direction) => geometry.GetCell(id).BoundaryCenter(direction);
        public static Vector3 GetLiftedBoundary(this ISurfaceGeometry geometry,int id,SurfaceDirection direction,float lift)
        {
            var link=geometry.Surface.GetBoundary(id,direction);
            var a=geometry.GetCellNormal(id); var b=geometry.GetCellNormal(link.NeighborCellId.Value);
            return geometry.GetBoundaryPosition(id,direction)+(a+b)*(lift/(1+Vector3.Dot(a,b)));
        }
        public static bool Raycast(this ISurfaceGeometry geometry,Ray ray,out float distance)
        {
            distance=float.PositiveInfinity;
            foreach(var q in geometry.RenderQuads)
            { if(RayTriangle(ray,q.Corner(0),q.Corner(1),q.Corner(2),out float t)) distance=Mathf.Min(distance,t);
              if(RayTriangle(ray,q.Corner(0),q.Corner(2),q.Corner(3),out t)) distance=Mathf.Min(distance,t); }
            return !float.IsPositiveInfinity(distance);
        }
        public static bool RayTriangle(Ray ray,Vector3 a,Vector3 b,Vector3 c,out float distance)
        {
            distance=0; var edge1=b-a; var edge2=c-a; var p=Vector3.Cross(ray.direction,edge2); float determinant=Vector3.Dot(edge1,p);
            if(Mathf.Abs(determinant)<1e-8f) return false;
            var offset=ray.origin-a; float u=Vector3.Dot(offset,p)/determinant;
            var q=Vector3.Cross(offset,edge1); float v=Vector3.Dot(ray.direction,q)/determinant;
            if(u<-.00001f || v<-.00001f || u+v>1.00001f) return false;
            distance=Vector3.Dot(edge2,q)/determinant; return distance>=0;
        }
    }
}
