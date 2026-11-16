using System;
using System.Collections.Generic;

namespace ArrowCube.Topology
{
    public readonly struct EdgeTransition
    {
        public CubeFace DestinationFace { get; }
        public SurfaceDirection EntryEdge { get; }
        public bool ReverseOffset { get; }
        public SurfaceDirection ForwardDirection => EntryEdge.Opposite();

        internal EdgeTransition(CubeFace face, SurfaceDirection entryEdge, bool reverse = false)
        { DestinationFace = face; EntryEdge = entryEdge; ReverseOffset = reverse; }
    }

    public readonly struct SurfaceStep
    {
        public SurfaceCoordinate Coordinate { get; }
        public SurfaceDirection Direction { get; }
        public bool CrossedEdge { get; }

        internal SurfaceStep(SurfaceCoordinate coordinate, SurfaceDirection direction, bool crossedEdge)
        { Coordinate = coordinate; Direction = direction; CrossedEdge = crossedEdge; }
    }

    /// <summary>A closed, four-neighbor surface with exact bidirectional edge mappings.</summary>
    public sealed class CubeTopology
    {
        // Rows: Front, Right, Back, Left, Top, Bottom. Columns: Right, Up, Left, Down.
        // Offset is Y on vertical edges and X on horizontal edges, always increasing locally.
        private static readonly EdgeTransition[,] Edges =
        {
            { E(CubeFace.Right, SurfaceDirection.Left), E(CubeFace.Top, SurfaceDirection.Down),
              E(CubeFace.Left, SurfaceDirection.Right), E(CubeFace.Bottom, SurfaceDirection.Up) },
            { E(CubeFace.Back, SurfaceDirection.Left), E(CubeFace.Top, SurfaceDirection.Right),
              E(CubeFace.Front, SurfaceDirection.Right), E(CubeFace.Bottom, SurfaceDirection.Right, true) },
            { E(CubeFace.Left, SurfaceDirection.Left), E(CubeFace.Top, SurfaceDirection.Up, true),
              E(CubeFace.Right, SurfaceDirection.Right), E(CubeFace.Bottom, SurfaceDirection.Down, true) },
            { E(CubeFace.Front, SurfaceDirection.Left), E(CubeFace.Top, SurfaceDirection.Left, true),
              E(CubeFace.Back, SurfaceDirection.Right), E(CubeFace.Bottom, SurfaceDirection.Left) },
            { E(CubeFace.Right, SurfaceDirection.Up), E(CubeFace.Back, SurfaceDirection.Up, true),
              E(CubeFace.Left, SurfaceDirection.Up, true), E(CubeFace.Front, SurfaceDirection.Up) },
            { E(CubeFace.Right, SurfaceDirection.Down, true), E(CubeFace.Front, SurfaceDirection.Down),
              E(CubeFace.Left, SurfaceDirection.Down), E(CubeFace.Back, SurfaceDirection.Down, true) }
        };

        private static readonly FaceOrientation[] Orientations =
        {
            new FaceOrientation(V(0,0,1), V(1,0,0), V(0,1,0)),
            new FaceOrientation(V(1,0,0), V(0,0,-1), V(0,1,0)),
            new FaceOrientation(V(0,0,-1), V(-1,0,0), V(0,1,0)),
            new FaceOrientation(V(-1,0,0), V(0,0,1), V(0,1,0)),
            new FaceOrientation(V(0,1,0), V(1,0,0), V(0,0,-1)),
            new FaceOrientation(V(0,-1,0), V(1,0,0), V(0,0,1))
        };

        public int GridSize { get; }
        public int CellCount { get; }

        public CubeTopology(int gridSize)
        {
            // Bound guarantees all cell IDs and doubled integer coordinates fit in Int32.
            if (gridSize < 1 || gridSize > 18918)
                throw new ArgumentOutOfRangeException(nameof(gridSize), "Grid size must be 1..18918.");
            GridSize = gridSize;
            CellCount = checked(6 * gridSize * gridSize);
        }

        public bool Contains(SurfaceCoordinate coordinate) =>
            (uint)coordinate.Face < 6 && (uint)coordinate.X < GridSize && (uint)coordinate.Y < GridSize;

