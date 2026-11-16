using System;
using System.Collections.Generic;

namespace ArrowCube.Topology
{
    /// <summary>Immutable head-to-tail cell IDs and connections. Cube coordinates are compatibility views.</summary>
    public sealed class ArrowPath
    {
        public int Id { get; }
        private IReadOnlyList<SurfaceCoordinate> cubeCells;
        private IReadOnlyList<SurfaceSegment> cubeSegments;
        public ISurfaceTopology Surface { get; }
        public IReadOnlyList<int> CellIds { get; }
        public IReadOnlyList<CellSegment> CellSegments { get; }
        public IReadOnlyList<SurfaceDirection> BodyDirections { get; }
        public int HeadCellId => CellIds[0];
        public int TailCellId => CellIds[CellIds.Count-1];
        public IReadOnlyList<SurfaceCoordinate> Cells
        {
            get
            {
                if(cubeCells==null)
                { var cube=CubeTopologyAdapter.Require(Surface); var copy=new List<SurfaceCoordinate>(); foreach(int id in CellIds) copy.Add(cube.GetCoordinate(id)); cubeCells=copy.AsReadOnly(); }
                return cubeCells;
            }
        }
        public IReadOnlyList<SurfaceSegment> Segments
        {
            get
            {
                if(cubeSegments==null)
                { var cube=CubeTopologyAdapter.Require(Surface); var copy=new List<SurfaceSegment>(); for(int i=1;i<Cells.Count;i++) copy.Add(new SurfaceSegment(cube,Cells[i-1],Cells[i])); cubeSegments=copy.AsReadOnly(); }
                return cubeSegments;
            }
        }
        public SurfaceCoordinate Head => Cells[0];
        public SurfaceCoordinate Tail => Cells[Cells.Count - 1];
        public SurfaceDirection HeadDirection { get; }
        public int GeometricTransitionCount { get; }
        public int FaceTransitionCount => GeometricTransitionCount;

        public ArrowPath(int id, CubeTopology topology, IEnumerable<SurfaceCoordinate> headToTail,
            SurfaceDirection headDirection)
            : this(id,(ISurfaceTopology)topology,CubeTopologyAdapter.CellIds(topology,headToTail),headDirection) { }

        public ArrowPath(int id,ISurfaceTopology topology,IEnumerable<int> headToTail,SurfaceDirection headDirection,
            IEnumerable<SurfaceDirection> headToTailDirections=null)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (topology == null) throw new ArgumentNullException(nameof(topology));
            Surface=topology;
            if (headToTail == null) throw new ArgumentNullException(nameof(headToTail));
            SurfaceDirections.Validate(headDirection);
            var cells = new List<int>(headToTail);
            if (cells.Count == 0) throw new ArgumentException("Path must contain at least one cell.", nameof(headToTail));
            var seen = new HashSet<int>();
            var segments = new List<CellSegment>(cells.Count - 1);
            var directions=headToTailDirections==null ? new List<SurfaceDirection>() : new List<SurfaceDirection>(headToTailDirections);
            if(headToTailDirections!=null && directions.Count!=cells.Count-1) throw new ArgumentException("One boundary direction per body connection is required.",nameof(headToTailDirections));
            int transitions = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                if(!topology.ContainsCell(cells[i])) throw new ArgumentOutOfRangeException(nameof(headToTail),"Cell is outside this surface.");
                if (!seen.Add(cells[i])) throw new ArgumentException("A path cannot revisit a cell.", nameof(headToTail));
                if (i == 0) continue;
                SurfaceDirection direction;
                if(headToTailDirections==null)
                {
                    if(!topology.TryGetDirection(cells[i-1],cells[i],out direction)) throw new ArgumentException("Path cells must be connected.",nameof(headToTail));
                    directions.Add(direction);
                }
                else direction=directions[i-1];
                if(topology.Step(cells[i-1],direction).CellId!=cells[i]) throw new ArgumentException("Body boundary does not reach the next cell.",nameof(headToTailDirections));
                var segment = new CellSegment(topology, cells[i - 1],direction);
                segments.Add(segment);
                if (segment.CrossesGeometricEdge) transitions++;
            }

            if (cells.Count > 1)
            {
                if (directions[0].Opposite() != headDirection)
                    throw new ArgumentException("Arrowhead must point away from the neck in the head face's frame.", nameof(headDirection));
            }

            Id = id;
            CellIds = cells.AsReadOnly();
            CellSegments = segments.AsReadOnly();
            BodyDirections=directions.AsReadOnly();
            HeadDirection = headDirection;
            GeometricTransitionCount = transitions;
        }
    }
}
