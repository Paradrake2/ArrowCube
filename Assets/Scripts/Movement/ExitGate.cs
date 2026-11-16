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
        private readonly HashSet<ExitGate> lookup = new HashSet<ExitGate>();
        public IReadOnlyList<ExitGate> Gates { get; }
        public int GridSize { get; }

        public ExitMap(CubeTopology topology, IEnumerable<ExitGate> gates)
        {
            if (topology == null) throw new ArgumentNullException(nameof(topology));
            if (gates == null) throw new ArgumentNullException(nameof(gates));
            var copy = new List<ExitGate>();
            foreach (var gate in gates)
            {
                topology.Validate(gate.Cell);
                if (!lookup.Add(gate)) throw new ArgumentException("Duplicate directional exit gate.", nameof(gates));
                copy.Add(gate);
            }
            GridSize = topology.GridSize;
            Gates = copy.AsReadOnly();
        }

        public bool CanExit(SurfaceCoordinate cell, SurfaceDirection direction) => lookup.Contains(new ExitGate(cell, direction));
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
