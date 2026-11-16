using System;
using ArrowCube.Topology;

namespace ArrowCube.Movement
{
    /// <summary>Puzzle-owned directional gate, also usable as a directed head-state key.</summary>
    public readonly struct CellGate : IEquatable<CellGate>
    {
        public int CellId { get; }
        public SurfaceDirection Direction { get; }
        public CellGate(int cellId,SurfaceDirection direction)
        { if(cellId<0) throw new ArgumentOutOfRangeException(nameof(cellId)); SurfaceDirections.Validate(direction); CellId=cellId; Direction=direction; }
        public bool Equals(CellGate other) => CellId==other.CellId && Direction==other.Direction;
        public override bool Equals(object obj) => obj is CellGate other && Equals(other);
        public override int GetHashCode() => unchecked(CellId*397^(int)Direction);
    }

    /// <summary>A logical route sample. Off-surface positions retain their departure cell ID.</summary>
    public readonly struct CellPathPosition : IEquatable<CellPathPosition>
    {
        public int CellId { get; }
        public int ExitDistance { get; }
        public ExitType? ExitType { get; }
        public SurfaceDirection ExitDirection { get; }
        public bool IsOnSurface => ExitDistance==0;
        public CellPathPosition(int cellId,int exitDistance=0,ExitType exitType=Movement.ExitType.DirectionalGate,SurfaceDirection exitDirection=SurfaceDirection.Right)
        {
            if(cellId<0) throw new ArgumentOutOfRangeException(nameof(cellId));
            if(exitDistance<0) throw new ArgumentOutOfRangeException(nameof(exitDistance));
            if((uint)exitType>1) throw new ArgumentOutOfRangeException(nameof(exitType));
            SurfaceDirections.Validate(exitDirection);
            CellId=cellId; ExitDistance=exitDistance; ExitType=exitDistance==0 ? (ExitType?)null : exitType;
            ExitDirection=exitDistance==0 || exitType==Movement.ExitType.DirectionalGate ? SurfaceDirection.Right : exitDirection;
        }
        public bool Equals(CellPathPosition other) => CellId==other.CellId && ExitDistance==other.ExitDistance && ExitType==other.ExitType && ExitDirection==other.ExitDirection;
        public override bool Equals(object obj) => obj is CellPathPosition other && Equals(other);
        public override int GetHashCode() => unchecked(((CellId*397^ExitDistance)*397^ExitType.GetHashCode())*397^(int)ExitDirection);
        public PathPosition ToCube(CubeTopology cube) => new PathPosition(cube.GetCoordinate(CellId),ExitDistance,ExitType??Movement.ExitType.DirectionalGate,ExitDirection);
    }
}
