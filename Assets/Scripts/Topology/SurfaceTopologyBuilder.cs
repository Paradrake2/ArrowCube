using System;
using System.Collections.Generic;

namespace ArrowCube.Topology
{
    /// <summary>Mutable construction, followed by closed-surface validation and immutable publication.</summary>
    public sealed class SurfaceTopologyBuilder
    {
        private readonly Dictionary<int,CellBoundary[]> cells=new Dictionary<int,CellBoundary[]>();

        public SurfaceTopologyBuilder AddCell(int cellId)
        {
            if(cellId<0) throw new ArgumentOutOfRangeException(nameof(cellId));
            if(cells.ContainsKey(cellId)) throw new ArgumentException("Duplicate cell ID: "+cellId,nameof(cellId));
            cells.Add(cellId,new CellBoundary[4]); return this;
        }

        public CellBoundary GetBoundary(int cellId,SurfaceDirection direction)
        { SurfaceDirections.Validate(direction); return Find(cellId)[(int)direction]; }

        public SurfaceTopologyBuilder SetBoundary(int cellId,SurfaceDirection direction,CellBoundary boundary)
        {
            SurfaceDirections.Validate(direction);
            if(boundary==null) throw new ArgumentNullException(nameof(boundary));
            Find(cellId)[(int)direction]=boundary; return this;
        }

        /// <summary>Connects both ends, including the inverse heading transformation.</summary>
        public SurfaceTopologyBuilder Connect(int cellId,SurfaceDirection direction,int neighborCellId,SurfaceDirection newDirection,
            bool isGeometricEdge=false,bool allowsEdgeExit=false)
        {
            SurfaceDirections.Validate(direction); SurfaceDirections.Validate(newDirection);
            var source=Find(cellId); var destination=Find(neighborCellId);
            source[(int)direction]=new CellBoundary(neighborCellId,newDirection,isGeometricEdge,allowsEdgeExit);
            destination[(int)newDirection.Opposite()]=new CellBoundary(cellId,direction.Opposite(),isGeometricEdge,allowsEdgeExit);
            return this;
        }

        public SurfaceTopology Build()
        {
            if(cells.Count==0) throw new InvalidOperationException("A topology needs at least one cell.");
            var ordered=new List<int>(cells.Keys); ordered.Sort();
            foreach(int id in ordered)
            for(int d=0;d<4;d++)
            {
                var direction=(SurfaceDirection)d; var boundary=GetBoundary(id,direction);
                if(boundary==null) throw new InvalidOperationException($"Cell {id}/{direction} is unconfigured.");
                if(boundary.Kind==BoundaryConnectionKind.Open)
                    throw new NotSupportedException($"Cell {id}/{direction} is intentionally open. This phase supports closed surfaces only.");
                if(!boundary.NeighborCellId.HasValue || !cells.ContainsKey(boundary.NeighborCellId.Value))
                    throw new InvalidOperationException($"Cell {id}/{direction} references a missing destination.");
                var reverse=GetBoundary(boundary.NeighborCellId.Value,boundary.NewDirection.Opposite());
                if(reverse==null || reverse.Kind!=BoundaryConnectionKind.Connected || reverse.NeighborCellId!=id || reverse.NewDirection!=direction.Opposite())
                    throw new InvalidOperationException($"Cell {id}/{direction} does not have a reversible connection with the correct heading.");
            }
            var snapshot=new Dictionary<int,SurfaceCell>(cells.Count);
            foreach(int id in ordered) snapshot.Add(id,new SurfaceCell(id,cells[id]));
            return new SurfaceTopology(snapshot,ordered.ToArray());
        }

        private CellBoundary[] Find(int cellId) => cells.TryGetValue(cellId,out var boundaries)
            ? boundaries : throw new ArgumentOutOfRangeException(nameof(cellId),"Cell must be added before configuring its boundaries.");
    }
}
