using System;

namespace ArrowCube.Topology
{
    public enum BoundaryConnectionKind { Connected, Open }

    /// <summary>Immutable geometry metadata. Directional gates belong to the puzzle's ExitMap.</summary>
    public sealed class CellBoundary
    {
        public BoundaryConnectionKind Kind { get; }
        public int? NeighborCellId { get; }
        public SurfaceDirection NewDirection { get; }
        public bool IsGeometricEdge { get; }
        public bool AllowsEdgeExit { get; }

        public CellBoundary(int neighborCellId,SurfaceDirection newDirection,bool isGeometricEdge=false,bool allowsEdgeExit=false)
        {
            if(neighborCellId<0) throw new ArgumentOutOfRangeException(nameof(neighborCellId));
            SurfaceDirections.Validate(newDirection);
            Kind=BoundaryConnectionKind.Connected; NeighborCellId=neighborCellId; NewDirection=newDirection;
            IsGeometricEdge=isGeometricEdge; AllowsEdgeExit=allowsEdgeExit;
        }

        private CellBoundary(bool isGeometricEdge,bool allowsEdgeExit)
        { Kind=BoundaryConnectionKind.Open; IsGeometricEdge=isGeometricEdge; AllowsEdgeExit=allowsEdgeExit; }

        /// <summary>Explicitly open, as distinct from an unconfigured builder slot (null).
        /// Closed topology builds reject this until open-surface traversal is implemented.</summary>
        public static CellBoundary Open(bool isGeometricEdge=false,bool allowsEdgeExit=false) => new CellBoundary(isGeometricEdge,allowsEdgeExit);
    }
}
