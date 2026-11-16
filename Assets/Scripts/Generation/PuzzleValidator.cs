using System;
using System.Collections.Generic;
using System.Threading;
using ArrowCube.Movement;
using ArrowCube.Topology;

namespace ArrowCube.Generation
{
    public sealed class PuzzleStatistics
    {
        public int RegionCount,OccupiedRegions;
        public int[] RegionOccupiedCells;
        public int CrossRegionPaths => CrossFacePaths;
        public int GeometricFolds => FaceTransitions;
        public int ConnectedRegions => ConnectedFaces;
        public int PathCount { get; internal set; }
        public int OccupiedCells { get; internal set; }
        public double Coverage { get; internal set; }
        public int CrossFacePaths { get; internal set; }
        public int FaceTransitions { get; internal set; }
        public int ConnectedFaces { get; internal set; }
        public int TotalTurns { get; internal set; }
        public double AverageTurns => PathCount==0 ? 0 : TotalTurns/(double)PathCount;
        public double AveragePathLength => PathCount==0 ? 0 : OccupiedCells/(double)PathCount;
        public int ImmediatelyRemovablePaths { get; internal set; }
        public int BlockedPaths => PathCount-ImmediatelyRemovablePaths;
        public double BlockedFraction => PathCount==0 ? 0 : BlockedPaths/(double)PathCount;
        public int DependencyCount { get; internal set; }
        public int DependencyDepth { get; internal set; }
        public int EdgeExitsInSolution { get; internal set; }
        public int GateExitsInSolution { get; internal set; }
    }

    public sealed class PuzzleValidation
    {
        public bool Succeeded { get; }
        public string Error { get; }
        public IReadOnlyList<int> Solution { get; }
        public PuzzleStatistics Statistics { get; }
        internal PuzzleValidation(bool success,string error,List<int> solution,PuzzleStatistics statistics)
        { Succeeded=success; Error=error; Solution=solution.AsReadOnly(); Statistics=statistics; }
    }

    /// <summary>Validates with the same simulator and transactional removal used by gameplay.</summary>
    public sealed class PuzzleValidator
    {
        private readonly PathMovementSimulator simulator=new PathMovementSimulator();

