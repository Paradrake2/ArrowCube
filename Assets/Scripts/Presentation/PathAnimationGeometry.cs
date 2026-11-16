using System;
using System.Collections.Generic;
using ArrowCube.Movement;
using ArrowCube.Topology;
using UnityEngine;

namespace ArrowCube.Presentation
{
    /// <summary>Maps the simulator's chronological rail onto cell planes and shared boundaries.</summary>
    public sealed class PathAnimationGeometry
    {
        private readonly ISurfaceGeometry geometry;
        private readonly List<float> sampleDistances=new List<float>();
        private IReadOnlyList<PathPosition> cachedCubeRail;
        private SurfaceRail cached;
        private SurfaceDirection cachedHeading;
        public PathAnimationGeometry(ISurfaceGeometry geometry) { this.geometry=geometry??throw new ArgumentNullException(nameof(geometry)); }

        private SurfaceRail Adapt(IReadOnlyList<PathPosition> rail,SurfaceDirection heading)
        {
            if(rail==null || rail.Count==0) throw new ArgumentException("Rail must contain positions.",nameof(rail));
            if(!ReferenceEquals(cachedCubeRail,rail) || cachedHeading!=heading)
            { cached=SurfaceRail.FromCube(CubeTopologyAdapter.Require(geometry.Surface),rail,heading); cachedCubeRail=rail; cachedHeading=heading; }
            return cached;
        }
        public Vector3 Evaluate(IReadOnlyList<PathPosition> rail,float distance,float lift=0) => Evaluate(Adapt(rail,SurfaceDirection.Right),distance,lift);
        public void WritePolyline(IReadOnlyList<PathPosition> rail,int bodyLength,float progress,List<Vector3> points,float lift=0,List<Vector3> segmentNormals=null,SurfaceDirection initialHeading=SurfaceDirection.Right)
            => WritePolyline(Adapt(rail,initialHeading),bodyLength,progress,points,lift,segmentNormals);
        public Vector3 GetSegmentNormal(IReadOnlyList<PathPosition> rail,float distance,SurfaceDirection initialHeading) => GetSegmentNormal(Adapt(rail,initialHeading),distance);
        public Vector3 GetNormal(IReadOnlyList<PathPosition> rail,float distance) => GetNormal(Adapt(rail,SurfaceDirection.Right),distance);

