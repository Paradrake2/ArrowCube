using System;
using System.Collections.Generic;

namespace ArrowCube.Topology
{
    /// <summary>An immutable cell. IDs have no dependency on cube faces or coordinates.</summary>
    public sealed class SurfaceCell
    {
        public int Id { get; }
        public IReadOnlyList<CellBoundary> Boundaries { get; }

        internal SurfaceCell(int id,IReadOnlyList<CellBoundary> boundaries)
        {
            if(id<0) throw new ArgumentOutOfRangeException(nameof(id));
            if(boundaries==null || boundaries.Count!=4) throw new ArgumentException("Four boundaries are required.",nameof(boundaries));
            var copy=new CellBoundary[4];
            for(int i=0;i<copy.Length;i++) copy[i]=boundaries[i]??throw new ArgumentException("Every boundary must be configured.",nameof(boundaries));
            Id=id; Boundaries=Array.AsReadOnly(copy);
        }

        // Preserve SurfaceDirection's serialized values: Right=0, Up=1, Left=2, Down=3.
        public CellBoundary GetBoundary(SurfaceDirection direction)
        { SurfaceDirections.Validate(direction); return Boundaries[(int)direction]; }
    }
}
