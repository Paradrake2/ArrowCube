using System;
using System.Collections.Generic;
using ArrowCube.Topology;

namespace ArrowCube.Movement
{
    /// <summary>An explicit departure from a cell in a specified incoming heading.</summary>
    public readonly struct ExitGate : IEquatable<ExitGate>
    {
        public SurfaceCoordinate Cell { get; }
        public SurfaceDirection Direction { get; }
        public ExitGate(SurfaceCoordinate cell, SurfaceDirection direction)
        { SurfaceDirections.Validate(direction); Cell = cell; Direction = direction; }
        public bool Equals(ExitGate other) => Cell == other.Cell && Direction == other.Direction;
        public override bool Equals(object obj) => obj is ExitGate other && Equals(other);
        public override int GetHashCode() => unchecked(Cell.GetHashCode() * 397 ^ (int)Direction);
    }

    public sealed class ExitMap
    {
        private readonly HashSet<CellGate> lookup = new HashSet<CellGate>();
        private IReadOnlyList<ExitGate> cubeGates;
        public ISurfaceTopology Surface { get; }
        public IReadOnlyList<CellGate> CellGates { get; }
        public int GridSize => CubeTopologyAdapter.Require(Surface).GridSize;
        public IReadOnlyList<ExitGate> Gates
        {
            get
            {
                if(cubeGates==null)
                { var cube=CubeTopologyAdapter.Require(Surface); var copy=new List<ExitGate>(); foreach(var gate in CellGates) copy.Add(new ExitGate(cube.GetCoordinate(gate.CellId),gate.Direction)); cubeGates=copy.AsReadOnly(); }
                return cubeGates;
            }
        }

        public ExitMap(CubeTopology topology, IEnumerable<ExitGate> gates)
            : this((ISurfaceTopology)topology,ConvertGates(topology,gates)) { }

        public ExitMap(ISurfaceTopology topology,IEnumerable<CellGate> gates)
        {
            if (topology == null) throw new ArgumentNullException(nameof(topology));
            if (gates == null) throw new ArgumentNullException(nameof(gates));
            Surface=topology;
            var copy = new List<CellGate>();
            foreach (var gate in gates)
            {
                if(!topology.ContainsCell(gate.CellId)) throw new ArgumentOutOfRangeException(nameof(gates),"Gate cell is outside this surface.");
                if (!lookup.Add(gate)) throw new ArgumentException("Duplicate directional exit gate.", nameof(gates));
                copy.Add(gate);
            }
            CellGates = copy.AsReadOnly();
        }

        private static IEnumerable<CellGate> ConvertGates(CubeTopology cube,IEnumerable<ExitGate> gates)
        {
            if(cube==null) throw new ArgumentNullException(nameof(cube));
            if(gates==null) throw new ArgumentNullException(nameof(gates));
            foreach(var gate in gates) yield return new CellGate(cube.GetCellIndex(gate.Cell),gate.Direction);
        }
        public bool CanExit(int cellId,SurfaceDirection direction) => lookup.Contains(new CellGate(cellId,direction));
        public bool CanExit(SurfaceCoordinate cell, SurfaceDirection direction) => CanExit(CubeTopologyAdapter.Require(Surface).GetCellIndex(cell),direction);
    }

    /// <summary>A route sample on the surface, outside a gate along its normal, or beyond an edge along its tangent.</summary>
    public readonly struct PathPosition : IEquatable<PathPosition>
    {
        public SurfaceCoordinate Cell { get; }
        public int ExitDistance { get; }
        public ExitType? ExitType { get; }
        public SurfaceDirection ExitDirection { get; }
        public bool IsOnSurface => ExitDistance == 0;
        public PathPosition(SurfaceCoordinate cell, int exitDistance = 0,
            ExitType exitType = Movement.ExitType.DirectionalGate, SurfaceDirection exitDirection = SurfaceDirection.Right)
        {
            if (exitDistance < 0) throw new ArgumentOutOfRangeException(nameof(exitDistance));
            if ((uint)exitType > 1) throw new ArgumentOutOfRangeException(nameof(exitType));
            SurfaceDirections.Validate(exitDirection);
            Cell = cell; ExitDistance = exitDistance;
            ExitType = exitDistance == 0 ? (ExitType?)null : exitType;
            // Gate travel is normal to the face, so its local tangent direction has no geometric meaning.
            ExitDirection = exitDistance == 0 || exitType == Movement.ExitType.DirectionalGate
                ? SurfaceDirection.Right : exitDirection;
        }
        public bool Equals(PathPosition other) => Cell == other.Cell && ExitDistance == other.ExitDistance
            && ExitType == other.ExitType && ExitDirection == other.ExitDirection;
        public override bool Equals(object obj) => obj is PathPosition other && Equals(other);
        public override int GetHashCode() => unchecked(((Cell.GetHashCode() * 397 ^ ExitDistance) * 397
            ^ ExitType.GetHashCode()) * 397 ^ (int)ExitDirection);
    }
}