        public PuzzleValidation Validate(PuzzleDefinition puzzle,IEnumerable<int> proposedSolution=null,
            CancellationToken cancellationToken=default,bool requireClearInitialExit=false)
        {
            if(puzzle==null) throw new ArgumentNullException(nameof(puzzle));
            var statistics=new PuzzleStatistics();
            var solved=new List<int>();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var grid=puzzle.CreateGrid();
                if(grid.Paths.Count==0) return Fail("Puzzle is empty.");
                Measure(puzzle,grid,statistics,cancellationToken,requireClearInitialExit);
                if(proposedSolution!=null)
                {
                    foreach(int id in proposedSolution)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if(!grid.Paths.ContainsKey(id)) return Fail("Solution contains a duplicate or unknown path ID.");
                        var plan=simulator.Simulate(grid,id,puzzle.Exits,puzzle.Rules);
                        if(!plan.Succeeded || !grid.TryCommit(plan)) return Fail($"Solution path {id} is not removable ({plan.Outcome}).");
                        Record(plan); solved.Add(id);
                    }
                    if(grid.Paths.Count!=0) return Fail("Solution does not remove every path.");
                }
                else
                {
                    // Canonical path order, never dictionary iteration order. Removing a safe path
                    // cannot add a blocker, so any safe choice preserves a solution if one exists.
                    while(grid.Paths.Count>0)
                    {
                        bool progress=false;
                        foreach(var path in puzzle.Paths)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if(!grid.Paths.ContainsKey(path.Id)) continue;
                            var plan=simulator.Simulate(grid,path.Id,puzzle.Exits,puzzle.Rules);
                            if(!plan.Succeeded) continue;
                            if(!grid.TryCommit(plan)) return Fail("Validation state changed unexpectedly.");
                            solved.Add(path.Id); Record(plan); progress=true;
                        }
                        if(!progress) return Fail("No removable path remains; puzzle is unsolvable.");
                    }
                }
                return new PuzzleValidation(true,string.Empty,solved,statistics);
            }
            catch(ArgumentException error) { return Fail(error.Message); }

            PuzzleValidation Fail(string error) => new PuzzleValidation(false,error,solved,statistics);
            void Record(MovementPlan plan)
            { if(plan.ExitType==ExitType.CubeEdge) statistics.EdgeExitsInSolution++; else statistics.GateExitsInSolution++; }
        }

        private void Measure(PuzzleDefinition puzzle,SurfaceGrid grid,PuzzleStatistics stats,CancellationToken token,bool requireClearInitialExit)
        {
            stats.PathCount=grid.Paths.Count; stats.OccupiedCells=grid.OccupiedCellCount;
            stats.Coverage=stats.OccupiedCells/(double)puzzle.Surface.CellCount;
            var regions=puzzle.Surface.Generation(); int regionCount=regions.RegionCount;
            var faceLinks=new HashSet<int>[regionCount]; for(int r=0;r<regionCount;r++) faceLinks[r]=new HashSet<int>();
            var occupiedRegions=new HashSet<int>(); var regionCoverage=new int[regionCount];
            var dependencies=new Dictionary<int,List<int>>();
            foreach(var path in puzzle.Paths)
            {
                token.ThrowIfCancellationRequested();
                if(path.FaceTransitionCount>0) stats.CrossFacePaths++;
                stats.FaceTransitions+=path.FaceTransitionCount;
                stats.TotalTurns+=CountTurns(puzzle.Surface,path);
                foreach(int cell in path.CellIds) { int r=regions.RegionOf(cell); occupiedRegions.Add(r); regionCoverage[r]++; }
                foreach(var segment in path.CellSegments)
                { int a=regions.RegionOf(segment.A),b=regions.RegionOf(segment.B); if(a!=b) { faceLinks[a].Add(b); faceLinks[b].Add(a); } }
                if(simulator.Simulate(grid,path.Id,puzzle.Exits,puzzle.Rules).Succeeded) stats.ImmediatelyRemovablePaths++;
                // The unobstructed route supplies ALL blockers, not just the first collision.
                var alone=new SurfaceGrid(puzzle.Surface,new[] { path });
                var rail=simulator.Simulate(alone,path.Id,puzzle.Exits,puzzle.Rules);
                if(requireClearInitialExit && !InitialExitClearance.IsClear(rail))
                    throw new ArgumentException($"Arrow {path.Id} points into its own starting body or has no unobstructed exit.");
                var blockers=new List<int>();
                if(rail.Succeeded)
                    for(int i=path.CellIds.Count;i<rail.CellRail.Count;i++)
                    {
                        var position=rail.CellRail[i];
                        if(position.IsOnSurface && grid.TryGetCellOwner(position.CellId,out int owner)
                            && owner!=path.Id && !blockers.Contains(owner)) blockers.Add(owner);
                    }
                dependencies.Add(path.Id,blockers); stats.DependencyCount+=blockers.Count;
            }
            stats.RegionCount=regionCount; stats.OccupiedRegions=occupiedRegions.Count; stats.RegionOccupiedCells=regionCoverage;
            var seen=new bool[regionCount]; var queue=new Queue<int>();
            for(int start=0;start<regionCount;start++)
            {
                if(seen[start]) continue; seen[start]=true; queue.Enqueue(start); int component=0;
                while(queue.Count>0)
                {
                    int region=queue.Dequeue(); if(puzzle.Surface is CubeTopology || occupiedRegions.Contains(region)) component++;
                    foreach(int other in faceLinks[region]) if(!seen[other]) { seen[other]=true; queue.Enqueue(other); }
                }
                stats.ConnectedFaces=Math.Max(stats.ConnectedFaces,component);
                if(puzzle.Surface is CubeTopology) break; // Historical root-face-zero component.
            }
            // Iterative topological depth avoids recursion on dense puzzles and detects cycles.
            var depths=new Dictionary<int,int>();
            for(int pass=0;pass<stats.PathCount;pass++)
            {
                bool progress=false;
                foreach(var path in puzzle.Paths)
                {
                    if(depths.ContainsKey(path.Id)) continue;
                    bool ready=true; int depth=1;
                    foreach(int blocker in dependencies[path.Id])
                    { if(!depths.TryGetValue(blocker,out int parent)) { ready=false; break; } depth=Math.Max(depth,parent+1); }
                    if(!ready) continue;
                    depths.Add(path.Id,depth); stats.DependencyDepth=Math.Max(stats.DependencyDepth,depth); progress=true;
                }
                if(!progress) break;
            }
        }

        public static int CountTurns(ISurfaceTopology topology,ArrowPath path)
        {
            int turns=0;
            for(int i=1;i<path.CellIds.Count-1;i++)
            {
                var incoming=path.BodyDirections[i-1]; var outgoing=path.BodyDirections[i];
                if(topology.Step(path.CellIds[i-1],incoming).Direction!=outgoing) turns++;
            }
            return turns;
        }
    }
}
