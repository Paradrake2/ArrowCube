using System;
using System.Collections.Generic;
using System.Threading;
using ArrowCube.Movement;
using ArrowCube.Topology;

namespace ArrowCube.Generation
{
    public enum HeuristicStrategy { NearestEdge, ProjectedExitDistance, OutermostLayer, Random }

    [Serializable]
    public sealed class HeuristicSolverReport
    {
        public HeuristicStrategy Strategy;
        public int SuccessfulRemovals, FailedAttempts, TotalAttempts;
        public bool Completed;
        public bool AttemptTraceTruncated;
        public double SuccessRate, FailuresPerRemoval;
        public int[] AttemptedPaths, RemovedPaths;
    }

    /// <summary>Only ranking, attempted gameplay simulations and observed results; no solution or dependency graph.</summary>
    public sealed class HeuristicPuzzleSolver
    {
        public HeuristicSolverReport Solve(PuzzleDefinition puzzle,HeuristicStrategy strategy,int seed=0,
            CancellationToken cancellationToken=default)
        {
            if(puzzle==null) throw new ArgumentNullException(nameof(puzzle));
            var grid=puzzle.CreateGrid(); var simulator=new PathMovementSimulator();
            var order=new List<ArrowPath>(puzzle.Paths); var random=new StableRandom(seed,(int)strategy);
            if(strategy!=HeuristicStrategy.Random)
            {
                var ranks=new Dictionary<int,double>(); foreach(var path in order) ranks.Add(path.Id,Rank(puzzle,path,strategy));
                var ties=new Dictionary<int,uint>(); foreach(var path in order) ties.Add(path.Id,random.NextUInt());
                order.Sort((a,b)=> { int result=ranks[a.Id].CompareTo(ranks[b.Id]); if(result!=0) return result;
                    result=ties[a.Id].CompareTo(ties[b.Id]); return result!=0 ? result : a.Id.CompareTo(b.Id); });
            }
            var attempted=new List<int>(); var removed=new List<int>();
            var knownBlocked=new HashSet<int>();
            var available=new List<ArrowPath>(order); int cursor=0,totalAttempts=0;
            while(grid.Paths.Count>0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArrowPath next=null;
                if(strategy==HeuristicStrategy.Random)
                {
                    if(available.Count==0) break;
                    int pick=random.Next(available.Count); next=available[pick]; available.RemoveAt(pick);
                }
                else
                    while(cursor<order.Count)
                    { var path=order[cursor++]; if(grid.Paths.ContainsKey(path.Id)) { next=path; break; } }
                if(next==null) break;
                totalAttempts++; if(attempted.Count<4096) attempted.Add(next.Id);
                var plan=simulator.Simulate(grid,next.Id,puzzle.Exits,puzzle.Rules);
                if(plan.Succeeded && grid.TryCommit(plan))
                {
                    removed.Add(next.Id); knownBlocked.Clear(); cursor=0;
                    if(strategy==HeuristicStrategy.Random) { available.Clear(); foreach(var path in order) if(grid.Paths.ContainsKey(path.Id)) available.Add(path); }
                }
                else knownBlocked.Add(next.Id); // Retry only after an observed state change.
            }
            int failed=totalAttempts-removed.Count;
            return new HeuristicSolverReport { Strategy=strategy,SuccessfulRemovals=removed.Count,FailedAttempts=failed,
                TotalAttempts=totalAttempts,Completed=grid.Paths.Count==0,AttemptTraceTruncated=totalAttempts>attempted.Count,
                SuccessRate=removed.Count/(double)Math.Max(1,totalAttempts),FailuresPerRemoval=failed/(double)Math.Max(1,removed.Count),
                AttemptedPaths=attempted.ToArray(),RemovedPaths=removed.ToArray() };
        }

        public static int EdgeDistance(SurfaceCoordinate cell,int size) =>
            Math.Min(Math.Min(cell.X,cell.Y),Math.Min(size-1-cell.X,size-1-cell.Y));

        private static double Rank(PuzzleDefinition puzzle,ArrowPath path,HeuristicStrategy strategy)
        {
            if(strategy==HeuristicStrategy.NearestEdge) return puzzle.Surface.Generation().ExitDistance(path.HeadCellId);
            if(strategy==HeuristicStrategy.OutermostLayer)
            {
                int min=int.MaxValue; double average=0;
                foreach(var cell in path.CellIds) { int d=puzzle.Surface.Generation().ExitDistance(cell); min=Math.Min(min,d); average+=d; }
                return min+average/path.CellIds.Count/(puzzle.Surface.Generation().Scale+1);
            }
            // Geometric projection ignores occupancy and does not ask whether this move is safe.
            var head=path.HeadCellId; var heading=path.HeadDirection;
            for(int distance=0;distance<=puzzle.Surface.Generation().RouteLimit;distance++)
            {
                var step=puzzle.Surface.Step(head,heading);
                if(puzzle.Rules.EnableCubeEdgeExits && step.AllowsEdgeExit || puzzle.Rules.EnableDirectionalGates && puzzle.Exits.CanExit(head,heading)) return distance;
                head=step.CellId; heading=step.Direction;
            }
            return double.MaxValue;
        }
    }
}
