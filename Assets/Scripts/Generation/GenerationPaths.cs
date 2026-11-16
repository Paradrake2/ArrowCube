using System;
using System.Collections.Generic;
using ArrowCube.Topology;

namespace ArrowCube.Generation
{
    internal static class GenerationPaths
    {
        public static ArrowPath Create(int id,ISurfaceTopology surface,IEnumerable<int> cells,SurfaceDirection heading)
        {
            var copy=new List<int>(cells); var directions=new List<SurfaceDirection>();
            for(int i=0;i+1<copy.Count;i++)
            {
                if(i==0)
                {
                    var direction=heading.Opposite();
                    if(surface.Step(copy[i],direction).CellId!=copy[i+1]) throw new ArgumentException("Head/body heading mismatch.");
                    directions.Add(direction);
                }
                else
                {
                    if(!surface.TryGenerationDirection(copy[i],copy[i+1],out var direction)) throw new ArgumentException("Disconnected generated body.");
                    directions.Add(direction);
                }
            }
            return new ArrowPath(id,surface,copy,heading,directions);
        }
        // Multiple boundary slots may connect the same two IDs on a small closed surface.
        // Generation uses an explicit, stable slot rather than ambiguous cell-only inference.
        public static bool TryGenerationDirection(this ISurfaceTopology surface,int from,int to,out SurfaceDirection direction)
        {
            for(int d=0;d<4;d++) { direction=(SurfaceDirection)d; if(surface.Step(from,direction).CellId==to) return true; }
            direction=default; return false;
        }
    }
}
