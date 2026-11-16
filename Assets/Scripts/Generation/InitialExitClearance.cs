using System.Collections.Generic;
using ArrowCube.Movement;
using ArrowCube.Topology;

namespace ArrowCube.Generation
{
    /// <summary>Generation rule stricter than legal tail-vacating snake motion: the head's
    /// exit corridor must not cross any of the arrow's starting body cells.</summary>
    public static class InitialExitClearance
    {
        public static bool IsClear(MovementPlan unobstructed)
        {
            if(!unobstructed.Succeeded) return false;
            var body=new HashSet<SurfaceCoordinate>(unobstructed.Path.Cells);
            for(int i=unobstructed.Path.Cells.Count;i<unobstructed.Rail.Count;i++)
                if(unobstructed.Rail[i].IsOnSurface && body.Contains(unobstructed.Rail[i].Cell)) return false;
            return true;
        }
    }
}
