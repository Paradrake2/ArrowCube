using System;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using ArrowCube.Movement;
using ArrowCube.Topology;

namespace ArrowCube.Generation
{
    public enum DependencyCertainty { Potential, ConfirmedMandatoryFixedRoute }
    [Serializable]
    public sealed class DependencyInterference
    {
        public int Blocker,FaceMask;
        public DependencyCertainty Certainty;
        public int[] CellIds,Regions;
        public int[] Faces,X,Y,FirstSteps,LastSteps;
    }
    [Serializable]
    public sealed class DependencyNode
    {
        public int PathId,FaceMask,Depth;
        public int[] Regions;
        public int[] PotentialBlockers,ConfirmedBlockers,PathsBlocked,IndependentBlockers;
        public bool HasValidExit,InitiallyRemovable,SubsetVerificationPassed,ExhaustiveSubsetsVerified,IndependentWidthExact;
        public int SubsetChecks;
        public DependencyInterference[] Interferences;
    }
    [Serializable]
    public sealed class DependencyGraphEdge
    {
        public int Blocker,Target,InterferenceFaceMask;
        public bool CrossFaceGeometry;
        public DependencyCertainty Certainty;
    }
    [Serializable]
    public sealed class MultiBlockerMetrics
    {
        public double AverageBlockersPerPath,AverageBlockersPerBlockedPath,AtLeastTwoFraction,AtLeastThreeFraction,IndependentMultiBlockerFraction;
        public double IndependentThreeBlockerFraction,AverageIndependentBlockersPerBlockedPath;
        public int ConservativeIndependentWidths;
        public double AverageBranching,CrossFaceGeometryFraction,MaxBlockerConcentration,DependencyConcentration,DistributionEntropy;
        public int MaximumBlockers,IndependentOpeningMoves;
        public int[] BlockerHistogram;
    }

