using System;
using System.Collections.Generic;

namespace ArrowCube.Topology
{
    /// <summary>Logical cell connectivity only. No mesh, gate, puzzle, or Unity dependencies.</summary>
    public interface ISurfaceTopology
    {
        int CellCount { get; }
        bool ContainsCell(int cellId);
        SurfaceCell GetCell(int cellId);
        CellBoundary GetBoundary(int cellId,SurfaceDirection direction);
        IEnumerable<int> EnumerateCellIds();
    }

    public readonly struct SurfaceCellStep
    {
        public int CellId { get; }
        public SurfaceDirection Direction { get; }
        public bool IsGeometricEdge { get; }
        public bool AllowsEdgeExit { get; }
        internal SurfaceCellStep(CellBoundary boundary)
        {
            CellId=boundary.NeighborCellId.Value; Direction=boundary.NewDirection;
            IsGeometricEdge=boundary.IsGeometricEdge; AllowsEdgeExit=boundary.AllowsEdgeExit;
        }
    }

    public static class SurfaceTraversal
    {
        /// <summary>Follows the connection even at a concave or convex fold. Exit policy is separate.</summary>
        public static SurfaceCellStep Step(this ISurfaceTopology topology,int cellId,SurfaceDirection direction)
        {
            if(topology==null) throw new ArgumentNullException(nameof(topology));
            var boundary=topology.GetBoundary(cellId,direction);
            if(boundary.Kind==BoundaryConnectionKind.Open)
                throw new NotSupportedException("Open-surface traversal is not implemented.");
            return new SurfaceCellStep(boundary);
        }
    }

    /// <summary>A validated immutable snapshot. Builder edits cannot alter an existing topology.</summary>
    public sealed class SurfaceTopology : ISurfaceTopology
    {
        private readonly Dictionary<int,SurfaceCell> cells;
        private readonly IReadOnlyList<int> ids;
        public int CellCount => cells.Count;

        internal SurfaceTopology(Dictionary<int,SurfaceCell> cells,int[] ids)
        { this.cells=cells; this.ids=Array.AsReadOnly(ids); }
        public bool ContainsCell(int cellId) => cells.ContainsKey(cellId);
        public SurfaceCell GetCell(int cellId) => cells.TryGetValue(cellId,out var cell) ? cell : throw new ArgumentOutOfRangeException(nameof(cellId));
        public CellBoundary GetBoundary(int cellId,SurfaceDirection direction) => GetCell(cellId).GetBoundary(direction);
        public IEnumerable<int> EnumerateCellIds() => ids;
    }
}
