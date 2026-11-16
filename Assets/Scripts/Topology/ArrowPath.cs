using System;
using System.Collections.Generic;

namespace ArrowCube.Topology
{
    /// <summary>Immutable geometry, ordered head to tail. Runtime movement/removal state belongs elsewhere.</summary>
    public sealed class ArrowPath
    {
        public int Id { get; }
        public IReadOnlyList<SurfaceCoordinate> Cells { get; }
        public IReadOnlyList<SurfaceSegment> Segments { get; }
        public SurfaceCoordinate Head => Cells[0];
        public SurfaceCoordinate Tail => Cells[Cells.Count - 1];
        public SurfaceDirection HeadDirection { get; }
        public int FaceTransitionCount { get; }

        public ArrowPath(int id, CubeTopology topology, IEnumerable<SurfaceCoordinate> headToTail,
            SurfaceDirection headDirection)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (topology == null) throw new ArgumentNullException(nameof(topology));
            if (headToTail == null) throw new ArgumentNullException(nameof(headToTail));
            SurfaceDirections.Validate(headDirection);
            var cells = new List<SurfaceCoordinate>(headToTail);
            if (cells.Count == 0) throw new ArgumentException("Path must contain at least one cell.", nameof(headToTail));
            var seen = new HashSet<SurfaceCoordinate>();
            var segments = new List<SurfaceSegment>(cells.Count - 1);
            int transitions = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                topology.Validate(cells[i]);
                if (!seen.Add(cells[i])) throw new ArgumentException("A path cannot revisit a cell.", nameof(headToTail));
                if (i == 0) continue;
                var segment = new SurfaceSegment(topology, cells[i - 1], cells[i]);
                segments.Add(segment);
                if (segment.CrossesFace) transitions++;
            }

            if (cells.Count > 1)
            {
                topology.TryGetDirection(cells[1], cells[0], out var incoming);
                if (topology.Step(cells[1], incoming).Direction != headDirection)
                    throw new ArgumentException("Arrowhead must point away from the neck in the head face's frame.", nameof(headDirection));
            }

            Id = id;
            Cells = cells.AsReadOnly();
            Segments = segments.AsReadOnly();
            HeadDirection = headDirection;
            FaceTransitionCount = transitions;
        }
    }
}