    /// <summary>Complete sweeps -> potential intersections -> pair simulations -> bounded subset checks.
    /// Mandatory labels apply to today's fixed route/static exit priority; no alternate-route assumption.</summary>
    public sealed class PuzzleDependencyGraph
    {
        public DependencyNode[] Nodes { get; private set; }
        public DependencyGraphEdge[] Edges { get; private set; }
        public bool HasCycle { get; private set; }
        public bool IsValid { get; private set; }
        public MultiBlockerMetrics Metrics { get; private set; }
        private readonly Dictionary<int,DependencyNode> lookup=new Dictionary<int,DependencyNode>();
        public DependencyNode GetNode(int id) => lookup[id];
        public string ToDot()
        {
            var result=new StringBuilder("digraph ArrowCube {\n  rankdir=LR;\n");
            foreach(var node in Nodes) result.Append("  p").Append(node.PathId).Append(" [label=\"Arrow ").Append(node.PathId).Append(" / depth ").Append(node.Depth).Append("\"];\n");
            foreach(var edge in Edges) result.Append("  p").Append(edge.Blocker).Append(" -> p").Append(edge.Target).Append(" [label=\"confirmed").Append(edge.CrossFaceGeometry ? " cross-face" : "").Append("\"];\n");
            return result.Append("}\n").ToString();
        }
        public static PuzzleDependencyGraph Analyze(PuzzleDefinition puzzle,CancellationToken token=default,RemovalTrajectoryCache cache=null,int exhaustiveLimit=3)
        {
            if(puzzle==null) throw new ArgumentNullException(nameof(puzzle));
            if(exhaustiveLimit<0 || exhaustiveLimit>4) throw new ArgumentOutOfRangeException(nameof(exhaustiveLimit));
            cache=cache??new RemovalTrajectoryCache();
            var graph=new PuzzleDependencyGraph(); var grid=puzzle.CreateGrid(); var simulator=new PathMovementSimulator();
            var paths=new Dictionary<int,ArrowPath>(); var edges=new List<DependencyGraphEdge>(); var outgoing=new Dictionary<int,List<int>>();
            foreach(var path in puzzle.Paths) { paths.Add(path.Id,path); outgoing.Add(path.Id,new List<int>()); }
            bool valid=true;
            foreach(var target in puzzle.Paths)
            {
                token.ThrowIfCancellationRequested(); var trajectory=cache.Get(puzzle.Surface,target,puzzle.Exits,puzzle.Rules,token);
                var initial=new HashSet<int>(target.CellIds);
                var hits=new SortedDictionary<int,List<CellTrajectoryVisit>>();
                foreach(var visit in trajectory.CellVisits)
                    if(!initial.Contains(visit.CellId) && grid.TryGetCellOwner(visit.CellId,out int owner) && owner!=target.Id)
                    { if(!hits.TryGetValue(owner,out var list)) hits.Add(owner,list=new List<CellTrajectoryVisit>()); list.Add(visit); }
                foreach(var segment in trajectory.SweptCellSegments)
                    if(grid.TryGetSegmentOwner(segment,out int owner) && owner!=target.Id && !hits.ContainsKey(owner)) hits.Add(owner,new List<CellTrajectoryVisit>());
                var confirmed=new List<int>(); var evidence=new List<DependencyInterference>(); var potential=new List<int>(hits.Keys);
                foreach(var pair in hits)
                {
                    token.ThrowIfCancellationRequested();
                    var isolated=new SurfaceGrid(puzzle.Surface,new[] { target,paths[pair.Key] });
                    var attempt=simulator.Simulate(isolated,target.Id,puzzle.Exits,puzzle.Rules);
                    bool required=trajectory.HasValidExit && attempt.Outcome==MovementOutcome.BlockedByPath && attempt.BlockingPathId==pair.Key;
                    var proof=new DependencyInterference { Blocker=pair.Key,Certainty=required ? DependencyCertainty.ConfirmedMandatoryFixedRoute : DependencyCertainty.Potential };
                    int n=pair.Value.Count; proof.CellIds=new int[n]; proof.Regions=new int[n]; proof.Faces=new int[n]; proof.X=new int[n]; proof.Y=new int[n]; proof.FirstSteps=new int[n]; proof.LastSteps=new int[n];
                    for(int i=0;i<n;i++) { var v=pair.Value[i]; proof.CellIds[i]=v.CellId; proof.Regions[i]=puzzle.Surface.Generation().RegionOf(v.CellId);
                        if(puzzle.Surface is CubeTopology cube) { var c=cube.GetCoordinate(v.CellId); proof.Faces[i]=(int)c.Face; proof.X[i]=c.X; proof.Y[i]=c.Y; } proof.FirstSteps[i]=v.FirstStep; proof.LastSteps[i]=v.LastStep; if(puzzle.Surface is CubeTopology) proof.FaceMask|=1<<proof.Faces[i]; }
                    evidence.Add(proof);
                    if(!required) continue;
                    confirmed.Add(pair.Key); outgoing[pair.Key].Add(target.Id);
                    edges.Add(new DependencyGraphEdge { Blocker=pair.Key,Target=target.Id,InterferenceFaceMask=proof.FaceMask,
                        // A target's unrelated cross-face tail is not evidence of a cross-face
                        // dependency. Use actual contacts and the blocker geometry instead.
                        CrossFaceGeometry=puzzle.Surface is CubeTopology
                            ? puzzle.Surface.Generation().RegionOf(target.HeadCellId)!=puzzle.Surface.Generation().RegionOf(paths[pair.Key].HeadCellId)
                                || (proof.FaceMask&~(1<<puzzle.Surface.Generation().RegionOf(target.HeadCellId)))!=0 || (FaceMask(paths[pair.Key])&~proof.FaceMask)!=0
                            : CrossRegionContact(puzzle.Surface,target,paths[pair.Key],proof.Regions),Certainty=DependencyCertainty.ConfirmedMandatoryFixedRoute });
                }
                var full=simulator.Simulate(grid,target.Id,puzzle.Exits,puzzle.Rules);
                var node=new DependencyNode { PathId=target.Id,Regions=puzzle.Surface.Generation().Regions(target),FaceMask=FaceMask(target),HasValidExit=trajectory.HasValidExit,InitiallyRemovable=full.Succeeded,
                    PotentialBlockers=potential.ToArray(),ConfirmedBlockers=confirmed.ToArray(),Interferences=evidence.ToArray() };
                node.SubsetVerificationPassed=VerifySubsets(puzzle,target,confirmed,paths,exhaustiveLimit,token,out int checks,out bool exhaustive);
                node.SubsetChecks=checks; node.ExhaustiveSubsetsVerified=exhaustive;
                if(!node.HasValidExit || !node.SubsetVerificationPassed || full.Succeeded!=(confirmed.Count==0) || potential.Count!=confirmed.Count) valid=false;
                graph.lookup.Add(target.Id,node);
            }
            var nodes=new List<DependencyNode>(); foreach(var path in puzzle.Paths) nodes.Add(graph.lookup[path.Id]);
            graph.Nodes=nodes.ToArray(); graph.Edges=edges.ToArray();
            var queue=new Queue<int>(); var remaining=new Dictionary<int,int>(); int visited=0;
            foreach(var node in graph.Nodes)
            { node.PathsBlocked=outgoing[node.PathId].ToArray(); remaining.Add(node.PathId,node.ConfirmedBlockers.Length); if(node.ConfirmedBlockers.Length==0) { node.Depth=1; queue.Enqueue(node.PathId); } }
            while(queue.Count>0)
            {
                token.ThrowIfCancellationRequested(); int id=queue.Dequeue(); visited++;
                foreach(int parent in outgoing[id]) { graph.lookup[parent].Depth=Math.Max(graph.lookup[parent].Depth,graph.lookup[id].Depth+1); if(--remaining[parent]==0) queue.Enqueue(parent); }
            }
            graph.HasCycle=visited!=nodes.Count; graph.IsValid=valid && !graph.HasCycle && nodes.Count>0;
            foreach(var node in graph.Nodes)
            {
                token.ThrowIfCancellationRequested();
                node.IndependentBlockers=graph.HasCycle ? Array.Empty<int>() : graph.FindIndependent(node.ConfirmedBlockers,token,out bool unused);
                node.IndependentWidthExact=node.ConfirmedBlockers.Length<=12 && !graph.HasCycle;
            }
            graph.Measure(); return graph;
        }

