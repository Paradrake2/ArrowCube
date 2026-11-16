using System;
using System.Collections.Generic;
using ArrowCube.Topology;
using UnityEngine;

namespace ArrowCube.Presentation
{
    /// <summary>Unity adapter. Logical topology remains independent of rendering and transforms.</summary>
    public sealed class CubeSurfaceGeometry : ISurfaceGeometry
    {
        private readonly Dictionary<int,SurfaceQuad> cells=new Dictionary<int,SurfaceQuad>();
        public ISurfaceTopology Surface => Topology;
        public Bounds Bounds => new Bounds(Vector3.zero,Vector3.one*SideLength);
        public float ReferenceCellSize => CellSize;
        public IReadOnlyList<SurfaceQuad> RenderQuads { get; }
        public CubeTopology Topology { get; }
        public float SideLength { get; }
        public float CellSize => SideLength / Topology.GridSize;

        public CubeSurfaceGeometry(CubeTopology topology, float sideLength)
        {
            if (topology == null) throw new ArgumentNullException(nameof(topology));
            if (float.IsNaN(sideLength) || float.IsInfinity(sideLength) || sideLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(sideLength));
            Topology = topology;
            SideLength = sideLength;
            var faces=new List<SurfaceQuad>();
            for(int f=0;f<6;f++)
            {
                var frame=topology.GetOrientation((CubeFace)f); var center=ToVector(frame.Normal)*(sideLength*.5f);
                var right=ToVector(frame.Right)*(sideLength*.5f); var up=ToVector(frame.Up)*(sideLength*.5f);
                faces.Add(new SurfaceQuad(center-right-up,center+right-up,center+right+up,center-right+up,ColoredCubeSurface.FaceColor((CubeFace)f)));
            }
            RenderQuads=faces.AsReadOnly();
        }

        public SurfaceQuad GetCell(int cellId)
        {
            if(!cells.TryGetValue(cellId,out var q))
            {
                var coordinate=Topology.GetCoordinate(cellId); var frame=Topology.GetOrientation(coordinate.Face);
                var center=GetLocalPosition(coordinate); var right=ToVector(frame.Right)*(CellSize*.5f); var up=ToVector(frame.Up)*(CellSize*.5f);
                q=new SurfaceQuad(center-right-up,center+right-up,center+right+up,center-right+up,ColoredCubeSurface.FaceColor(coordinate.Face)); cells.Add(cellId,q);
            }
            return q;
        }

        public Vector3 GetLocalPosition(SurfaceCoordinate cell, float lift = 0) =>
            ToVector(Topology.GetDoubledPosition(cell)) * (CellSize * 0.5f)
            + ToVector(Topology.GetOrientation(cell.Face).Normal) * lift;

        public Vector3 GetWorldPosition(SurfaceCoordinate cell, Transform cube, float lift = 0)
        {
            if (cube == null) throw new ArgumentNullException(nameof(cube));
            return cube.TransformPoint(GetLocalPosition(cell, lift));
        }

        public Vector3 GetLocalDirection(CubeFace face, SurfaceDirection direction) =>
            ToVector(Topology.GetOrientation(face).GetTangent(direction));

        /// <summary>Writes a head-to-tail polyline, inserting a shared point at every seam.</summary>
        public void WritePolyline(ArrowPath path, List<Vector3> points, float lift = 0)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (float.IsNaN(lift) || float.IsInfinity(lift) || lift < 0)
                throw new ArgumentOutOfRangeException(nameof(lift));
            points.Clear();
            for (int i = 0; i < path.Cells.Count; i++)
            {
                var cell = path.Cells[i];
                if (i > 0)
                {
                    var previous = path.Cells[i - 1];
                    if (!Topology.TryGetDirection(previous, cell, out var direction))
                        throw new ArgumentException("Path is not connected on this topology.", nameof(path));
                    if (previous.Face != cell.Face)
                    {
                        var seam = ToVector(Topology.GetDoubledEdgeMidpoint(previous, direction)) * (CellSize * 0.5f);
                        // Offset in BOTH face normals so raised seam joins remain outside the cube.
                        var normals = ToVector(Topology.GetOrientation(previous.Face).Normal)
                            + ToVector(Topology.GetOrientation(cell.Face).Normal);
                        points.Add(seam + normals * lift);
                    }
                }
                points.Add(GetLocalPosition(cell, lift));
            }
        }

        public static Vector3 ToVector(GridVector3 vector) => new Vector3(vector.X, vector.Y, vector.Z);
    }
}
