using System;
using System.Collections.Generic;

namespace ArrowCube.Topology
{
    /// <summary>Compatibility views for the existing cube generator, geometry and file formats.</summary>
    public static class CubeTopologyAdapter
    {
        public static CubeTopology Require(ISurfaceTopology surface) => surface as CubeTopology
            ?? throw new NotSupportedException("This coordinate API requires a cube. Use Surface and cell-ID APIs for other topologies.");
        public static IReadOnlyList<int> CellIds(CubeTopology cube,IEnumerable<SurfaceCoordinate> coordinates)
        {
            if(cube==null) throw new ArgumentNullException(nameof(cube));
            if(coordinates==null) throw new ArgumentNullException(nameof(coordinates));
            var ids=new List<int>(); foreach(var coordinate in coordinates) ids.Add(cube.GetCellIndex(coordinate));
            return ids.AsReadOnly();
        }
        public static bool Compatible(ISurfaceTopology a,ISurfaceTopology b) => ReferenceEquals(a,b)
            || a is CubeTopology ca && b is CubeTopology cb && ca.GridSize==cb.GridSize;
        public static void RequireCompatible(ISurfaceTopology a,ISurfaceTopology b)
        { if(a==null || b==null || !Compatible(a,b)) throw new ArgumentException("Data belongs to a different surface topology."); }
    }
}
