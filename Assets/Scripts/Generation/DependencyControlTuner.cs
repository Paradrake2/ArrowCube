using System;
using System.Collections.Generic;
using System.Threading;
using ArrowCube.Movement;
using ArrowCube.Topology;

namespace ArrowCube.Generation
{
    /// <summary>Lightweight physical blocker tuning, independent of measured difficulty search.
    /// First keep every occupied cell fixed and change arrow orientation. Small endpoint
    /// adjustments are a bounded fallback; every accepted layout has a gameplay solution.</summary>
    internal sealed class DependencyControlTuner
    {
        private readonly PathMovementSimulator sim=new PathMovementSimulator();
        private readonly PuzzleValidator validator=new PuzzleValidator();
        private sealed class State
        {
            public ArrowPath Path,Reverse;
            public HashSet<int> Blockers,ReverseBlockers;
        }
        public IEnumerable<GeneratedPuzzle> TuneSteps(GeneratedPuzzle source,StableRandom random,CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); yield return null;
            var settings=source.Recipe.DependencyControlSettings; var paths=new List<ArrowPath>(source.Paths);
            var states=Build(source,paths,token); int flips=0,reroutes=0,endpointTrials=0;
            int passes=settings.AdjustmentPasses==0 ? paths.Count*2 : settings.AdjustmentPasses;
            // Each endpoint trial can require a complete replay. Cap the automatic
            // fallback so large puzzles do not spend minutes chasing a soft average.
            int endpointBudget=settings.EndpointAttempts==0 ? Math.Min(256,Math.Max(64,paths.Count*4)) : settings.EndpointAttempts;
            for(int pass=0;pass<passes;pass++)
            {
                yield return null;
                token.ThrowIfCancellationRequested(); Counts(states,out int free,out int links);
                double average=links/(double)Math.Max(1,states.Count-free);
                if(free<=settings.MaximumStartingFreeArrows && average>=settings.TargetBlockersPerBlockedArrow-.001) break;
                int selected=-1; double bestGain=0;
                int start=random.Next(states.Count);
                for(int offset=0;offset<states.Count;offset++)
                {
                    yield return null;
                    token.ThrowIfCancellationRequested(); int index=(start+offset)%states.Count; var state=states[index];
                    if(state.Reverse==null || state.ReverseBlockers.Count<=state.Blockers.Count) continue;
                    bool reducesFree=state.Blockers.Count==0;
                    if(free>settings.MaximumStartingFreeArrows && !reducesFree) continue;
                    if(CreatesCycle(state.Path.Id,state.ReverseBlockers,states,token)) continue;
                    double gain=(reducesFree ? 1000 : 0)+state.ReverseBlockers.Count-state.Blockers.Count;
                    if(gain>bestGain) { bestGain=gain; selected=index; }
                }
                if(selected>=0)
                {
                    var state=states[selected]; var old=state.Path; var oldBlockers=state.Blockers;
                    state.Path=state.Reverse; state.Blockers=state.ReverseBlockers; state.Reverse=old; state.ReverseBlockers=oldBlockers;
                    paths[selected]=state.Path; flips++; continue;
                }
                if(endpointTrials>=endpointBudget) break;
                double cost=Cost(states,settings);
                bool changed=false;
                var grid=new SurfaceGrid(source.Topology,paths);
                for(int offset=0;offset<paths.Count && !changed && endpointTrials<endpointBudget;offset++)
                {
                    int index=(start+offset)%paths.Count; var original=paths[index];
                    if(free>settings.MaximumStartingFreeArrows && states[index].Blockers.Count!=0) continue;
                    for(int end=0;end<2 && !changed;end++)
                    for(int direction=0;direction<4 && !changed && endpointTrials<endpointBudget;direction++)
                    {
                        token.ThrowIfCancellationRequested();
                        var cells=new List<SurfaceCoordinate>(original.Cells);
                        int endpoint=end==0 ? 0 : cells.Count-1,neighbor=end==0 ? 1 : cells.Count-2;
                        var next=source.Topology.Step(cells[neighbor],(SurfaceDirection)direction).Coordinate;
                        if(next==cells[endpoint] || cells.Contains(next) || grid.TryGetCellOwner(next,out _)) continue;
                        cells[endpoint]=next; endpointTrials++;
                        yield return null;
                        source.Topology.TryGetDirection(cells[1],cells[0],out var incoming);
                        var heading=source.Topology.Step(cells[1],incoming).Direction;
                        var replacement=new ArrowPath(original.Id,source.Topology,cells,heading);
                        var solo=sim.Simulate(new SurfaceGrid(source.Topology,new[] { replacement }),replacement.Id,source.Exits,source.Rules);
                        if(!InitialExitClearance.IsClear(solo)) continue;
                        var proposed=new List<ArrowPath>(paths); proposed[index]=replacement;
                        var definition=new PuzzleDefinition(source.Name,source.Topology,proposed,source.Exits,source.Rules);
                        var validation=validator.Validate(definition,cancellationToken:token,requireClearInitialExit:true);
                        if(!validation.Succeeded || PuzzleGenerator.QualityError(source.Recipe,validation.Statistics,false).Length!=0) continue;
                        var nextStates=Build(source,proposed,token);
                        if(Cost(nextStates,settings)>=cost-.00001) continue;
                        paths=proposed; states=nextStates; reroutes++; changed=true;
                    }
                }
                if(!changed) break;
            }
            var finalDefinition=new PuzzleDefinition(source.Name,source.Topology,paths,source.Exits,source.Rules);
            var final=validator.Validate(finalDefinition,cancellationToken:token,requireClearInitialExit:true);
            if(!final.Succeeded || PuzzleGenerator.QualityError(source.Recipe,final.Statistics,false).Length!=0) { yield return source; yield break; }
            var result=new GeneratedPuzzle(source.Recipe,finalDefinition,final,source.Report);
            result.DependencyControlReport=MakeReport(source.Recipe,final.Statistics,null);
            result.DependencyControlReport.OrientationChanges=flips; result.DependencyControlReport.EndpointChanges=reroutes;
            yield return result;
        }
        internal static DependencyControlReport MakeReport(PuzzleRecipe recipe,PuzzleStatistics stats,DependencyControlReport previous)
        {
            var settings=recipe.DependencyControlSettings; if(settings==null) return null;
            double average=stats.DependencyCount/(double)Math.Max(1,stats.BlockedPaths);
            return new DependencyControlReport { InitiallyFree=stats.ImmediatelyRemovablePaths,MaximumStartingFreeArrows=settings.MaximumStartingFreeArrows,
                AverageBlockersPerBlockedArrow=average,TargetAverageBlockers=settings.TargetBlockersPerBlockedArrow,
                BlockerTargetReached=average>=settings.TargetBlockersPerBlockedArrow-.001,
                OrientationChanges=previous?.OrientationChanges??0,EndpointChanges=previous?.EndpointChanges??0 };
        }
        private List<State> Build(PuzzleDefinition source,List<ArrowPath> paths,CancellationToken token)
        {
            var grid=new SurfaceGrid(source.Topology,paths); var result=new List<State>();
            foreach(var path in paths)
            {
                token.ThrowIfCancellationRequested(); var state=new State { Path=path,Blockers=Find(source,grid,path) };
                var cells=new List<SurfaceCoordinate>(path.Cells); cells.Reverse();
                source.Topology.TryGetDirection(cells[1],cells[0],out var incoming);
                var reversed=new ArrowPath(path.Id,source.Topology,cells,source.Topology.Step(cells[1],incoming).Direction);
                var alone=sim.Simulate(new SurfaceGrid(source.Topology,new[] { reversed }),reversed.Id,source.Exits,source.Rules);
                if(InitialExitClearance.IsClear(alone)) { state.Reverse=reversed; state.ReverseBlockers=Find(source,grid,reversed); }
                result.Add(state);
            }
            return result;
        }
        private HashSet<int> Find(PuzzleDefinition source,SurfaceGrid grid,ArrowPath path)
        {
            var route=sim.Simulate(new SurfaceGrid(source.Topology,new[] { path }),path.Id,source.Exits,source.Rules);
            var owners=new HashSet<int>();
            for(int i=path.Cells.Count;i<route.Rail.Count;i++)
                if(route.Rail[i].IsOnSurface && grid.TryGetCellOwner(route.Rail[i].Cell,out int id) && id!=path.Id) owners.Add(id);
            return owners;
        }
        private static bool CreatesCycle(int id,HashSet<int> proposed,List<State> states,CancellationToken token)
        {
            var lookup=new Dictionary<int,HashSet<int>>(); foreach(var state in states) lookup.Add(state.Path.Id,state.Blockers);
            var seen=new HashSet<int>(); var stack=new Stack<int>(proposed);
            while(stack.Count>0) { token.ThrowIfCancellationRequested(); int next=stack.Pop(); if(next==id) return true; if(!seen.Add(next)) continue; foreach(int dependency in lookup[next]) stack.Push(dependency); }
            return false;
        }
        private static void Counts(List<State> states,out int free,out int links)
        { free=0; links=0; foreach(var state in states) { if(state.Blockers.Count==0) free++; links+=state.Blockers.Count; } }
        private static double Cost(List<State> states,DependencyControlSettings settings)
        {
            Counts(states,out int free,out int links);
            return Math.Max(0,free-settings.MaximumStartingFreeArrows)*10000
                +100*Math.Max(0,settings.TargetBlockersPerBlockedArrow-links/(double)Math.Max(1,states.Count-free))-.0001*links;
        }
    }
}
