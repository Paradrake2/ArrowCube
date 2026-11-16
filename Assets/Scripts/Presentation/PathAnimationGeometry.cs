using System;
using System.Collections.Generic;
using ArrowCube.Movement;
using ArrowCube.Topology;
using UnityEngine;

namespace ArrowCube.Presentation
{
    /// <summary>Samples the route itself, keeping knots at bends and seam midpoints while sliding.</summary>
    public sealed class PathAnimationGeometry
    {
        private readonly CubeSurfaceGeometry geometry;
        private readonly List<float> sampleDistances=new List<float>();
        public PathAnimationGeometry(CubeSurfaceGeometry geometry)
        { this.geometry = geometry ?? throw new ArgumentNullException(nameof(geometry)); }

        public Vector3 Evaluate(IReadOnlyList<PathPosition> rail, float distance, float lift = 0)
        {
            Validate(rail, distance, lift);
            int index = Mathf.Min(Mathf.FloorToInt(distance), rail.Count - 1);
            var from = rail[index];
            var a = Position(from, lift);
            if (index == rail.Count - 1) return a;
            var to = rail[index + 1];
            var b = Position(to, lift);
            float fraction = distance - index;
            if (from.IsOnSurface && to.IsOnSurface && from.Cell.Face != to.Cell.Face)
            {
                if (!geometry.Topology.TryGetDirection(from.Cell, to.Cell, out var direction))
                    throw new ArgumentException("Rail has a disconnected seam.", nameof(rail));
                var seam = CubeSurfaceGeometry.ToVector(geometry.Topology.GetDoubledEdgeMidpoint(from.Cell, direction))
                    * (geometry.CellSize * 0.5f) + (Normal(from.Cell.Face) + Normal(to.Cell.Face)) * lift;
                return fraction <= 0.5f ? Vector3.Lerp(a, seam, fraction * 2)
                    : Vector3.Lerp(seam, b, (fraction - 0.5f) * 2);
            }
            return Vector3.Lerp(a, b, fraction);
        }

        public void WritePolyline(IReadOnlyList<PathPosition> rail, int bodyLength, float progress,
            List<Vector3> points, float lift = 0,List<Vector3> segmentNormals=null,SurfaceDirection initialHeading=SurfaceDirection.Right)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            Validate(rail, progress, lift);
            if (bodyLength < 1 || bodyLength > rail.Count) throw new ArgumentOutOfRangeException(nameof(bodyLength));
            if (progress > rail.Count - bodyLength) throw new ArgumentOutOfRangeException(nameof(progress));
            float tail = progress, head = bodyLength - 1 + progress;
            points.Clear();
            sampleDistances.Clear(); segmentNormals?.Clear();
            points.Add(Evaluate(rail, tail, lift));
            sampleDistances.Add(tail);
            if (bodyLength == 1) return;
            for (int i = Mathf.FloorToInt(tail); i < head && i < rail.Count - 1; i++)
            {
                var from = rail[i]; var to = rail[i + 1];
                if (from.IsOnSurface && to.IsOnSurface && from.Cell.Face != to.Cell.Face
                    && i + 0.5f > tail && i + 0.5f < head)
                { points.Add(Evaluate(rail, i + 0.5f, lift)); sampleDistances.Add(i+.5f); }
                if (i + 1 > tail && i + 1 < head) { points.Add(Position(to, lift)); sampleDistances.Add(i+1); }
            }
            points.Add(Evaluate(rail, head, lift));
            sampleDistances.Add(head);
            points.Reverse();
            sampleDistances.Reverse();
            if(segmentNormals!=null)
                for(int i=0;i<sampleDistances.Count-1;i++)
                    segmentNormals.Add(GetSegmentNormal(rail,(sampleDistances[i]+sampleDistances[i+1])*.5f,initialHeading));
        }

        public Vector3 GetSegmentNormal(IReadOnlyList<PathPosition> rail,float distance,SurfaceDirection initialHeading)
        {
            Validate(rail,distance,0);
            int i=Mathf.Min(Mathf.FloorToInt(distance),rail.Count-1);
            var from=rail[i]; var to=rail[Mathf.Min(i+1,rail.Count-1)];
            if(from.ExitType==ExitType.DirectionalGate || to.ExitType==ExitType.DirectionalGate)
            {
                int gate=i; while(gate>0 && !rail[gate].IsOnSurface) gate--;
                var heading=initialHeading;
                if(gate>0 && geometry.Topology.TryGetDirection(rail[gate-1].Cell,rail[gate].Cell,out var incoming))
                    heading=geometry.Topology.Step(rail[gate-1].Cell,incoming).Direction;
                // Turn the ribbon plane with the departing head, preserving its cross-section.
                return -geometry.GetLocalDirection(rail[gate].Cell.Face,heading);
            }
            if(from.IsOnSurface && to.IsOnSurface && from.Cell.Face!=to.Cell.Face)
                return Normal(distance-i<.5f ? from.Cell.Face : to.Cell.Face);
            return Normal(from.Cell.Face);
        }

        public Vector3 GetNormal(IReadOnlyList<PathPosition> rail, float distance)
        {
            Validate(rail, distance, 0);
            int i = Mathf.Min(Mathf.FloorToInt(distance), rail.Count - 1);
            if (i == rail.Count - 1 || !rail[i].IsOnSurface || !rail[i + 1].IsOnSurface)
                return Normal(rail[i].Cell.Face);
            return Vector3.Slerp(Normal(rail[i].Cell.Face), Normal(rail[i + 1].Cell.Face), distance - i).normalized;
        }

        private Vector3 Position(PathPosition position, float lift)
        {
            var departure = position.ExitType == ExitType.CubeEdge
                ? geometry.GetLocalDirection(position.Cell.Face, position.ExitDirection) : Normal(position.Cell.Face);
            return geometry.GetLocalPosition(position.Cell, lift) + departure * (position.ExitDistance * geometry.CellSize);
        }
        private Vector3 Normal(CubeFace face) => CubeSurfaceGeometry.ToVector(geometry.Topology.GetOrientation(face).Normal);
        private static void Validate(IReadOnlyList<PathPosition> rail, float distance, float lift)
        {
            if (rail == null || rail.Count == 0) throw new ArgumentException("Rail must contain positions.", nameof(rail));
            if (float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0 || distance > rail.Count - 1)
                throw new ArgumentOutOfRangeException(nameof(distance));
            if (float.IsNaN(lift) || float.IsInfinity(lift) || lift < 0) throw new ArgumentOutOfRangeException(nameof(lift));
        }
    }
}