        public float BoundaryFraction(SurfaceRail rail,int index)
        {
            var from=rail[index]; var to=rail[index+1];
            if(!from.IsOnSurface || !to.IsOnSurface) return .5f;
            var seam=geometry.GetBoundaryPosition(from.CellId,rail.Directions[index]);
            float a=Vector3.Distance(geometry.GetCellCenter(from.CellId),seam),b=Vector3.Distance(seam,geometry.GetCellCenter(to.CellId));
            return a/(a+b);
        }
        public bool HasBoundaryKnot(SurfaceRail rail,int index)
        {
            if(index>=rail.Count-1 || !rail[index].IsOnSurface || !rail[index+1].IsOnSurface) return false;
            if(Vector3.Dot(geometry.GetCellNormal(rail[index].CellId),geometry.GetCellNormal(rail[index+1].CellId))<.99999f) return true;
            var seam=geometry.GetBoundaryPosition(rail[index].CellId,rail.Directions[index]);
            return Vector3.Cross(seam-geometry.GetCellCenter(rail[index].CellId),geometry.GetCellCenter(rail[index+1].CellId)-seam).sqrMagnitude>1e-10f;
        }
        public Vector3 Evaluate(SurfaceRail rail,float distance,float lift=0)
        {
            Validate(rail,distance,lift);
            int index=Mathf.Min(Mathf.FloorToInt(distance),rail.Count-1);
            var from=rail[index]; var a=Position(from,lift);
            if(index==rail.Count-1) return a;
            var to=rail[index+1]; var b=Position(to,lift); float fraction=distance-index;
            if(from.IsOnSurface && to.IsOnSurface)
            {
                var seam=geometry.GetLiftedBoundary(from.CellId,rail.Directions[index],lift);
                float split=BoundaryFraction(rail,index);
                return fraction<=split ? Vector3.Lerp(a,seam,fraction/split) : Vector3.Lerp(seam,b,(fraction-split)/(1-split));
            }
            return Vector3.Lerp(a,b,fraction);
        }
        public void WritePolyline(SurfaceRail rail,int bodyLength,float progress,List<Vector3> points,float lift=0,List<Vector3> segmentNormals=null)
        {
            if(points==null) throw new ArgumentNullException(nameof(points));
            Validate(rail,progress,lift);
            if(bodyLength<1 || bodyLength>rail.Count) throw new ArgumentOutOfRangeException(nameof(bodyLength));
            if(progress>rail.Count-bodyLength) throw new ArgumentOutOfRangeException(nameof(progress));
            float tail=progress,head=bodyLength-1+progress;
            points.Clear(); sampleDistances.Clear(); segmentNormals?.Clear();
            points.Add(Evaluate(rail,tail,lift)); sampleDistances.Add(tail);
            if(bodyLength==1) return;
            for(int i=Mathf.FloorToInt(tail);i<head && i<rail.Count-1;i++)
            {
                float seam=i+BoundaryFraction(rail,i);
                if(HasBoundaryKnot(rail,i) && seam>tail && seam<head)
                { points.Add(Evaluate(rail,seam,lift)); sampleDistances.Add(seam); }
                if(i+1>tail && i+1<head) { points.Add(Position(rail[i+1],lift)); sampleDistances.Add(i+1); }
            }
            points.Add(Evaluate(rail,head,lift)); sampleDistances.Add(head);
            points.Reverse(); sampleDistances.Reverse();
            if(segmentNormals!=null)
                for(int i=0;i<sampleDistances.Count-1;i++) segmentNormals.Add(GetSegmentNormal(rail,(sampleDistances[i]+sampleDistances[i+1])*.5f));
        }
        public Vector3 GetSegmentNormal(SurfaceRail rail,float distance)
        {
            Validate(rail,distance,0); int i=Mathf.Min(Mathf.FloorToInt(distance),rail.Count-1);
            var from=rail[i]; var to=rail[Mathf.Min(i+1,rail.Count-1)];
            if(from.ExitType==ExitType.DirectionalGate || to.ExitType==ExitType.DirectionalGate)
            {
                int gate=i; while(gate>0 && !rail[gate].IsOnSurface) gate--;
                return -geometry.GetDirection(rail[gate].CellId,rail.Directions[Mathf.Min(gate,rail.Directions.Count-1)]);
            }
            if(from.IsOnSurface && to.IsOnSurface && i<rail.Count-1)
                return geometry.GetCellNormal(distance-i<BoundaryFraction(rail,i) ? from.CellId : to.CellId);
            return geometry.GetCellNormal(from.CellId);
        }
        public Vector3 GetNormal(SurfaceRail rail,float distance)
        {
            Validate(rail,distance,0); int i=Mathf.Min(Mathf.FloorToInt(distance),rail.Count-1);
            if(i==rail.Count-1 || !rail[i].IsOnSurface || !rail[i+1].IsOnSurface) return geometry.GetCellNormal(rail[i].CellId);
            return Vector3.Slerp(geometry.GetCellNormal(rail[i].CellId),geometry.GetCellNormal(rail[i+1].CellId),distance-i).normalized;
        }
        private Vector3 Position(CellPathPosition position,float lift)
        {
            var quad=geometry.GetCell(position.CellId);
            var departure=position.ExitType==ExitType.CubeEdge ? quad.Direction(position.ExitDirection) : quad.Normal;
            float size=position.ExitType==ExitType.CubeEdge ? 2*Vector3.Distance(quad.Center,quad.BoundaryCenter(position.ExitDirection)) : quad.Size;
            return quad.Center+quad.Normal*lift+departure*(position.ExitDistance*size);
        }
        private static void Validate(SurfaceRail rail,float distance,float lift)
        {
            if(rail==null || rail.Count==0) throw new ArgumentException("Rail must contain positions.",nameof(rail));
            if(float.IsNaN(distance)||float.IsInfinity(distance)||distance<0||distance>rail.Count-1) throw new ArgumentOutOfRangeException(nameof(distance));
            if(float.IsNaN(lift)||float.IsInfinity(lift)||lift<0) throw new ArgumentOutOfRangeException(nameof(lift));
        }
    }
}