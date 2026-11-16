using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ArrowCube.Topology;

namespace ArrowCube.Movement
{
    /// <summary>Central ownership of cells AND undirected segments. Mutations are atomic.</summary>
    public sealed class SurfaceGrid
    {
        private readonly Dictionary<int, ArrowPath> paths = new Dictionary<int, ArrowPath>();
        private readonly Dictionary<SurfaceCoordinate, int> cells = new Dictionary<SurfaceCoordinate, int>();
        private readonly Dictionary<SurfaceSegment, int> segments = new Dictionary<SurfaceSegment, int>();
        public CubeTopology Topology { get; }
        public IReadOnlyDictionary<int, ArrowPath> Paths { get; }
        public long Revision { get; private set; }
        public int OccupiedCellCount => cells.Count;
        public int OccupiedSegmentCount => segments.Count;

        public SurfaceGrid(CubeTopology topology, IEnumerable<ArrowPath> initialPaths)
        {
            Topology = topology ?? throw new ArgumentNullException(nameof(topology));
            if (initialPaths == null) throw new ArgumentNullException(nameof(initialPaths));
            Paths = new ReadOnlyDictionary<int, ArrowPath>(paths);
            foreach (var path in initialPaths) AddPath(path);
        }

        public bool TryGetCellOwner(SurfaceCoordinate cell, out int pathId)
        { Topology.Validate(cell); return cells.TryGetValue(cell, out pathId); }
        public bool TryGetSegmentOwner(SurfaceSegment segment, out int pathId) => segments.TryGetValue(segment, out pathId);

        public void AddPath(ArrowPath path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (paths.ContainsKey(path.Id)) throw new ArgumentException("Path ID is already active.", nameof(path));
            // Validate against THIS grid before any writes, even if the path came from another topology.
            _ = new ArrowPath(path.Id, Topology, path.Cells, path.HeadDirection);
            foreach (var cell in path.Cells)
                if (cells.ContainsKey(cell)) throw new ArgumentException("Paths cannot share a cell.", nameof(path));
            foreach (var segment in path.Segments)
                if (segments.ContainsKey(segment)) throw new ArgumentException("Paths cannot share a segment.", nameof(path));
            paths.Add(path.Id, path);
            foreach (var cell in path.Cells) cells.Add(cell, path.Id);
            foreach (var segment in path.Segments) segments.Add(segment, path.Id);
            Revision++;
        }

        public bool RemovePath(int id)
        {
            if (!paths.TryGetValue(id, out var path)) return false;
            foreach (var cell in path.Cells) cells.Remove(cell);
            foreach (var segment in path.Segments) segments.Remove(segment);
            paths.Remove(id);
            Revision++;
            return true;
        }

        /// <summary>Refuses failed, foreign, stale, or already committed plans.</summary>
        public bool TryCommit(MovementPlan plan)
        {
            if (plan == null || !plan.Succeeded || !ReferenceEquals(plan.Grid, this)
                || plan.Revision != Revision || !paths.TryGetValue(plan.Path.Id, out var active)
                || !ReferenceEquals(active, plan.Path)) return false;
            return RemovePath(plan.Path.Id);
        }
    }
}