        public static int FaceMask(ArrowPath path) { int mask=0; if(path.Surface is CubeTopology cube) foreach(int cell in path.CellIds) mask|=1<<(int)cube.GetCoordinate(cell).Face; return mask; }
        public static bool SpansRegions(ArrowPath a,ArrowPath b)
        { int region=a.Surface.Generation().RegionOf(a.HeadCellId); foreach(int c in a.CellIds) if(a.Surface.Generation().RegionOf(c)!=region) return true; foreach(int c in b.CellIds) if(b.Surface.Generation().RegionOf(c)!=region) return true; return false; }
        private static bool CrossRegionContact(ISurfaceTopology surface,ArrowPath target,ArrowPath blocker,int[] contacts)
        { int region=surface.Generation().RegionOf(target.HeadCellId); foreach(int r in contacts) if(r!=region) return true; foreach(int c in blocker.CellIds) if(surface.Generation().RegionOf(c)!=region) return true; return false; }
        public static bool VerifyIndependentBlockers(PuzzleDefinition puzzle,int targetId,IReadOnlyCollection<int> expected,CancellationToken token=default)
        {
            var graph=Analyze(puzzle,token); var node=graph.GetNode(targetId);
            return graph.IsValid && new HashSet<int>(node.ConfirmedBlockers).SetEquals(expected) && node.SubsetVerificationPassed && node.IndependentBlockers.Length==expected.Count;
        }
        private static bool VerifySubsets(PuzzleDefinition puzzle,ArrowPath target,List<int> ids,Dictionary<int,ArrowPath> paths,int limit,
            CancellationToken token,out int checks,out bool exhaustive)
        {
            var sim=new PathMovementSimulator(); checks=0; exhaustive=ids.Count<=limit;
            int count=exhaustive ? 1<<ids.Count : 2;
            for(int subset=0;subset<count;subset++)
            {
                token.ThrowIfCancellationRequested(); var present=new List<ArrowPath> { target };
                if(exhaustive) { for(int i=0;i<ids.Count;i++) if((subset&(1<<i))!=0) present.Add(paths[ids[i]]); }
                else if(subset==1) foreach(int id in ids) present.Add(paths[id]);
                bool succeeded=sim.Simulate(new SurfaceGrid(puzzle.Surface,present),target.Id,puzzle.Exits,puzzle.Rules).Succeeded; checks++;
                if(succeeded!=(present.Count==1)) return false;
            }
            // For large sets all singleton obstructions were separately proven above.
            // Zero/full checks plus singleton necessity prove the fixed-route result, without
            // claiming exponential subset enumeration or reachability of each artificial state.
            return true;
        }
        private bool Reaches(int start,int destination,CancellationToken token)
        {
            var seen=new HashSet<int>(); var stack=new Stack<int>(); stack.Push(start);
            while(stack.Count>0) { token.ThrowIfCancellationRequested(); int id=stack.Pop(); if(id==destination) return true; if(!seen.Add(id)) continue; foreach(int next in lookup[id].ConfirmedBlockers) stack.Push(next); }
            return false;
        }
        private int[] FindIndependent(int[] ids,CancellationToken token,out bool exact)
        {
            exact=ids.Length<=12; int n=ids.Length; var incompatible=new bool[n,n];
            for(int a=0;a<n;a++) for(int b=0;b<a;b++) incompatible[a,b]=incompatible[b,a]=Reaches(ids[a],ids[b],token)||Reaches(ids[b],ids[a],token);
            var best=new List<int>(); int count=exact ? 1<<n : 1;
            for(int mask=0;mask<count;mask++)
            {
                if((mask&255)==0) token.ThrowIfCancellationRequested(); var chosen=new List<int>(); bool valid=true;
                for(int a=0;a<n;a++) if(!exact || (mask&(1<<a))!=0)
                {
                    bool fits=true; foreach(int b in chosen) if(incompatible[a,b]) { fits=false; break; }
                    if(fits) chosen.Add(a); else if(exact) { valid=false; break; }
                }
                if(valid && chosen.Count>=best.Count)
                {
                    int cost=0,oldCost=0; foreach(int i in chosen) cost+=lookup[ids[i]].Depth; foreach(int i in best) oldCost+=lookup[ids[i]].Depth;
                    if(chosen.Count>best.Count || cost<oldCost) best=chosen;
                }
            }
            var result=new int[best.Count]; for(int i=0;i<result.Length;i++) result[i]=ids[best[i]]; return result;
        }
        private void Measure()
        {
            var m=new MultiBlockerMetrics(); int links=Edges.Length,blocked=0,multi=0,three=0,independent=0,branchers=0,cross=0,maxOut=0;
            foreach(var node in Nodes) m.MaximumBlockers=Math.Max(m.MaximumBlockers,node.ConfirmedBlockers.Length);
            m.BlockerHistogram=new int[m.MaximumBlockers+1];
            foreach(var node in Nodes)
            {
                int k=node.ConfirmedBlockers.Length; m.BlockerHistogram[k]++; if(k>0) blocked++; if(k>=2) multi++; if(k>=3) three++; if(node.IndependentBlockers.Length>=2) independent++;
                if(node.InitiallyRemovable) m.IndependentOpeningMoves++;
                if(node.IndependentBlockers.Length>=3) m.IndependentThreeBlockerFraction++;
                m.AverageIndependentBlockersPerBlockedPath+=node.IndependentBlockers.Length;
                if(!node.IndependentWidthExact) m.ConservativeIndependentWidths++;
                int degree=node.PathsBlocked.Length; if(degree>0) branchers++; maxOut=Math.Max(maxOut,degree);
                if(links>0) m.DependencyConcentration+=Math.Pow(degree/(double)links,2);
            }
            foreach(var edge in Edges) if(edge.CrossFaceGeometry) cross++;
            int total=Math.Max(1,Nodes.Length); m.AverageBlockersPerPath=links/(double)total; m.AverageBlockersPerBlockedPath=links/(double)Math.Max(1,blocked);
            m.AtLeastTwoFraction=multi/(double)total; m.AtLeastThreeFraction=three/(double)total; m.IndependentMultiBlockerFraction=independent/(double)total;
            m.IndependentThreeBlockerFraction/=total; m.AverageIndependentBlockersPerBlockedPath/=Math.Max(1,blocked);
            m.AverageBranching=links/(double)Math.Max(1,branchers); m.CrossFaceGeometryFraction=cross/(double)Math.Max(1,links); m.MaxBlockerConcentration=maxOut/(double)Math.Max(1,total-1);
            foreach(int bucket in m.BlockerHistogram) if(bucket>0) { double p=bucket/(double)total; m.DistributionEntropy-=p*Math.Log(p)/Math.Log(Math.Max(2,m.BlockerHistogram.Length)); }
            Metrics=m;
        }
    }
}
