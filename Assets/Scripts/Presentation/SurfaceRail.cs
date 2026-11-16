using System;
using System.Collections.Generic;
using ArrowCube.Movement;
using ArrowCube.Topology;

namespace ArrowCube.Presentation
{
    /// <summary>Presentation view of a simulator rail, retaining explicit boundary directions.</summary>
    public sealed class SurfaceRail
    {
        public IReadOnlyList<CellPathPosition> Positions { get; }
        public IReadOnlyList<SurfaceDirection> Directions { get; }
        public int Count => Positions.Count;
        public CellPathPosition this[int i] => Positions[i];
        public SurfaceRail(IReadOnlyList<CellPathPosition> positions,IReadOnlyList<SurfaceDirection> directions)
        {
            if(positions==null || positions.Count==0 || directions==null || directions.Count!=positions.Count-1) throw new ArgumentException("Rail directions must match its connections.");
            Positions=positions; Directions=directions;
        }
        public static SurfaceRail FromPath(ArrowPath path)
        {
            var positions=new List<CellPathPosition>(); var directions=new List<SurfaceDirection>();
            for(int i=path.CellIds.Count-1;i>=0;i--)
            {
                positions.Add(new CellPathPosition(path.CellIds[i]));
                if(i>0) directions.Add(path.Surface.GetBoundary(path.CellIds[i-1],path.BodyDirections[i-1]).NewDirection.Opposite());
            }
            return new SurfaceRail(positions.AsReadOnly(),directions.AsReadOnly());
        }
        public static SurfaceRail FromPlan(MovementPlan plan,bool includeAttempted=false)
        {
            var positions=new List<CellPathPosition>(plan.CellRail); var directions=new List<SurfaceDirection>(FromPath(plan.Path).Directions);
            if(includeAttempted && plan.BlockedCellId.HasValue) positions.Add(plan.AttemptedCellHead);
            var heading=plan.Path.HeadDirection;
            for(int i=plan.Path.CellIds.Count-1;i<positions.Count-1;i++)
            {
                directions.Add(heading);
                if(positions[i].IsOnSurface && positions[i+1].IsOnSurface) heading=plan.Path.Surface.Step(positions[i].CellId,heading).Direction;
            }
            return new SurfaceRail(positions.AsReadOnly(),directions.AsReadOnly());
        }
        public static SurfaceRail FromCube(CubeTopology cube,IReadOnlyList<PathPosition> rail,SurfaceDirection initialHeading)
        {
            var positions=new List<CellPathPosition>(); var directions=new List<SurfaceDirection>();
            var departureHeading=initialHeading;
            for(int i=0;i<rail.Count;i++)
            {
                var p=rail[i]; positions.Add(new CellPathPosition(cube.GetCellIndex(p.Cell),p.ExitDistance,p.ExitType??ExitType.DirectionalGate,p.ExitDirection));
                if(i>=rail.Count-1) continue;
                if(p.IsOnSurface && rail[i+1].IsOnSurface) { cube.TryGetDirection(p.Cell,rail[i+1].Cell,out var d); directions.Add(d); departureHeading=cube.Step(p.Cell,d).Direction; }
                else if(p.ExitType==ExitType.CubeEdge || rail[i+1].ExitType==ExitType.CubeEdge) directions.Add(rail[i+1].ExitDirection);
                else
                {
                    directions.Add(departureHeading);
                }
            }
            return new SurfaceRail(positions.AsReadOnly(),directions.AsReadOnly());
        }
    }
}
