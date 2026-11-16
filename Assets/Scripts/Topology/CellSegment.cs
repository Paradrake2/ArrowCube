using System;
using System.Runtime.CompilerServices;

namespace ArrowCube.Topology
{
    /// <summary>An undirected boundary connection, scoped to its topology. Direction slots
    /// distinguish parallel connections between the same two cells.</summary>
    public readonly struct CellSegment : IEquatable<CellSegment>
    {
        public ISurfaceTopology Surface { get; }
        public int A { get; }
        public int B { get; }
        public SurfaceDirection DirectionFromA { get; }
        public SurfaceDirection DirectionFromB { get; }
        public bool CrossesGeometricEdge { get; }
        public CellSegment(ISurfaceTopology surface,int from,int to) : this(surface,from,FindDirection(surface,from,to)) { }
        public CellSegment(ISurfaceTopology surface,int from,SurfaceDirection direction)
        {
            if(surface==null) throw new ArgumentNullException(nameof(surface));
            var step=surface.Step(from,direction); var reverse=step.Direction.Opposite();
            bool ordered=from<step.CellId || from==step.CellId && (int)direction<=(int)reverse;
            Surface=surface; A=ordered ? from : step.CellId; B=ordered ? step.CellId : from;
            DirectionFromA=ordered ? direction : reverse; DirectionFromB=ordered ? reverse : direction;
            CrossesGeometricEdge=step.IsGeometricEdge;
        }
        private static SurfaceDirection FindDirection(ISurfaceTopology surface,int from,int to)
        {
            if(!SurfaceTraversal.TryGetDirection(surface,from,to,out var direction)) throw new ArgumentException("Segment endpoints must be connected surface cells.");
            return direction;
        }
        public bool Equals(CellSegment other) => CubeTopologyAdapter.Compatible(Surface,other.Surface)
            && A==other.A && B==other.B && DirectionFromA==other.DirectionFromA && DirectionFromB==other.DirectionFromB;
        public override bool Equals(object obj) => obj is CellSegment other && Equals(other);
        public override int GetHashCode()
        {
            int topology=Surface is CubeTopology cube ? cube.GridSize : Surface==null ? 0 : RuntimeHelpers.GetHashCode(Surface);
            return unchecked(((((topology*397)^A)*397^B)*397^(int)DirectionFromA)*397^(int)DirectionFromB);
        }
        public static bool operator ==(CellSegment a,CellSegment b) => a.Equals(b);
        public static bool operator !=(CellSegment a,CellSegment b) => !a.Equals(b);
    }
}
