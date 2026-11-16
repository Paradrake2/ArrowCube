using System;

namespace ArrowCube.Topology
{
    /// <summary>An undirected connection with the same identity from either side of a seam.</summary>
    public readonly struct SurfaceSegment : IEquatable<SurfaceSegment>
    {
        public SurfaceCoordinate A { get; }
        public SurfaceCoordinate B { get; }

        public SurfaceSegment(CubeTopology topology, SurfaceCoordinate a, SurfaceCoordinate b)
        {
            if (topology == null) throw new ArgumentNullException(nameof(topology));
            if (!topology.TryGetDirection(a, b, out _))
                throw new ArgumentException("Segment endpoints must be cardinal neighbors, including across cube edges.");
            bool ordered = topology.GetCellIndex(a) < topology.GetCellIndex(b);
            A = ordered ? a : b;
            B = ordered ? b : a;
        }

        public bool CrossesFace => A.Face != B.Face;
        public bool Equals(SurfaceSegment other) => A == other.A && B == other.B;
        public override bool Equals(object obj) => obj is SurfaceSegment other && Equals(other);
        public override int GetHashCode() => unchecked(A.GetHashCode() * 397 ^ B.GetHashCode());
        public static bool operator ==(SurfaceSegment a, SurfaceSegment b) => a.Equals(b);
        public static bool operator !=(SurfaceSegment a, SurfaceSegment b) => !a.Equals(b);
    }
}
