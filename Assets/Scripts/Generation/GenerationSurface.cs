using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ArrowCube.Topology;

namespace ArrowCube.Generation
{
    /// <summary>Stable dense sampling indices and connected coplanar regions. Geometry flags
    /// come from the author; no mesh inspection or shape-specific traversal is performed.</summary>
    public sealed class GenerationSurface
    {
        private static readonly ConditionalWeakTable<ISurfaceTopology,GenerationSurface> Cache=new ConditionalWeakTable<ISurfaceTopology,GenerationSurface>();
        private readonly Dictionary<int,int> indices=new Dictionary<int,int>(), regions=new Dictionary<int,int>(), distances=new Dictionary<int,int>();
        private readonly int[] ids;
        public ISurfaceTopology Surface { get; }
        public int RegionCount { get; }
        public int Scale { get; }
        public int RouteLimit => Surface is CubeTopology ? 4*Scale : 4*Surface.CellCount;
        public bool HasEdgeExits { get; }
        public IReadOnlyList<CellGateSeed> Folds { get; }
        internal static GenerationSurface For(ISurfaceTopology surface) => Cache.GetValue(surface,s=>new GenerationSurface(s));
        private GenerationSurface(ISurfaceTopology surface)
        {
            Surface=surface; var ordered=new List<int>(surface.EnumerateCellIds()); ordered.Sort(); ids=ordered.ToArray();
            for(int i=0;i<ids.Length;i++) indices.Add(ids[i],i);
            int count=0; var queue=new Queue<int>(); var folds=new List<CellGateSeed>();
            foreach(int cell in ids)
            {
                for(int d=0;d<4;d++)
                {
                    var b=surface.GetBoundary(cell,(SurfaceDirection)d);
                    if(b.AllowsEdgeExit) HasEdgeExits=true;
                    if(b.IsGeometricEdge) folds.Add(new CellGateSeed(cell,(SurfaceDirection)d));
                }
                if(regions.ContainsKey(cell)) continue;
                regions.Add(cell,count); queue.Enqueue(cell);
                while(queue.Count>0)
                {
                    int next=queue.Dequeue();
                    for(int d=0;d<4;d++)
                    {
                        var b=surface.GetBoundary(next,(SurfaceDirection)d); int neighbor=b.NeighborCellId.Value;
                        if(!b.IsGeometricEdge && !regions.ContainsKey(neighbor)) { regions.Add(neighbor,count); queue.Enqueue(neighbor); }
                    }
                }
                count++;
            }
            // Cube enum order is part of the historical deterministic heuristic contract.
            if(surface is CubeTopology c) foreach(int cell in ids) regions[cell]=(int)c.GetCoordinate(cell).Face;
            RegionCount=count; Folds=folds.AsReadOnly();
            Scale=surface is CubeTopology cube ? cube.GridSize : Math.Max(2,(int)Math.Ceiling(Math.Sqrt(surface.CellCount/(double)count)));
            foreach(int cell in ids)
                for(int d=0;d<4;d++) if(surface.GetBoundary(cell,(SurfaceDirection)d).AllowsEdgeExit)
                { distances[cell]=0; queue.Enqueue(cell); break; }
            if(queue.Count==0) foreach(int cell in ids)
                for(int d=0;d<4;d++) if(surface.GetBoundary(cell,(SurfaceDirection)d).IsGeometricEdge)
                { distances[cell]=0; queue.Enqueue(cell); break; }
            while(queue.Count>0)
            {
                int cell=queue.Dequeue();
                for(int d=0;d<4;d++)
                {
                    int neighbor=surface.Step(cell,(SurfaceDirection)d).CellId;
                    if(!distances.ContainsKey(neighbor)) { distances.Add(neighbor,distances[cell]+1); queue.Enqueue(neighbor); }
                }
            }
        }
        public int CellAt(int index) => ids[index];
        public int IndexOf(int cell) => indices[cell];
        public int RegionOf(int cell) => regions[cell];
        public int ExitDistance(int cell)
        {
            if(Surface is CubeTopology cube) return HeuristicPuzzleSolver.EdgeDistance(cube.GetCoordinate(cell),cube.GridSize);
            return distances.TryGetValue(cell,out int value) ? value : 0;
        }
        public int[] Regions(ArrowPath path)
        { var set=new SortedSet<int>(); foreach(int cell in path.CellIds) set.Add(RegionOf(cell)); var result=new int[set.Count]; set.CopyTo(result); return result; }
    }
    public readonly struct CellGateSeed
    {
        public readonly int Cell; public readonly SurfaceDirection Direction;
        public CellGateSeed(int cell,SurfaceDirection direction) { Cell=cell; Direction=direction; }
    }
    internal static class GenerationSurfaceExtensions
    {
        public static GenerationSurface Generation(this ISurfaceTopology surface) => GenerationSurface.For(surface);
        public static int GetCoordinate(this ISurfaceTopology surface,int index) => surface.Generation().CellAt(index);
        public static int GetCellIndex(this ISurfaceTopology surface,int cell) => surface.Generation().IndexOf(cell);
    }
}