        public void Validate(SurfaceCoordinate coordinate)
        {
            if (!Contains(coordinate)) throw new ArgumentOutOfRangeException(nameof(coordinate), coordinate, "Cell is outside this grid.");
        }

        public FaceOrientation GetOrientation(CubeFace face)
        { ValidateFace(face); return Orientations[(int)face]; }

        public EdgeTransition GetEdgeTransition(CubeFace face, SurfaceDirection edge)
        {
            ValidateFace(face);
            SurfaceDirections.Validate(edge);
            return Edges[(int)face, (int)edge];
        }

        public SurfaceStep Step(SurfaceCoordinate from, SurfaceDirection direction)
        {
            Validate(from);
            SurfaceDirections.Validate(direction);
            int x = from.X, y = from.Y;
            switch (direction)
            {
                case SurfaceDirection.Right: x++; break;
                case SurfaceDirection.Up: y++; break;
                case SurfaceDirection.Left: x--; break;
                case SurfaceDirection.Down: y--; break;
            }

            if ((uint)x < GridSize && (uint)y < GridSize)
                return new SurfaceStep(new SurfaceCoordinate(from.Face, x, y), direction, false);

            var edge = GetEdgeTransition(from.Face, direction);
            int offset = direction == SurfaceDirection.Right || direction == SurfaceDirection.Left ? from.Y : from.X;
            if (edge.ReverseOffset) offset = GridSize - 1 - offset;
            switch (edge.EntryEdge)
            {
                case SurfaceDirection.Right: x = GridSize - 1; y = offset; break;
                case SurfaceDirection.Up: x = offset; y = GridSize - 1; break;
                case SurfaceDirection.Left: x = 0; y = offset; break;
                default: x = offset; y = 0; break;
            }
            return new SurfaceStep(new SurfaceCoordinate(edge.DestinationFace, x, y), edge.ForwardDirection, true);
        }

        public bool TryGetDirection(SurfaceCoordinate from, SurfaceCoordinate to, out SurfaceDirection direction)
        {
            Validate(from);
            Validate(to);
            for (int i = 0; i < 4; i++)
            {
                direction = (SurfaceDirection)i;
                if (Step(from, direction).Coordinate == to) return true;
            }
            direction = default;
            return false;
        }

        public int GetCellIndex(SurfaceCoordinate coordinate)
        {
            Validate(coordinate);
            return ((int)coordinate.Face * GridSize + coordinate.Y) * GridSize + coordinate.X;
        }

        public SurfaceCoordinate GetCoordinate(int index)
        {
            if ((uint)index >= CellCount) throw new ArgumentOutOfRangeException(nameof(index));
            int faceArea = GridSize * GridSize;
            int local = index % faceArea;
            return new SurfaceCoordinate((CubeFace)(index / faceArea), local % GridSize, local / GridSize);
        }

        public IEnumerable<SurfaceCoordinate> EnumerateCells()
        {
            for (int i = 0; i < CellCount; i++) yield return GetCoordinate(i);
        }

        /// <summary>Exact cube-local center in half-cell units; cube side length is 2 * GridSize.</summary>
        public GridVector3 GetDoubledPosition(SurfaceCoordinate coordinate)
        {
            Validate(coordinate);
            var frame = GetOrientation(coordinate.Face);
            return frame.Normal * GridSize + frame.Right * (2 * coordinate.X + 1 - GridSize)
                + frame.Up * (2 * coordinate.Y + 1 - GridSize);
        }

        /// <summary>The shared seam point, avoiding a chord through the cube on edge crossings.</summary>
        public GridVector3 GetDoubledEdgeMidpoint(SurfaceCoordinate from, SurfaceDirection direction)
        {
            var step = Step(from, direction);
            if (!step.CrossedEdge) throw new ArgumentException("Step does not cross a cube edge.", nameof(direction));
            return GetDoubledPosition(from) + GetOrientation(from.Face).GetTangent(direction);
        }

        private static EdgeTransition E(CubeFace face, SurfaceDirection entry, bool reverse = false) => new EdgeTransition(face, entry, reverse);
        private static GridVector3 V(int x, int y, int z) => new GridVector3(x, y, z);
        private static void ValidateFace(CubeFace face)
        { if ((uint)face >= 6) throw new ArgumentOutOfRangeException(nameof(face)); }
    }
}
