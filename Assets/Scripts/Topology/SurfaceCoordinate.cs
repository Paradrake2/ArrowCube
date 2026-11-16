using System;

namespace ArrowCube.Topology
{
    public enum CubeFace { Front, Right, Back, Left, Top, Bottom }

    // Local face coordinates: X grows right, Y grows up when viewed from outside.
    public enum SurfaceDirection { Right, Up, Left, Down }

    public static class SurfaceDirections
    {
        public static SurfaceDirection Opposite(this SurfaceDirection direction)
        {
            Validate(direction);
            return (SurfaceDirection)(((int)direction + 2) % 4);
        }

        public static void Validate(SurfaceDirection direction)
        {
            if ((uint)direction >= 4)
                throw new ArgumentOutOfRangeException(nameof(direction));
        }
    }

    /// <summary>A cell center on one of the six connected face grids.</summary>
    public readonly struct SurfaceCoordinate : IEquatable<SurfaceCoordinate>
    {
        public CubeFace Face { get; }
        public int X { get; }
        public int Y { get; }

        public SurfaceCoordinate(CubeFace face, int x, int y)
        {
            if ((uint)face >= 6) throw new ArgumentOutOfRangeException(nameof(face));
            if (x < 0) throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0) throw new ArgumentOutOfRangeException(nameof(y));
            Face = face;
            X = x;
            Y = y;
        }

        public bool Equals(SurfaceCoordinate other) => Face == other.Face && X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is SurfaceCoordinate other && Equals(other);
        public override int GetHashCode() => unchecked((((int)Face * 397) ^ X) * 397 ^ Y);
        public static bool operator ==(SurfaceCoordinate a, SurfaceCoordinate b) => a.Equals(b);
        public static bool operator !=(SurfaceCoordinate a, SurfaceCoordinate b) => !a.Equals(b);
        public override string ToString() => $"{Face}({X}, {Y})";
    }

    /// <summary>An exact integer vector; topology never depends on floating point or Unity.</summary>
    public readonly struct GridVector3 : IEquatable<GridVector3>
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public GridVector3(int x, int y, int z) { X = x; Y = y; Z = z; }
        public static GridVector3 operator +(GridVector3 a, GridVector3 b) =>
            new GridVector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static GridVector3 operator -(GridVector3 a) => new GridVector3(-a.X, -a.Y, -a.Z);
        public static GridVector3 operator *(GridVector3 a, int scale) =>
            new GridVector3(a.X * scale, a.Y * scale, a.Z * scale);
        public bool Equals(GridVector3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is GridVector3 other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397 ^ Y) * 397 ^ Z);
        public static bool operator ==(GridVector3 a, GridVector3 b) => a.Equals(b);
        public static bool operator !=(GridVector3 a, GridVector3 b) => !a.Equals(b);
        public override string ToString() => $"({X}, {Y}, {Z})";
    }

    public readonly struct FaceOrientation
    {
        public GridVector3 Normal { get; }
        public GridVector3 Right { get; }
        public GridVector3 Up { get; }

        internal FaceOrientation(GridVector3 normal, GridVector3 right, GridVector3 up)
        { Normal = normal; Right = right; Up = up; }

        public GridVector3 GetTangent(SurfaceDirection direction)
        {
            switch (direction)
            {
                case SurfaceDirection.Right: return Right;
                case SurfaceDirection.Up: return Up;
                case SurfaceDirection.Left: return -Right;
                case SurfaceDirection.Down: return -Up;
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }
    }
}
