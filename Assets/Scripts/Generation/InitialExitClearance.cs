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
            var body=new HashSet<int>(unobstructed.Path.CellIds);
            for(int i=unobstructed.Path.CellIds.Count;i<unobstructed.CellRail.Count;i++)
                if(unobstructed.CellRail[i].IsOnSurface && body.Contains(unobstructed.CellRail[i].CellId)) return false;
            return true;
        }
    }
}
