using System;
using System.Collections.Generic;
using System.Threading;
using ArrowCube.Movement;
using ArrowCube.Topology;

namespace ArrowCube.Generation
{
    [Serializable]
    public sealed class DifficultyWeights
    {
        public double EdgeResistance=30, Dependencies=25, SafeMoveScarcity=15, SpatialUnpredictability=15, CrossFace=10, VisualComplexity=5;
        public DifficultyWeights Copy() => (DifficultyWeights)MemberwiseClone();
        public void Validate()
        {
            double sum=0;
            foreach(double value in new[] { EdgeResistance,Dependencies,SafeMoveScarcity,SpatialUnpredictability,CrossFace,VisualComplexity })
            { if(double.IsNaN(value) || double.IsInfinity(value) || value<0) throw new ArgumentOutOfRangeException(nameof(DifficultyWeights)); sum+=value; }
            if(sum<=0) throw new ArgumentException("At least one difficulty weight must be positive.");
        }
    }

    [Serializable]
    public sealed class DifficultyComponentScores
    {
        public double EdgeResistance, Dependencies, SafeMoveScarcity, SpatialUnpredictability, CrossFace, VisualComplexity;
    }

    [Serializable]
    public sealed class DependencyLink
    {
        public int Path, Blocker;
        public bool CrossFace;
    }

    [Serializable]
    public sealed class ChoiceSample
    {
        public int Remaining, Safe, Blocked, NewlyUnlocked;
    }

    [Serializable]
    public sealed class PuzzleDifficultyReport
    {
        public int AnalysisVersion=2;
        public int SurfaceRegionCount,OccupiedSurfaceRegions,GeometricFolds;
        public double[] RegionCoverage;
        public double RegionCoverageDeviation;
        public int Seed, GenerationVersion, PathCount, InitialSafe, InitialBlocked, DependencyLayers, LongestDependencyChain, CrossFacePaths;
        public double RequestedScore, ConfigurationEstimate, MeasuredScore, InitialSafeFraction, AverageDependencyDepth, AverageUnlockedPerRemoval, MaxUnlockedPerRemoval;
        public string ProfileName;
        public bool EnableCubeEdgeExits,EnableDirectionalGates;
        public double CrossFaceDependencyFraction, EdgeRemovabilityCorrelation, Coverage, AverageLength, AverageTurns, MeanLocalDensity, LocalDensityDeviation;
        public double MinSafeFraction, MeanSafeFraction, SafeSetChangeRate;
        public double EdgeFirstNormalizedMistakeRate;
        public bool AllSafeOrdersEquivalent=true;
        public string DependencyInterpretation="Exact mandatory blockers for fixed routes and static exits, verified with gameplay simulation. Safe removals commute; no safe order creates a dead end.";
        public int[] SafePaths, BlockedPaths, SafeByFace, BlockedByFace, LengthHistogram, TurnHistogram;
        public int[] DependencyFaceMatrix;
        public DependencyLink[] Dependencies;
        public ChoiceSample[] ChoiceSamples;
        public HeuristicSolverReport[] Solvers;
        public HeuristicSolverReport[] AlternativeRandomOrders;
        public double RandomSuccessRateMean,RandomFailuresPerRemovalMean;
        public DifficultyComponentScores Components;
        public DifficultyWeights Weights;
        public double GenerationMilliseconds;
        public int CandidatesEvaluated, RejectedCandidates;
        public bool TargetReached;
        public string Warning;
        public MultiBlockerMetrics MultiBlocker;
        public DependencyNode[] DependencyNodes;
        public DependencyGraphEdge[] ConfirmedDependencyEdges;
        public double RequestedAverageBlockers,RequestedIndependentMultiFraction;
        public bool MultiBlockerTargetsReached;
        public int TrajectoryCacheHits,TrajectoryCacheMisses;
        internal PuzzleDependencyGraph Graph { get; set; }
    }

    /// <summary>Independent structural analysis. Never changes gameplay, retries, or the input puzzle.</summary>
    public sealed class PuzzleDifficultyAnalyzer
    {
        public PuzzleDifficultyReport Analyze(PuzzleDefinition puzzle,int seed=0,DifficultyWeights weights=null,
            CancellationToken cancellationToken=default,int alternativeOrderSamples=1,int analysisVersion=3,RemovalTrajectoryCache trajectoryCache=null)
        {
            if(puzzle==null || puzzle.Paths.Count==0) throw new ArgumentException("Difficulty analysis needs a non-empty puzzle.",nameof(puzzle));
            if(alternativeOrderSamples<1 || alternativeOrderSamples>8) throw new ArgumentOutOfRangeException(nameof(alternativeOrderSamples));
            weights=weights??new DifficultyWeights(); weights.Validate();
            var graph=analysisVersion>=3 ? PuzzleDependencyGraph.Analyze(puzzle,cancellationToken,trajectoryCache) : null;
            if(graph!=null && !graph.IsValid) throw new ArgumentException("Dependency verification failed or contains a cycle.");
            var regions=puzzle.Surface.Generation(); int regionCount=regions.RegionCount;
            int count=puzzle.Paths.Count; var grid=puzzle.CreateGrid(); var sim=new PathMovementSimulator();
            var report=new PuzzleDifficultyReport { Seed=seed,PathCount=count,SafeByFace=new int[regionCount],BlockedByFace=new int[regionCount],DependencyFaceMatrix=new int[regionCount<=256 ? regionCount*regionCount : 0],Weights=weights.Copy(),
                ProfileName=puzzle.Name,EnableCubeEdgeExits=puzzle.Rules.EnableCubeEdgeExits,EnableDirectionalGates=puzzle.Rules.EnableDirectionalGates };
            report.AnalysisVersion=analysisVersion;
            if(graph!=null) { report.Graph=graph; report.MultiBlocker=graph.Metrics; report.DependencyNodes=graph.Nodes; report.ConfirmedDependencyEdges=graph.Edges; }
            var blockers=new Dictionary<int,HashSet<int>>(); var dependants=new Dictionary<int,List<int>>();
            var depths=new Dictionary<int,int>(); var safe=new List<int>(); var blocked=new List<int>(); var links=new List<DependencyLink>();
            var paths=new Dictionary<int,ArrowPath>(); int maxLength=0,maxTurns=0,occupied=0,turns=0;
            foreach(var path in puzzle.Paths) { paths.Add(path.Id,path); dependants.Add(path.Id,new List<int>()); maxLength=Math.Max(maxLength,path.CellIds.Count); }
            report.LengthHistogram=new int[maxLength+1]; var pathTurns=new Dictionary<int,int>();
            double sumDistance=0,sumDistanceSquared=0,sumSafeDistance=0;
            foreach(var path in puzzle.Paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var owners=new HashSet<int>();
                if(graph!=null) owners.UnionWith(graph.GetNode(path.Id).ConfirmedBlockers);
                else
                {
                    var alone=new SurfaceGrid(puzzle.Surface,new[] { path }); var route=sim.Simulate(alone,path.Id,puzzle.Exits,puzzle.Rules);
                    if(!route.Succeeded) throw new ArgumentException("A path has no unobstructed exit: "+path.Id);
                    for(int i=path.CellIds.Count;i<route.CellRail.Count;i++)
                        if(route.CellRail[i].IsOnSurface && grid.TryGetCellOwner(route.CellRail[i].CellId,out int owner) && owner!=path.Id) owners.Add(owner);
                }
                blockers.Add(path.Id,owners);
                foreach(int owner in owners)
                {
                    dependants[owner].Add(path.Id);
                    links.Add(new DependencyLink { Path=path.Id,Blocker=owner,CrossFace=regions.RegionOf(path.HeadCellId)!=regions.RegionOf(paths[owner].HeadCellId) });
                    if(report.DependencyFaceMatrix.Length>0) report.DependencyFaceMatrix[regions.RegionOf(path.HeadCellId)*regionCount+regions.RegionOf(paths[owner].HeadCellId)]++;
                }
                bool removable=graph!=null ? graph.GetNode(path.Id).InitiallyRemovable : sim.Simulate(grid,path.Id,puzzle.Exits,puzzle.Rules).Succeeded;
                if(removable!=(owners.Count==0)) throw new ArgumentException("Route blockers disagree with gameplay simulation.");
                if(removable) { safe.Add(path.Id); report.SafeByFace[(int)regions.RegionOf(path.HeadCellId)]++; }
                else { blocked.Add(path.Id); report.BlockedByFace[(int)regions.RegionOf(path.HeadCellId)]++; }
                double distance=regions.ExitDistance(path.HeadCellId);
                sumDistance+=distance; sumDistanceSquared+=distance*distance; if(removable) sumSafeDistance+=distance;
                int bends=PuzzleValidator.CountTurns(puzzle.Surface,path); pathTurns.Add(path.Id,bends); maxTurns=Math.Max(maxTurns,bends);
                turns+=bends; occupied+=path.CellIds.Count; report.LengthHistogram[path.CellIds.Count]++;
                if(path.FaceTransitionCount>0) report.CrossFacePaths++;
            }
            links.Sort((a,b)=>a.Path!=b.Path ? a.Path.CompareTo(b.Path) : a.Blocker.CompareTo(b.Blocker));
            var remaining=new Dictionary<int,int>(); var queue=new Queue<int>(safe);
            foreach(var path in puzzle.Paths) { remaining.Add(path.Id,blockers[path.Id].Count); if(blockers[path.Id].Count==0) depths.Add(path.Id,1); }
            int visited=0; double sumDepth=0;
            while(queue.Count>0)
            {
                int id=queue.Dequeue(),depth=depths[id]; visited++; sumDepth+=depth;
                report.LongestDependencyChain=Math.Max(report.LongestDependencyChain,depth);
                foreach(int parent in dependants[id])
                {
                    depths.TryGetValue(parent,out int old); depths[parent]=Math.Max(old,depth+1);
                    if(--remaining[parent]==0) queue.Enqueue(parent);
                }
            }
            if(visited!=count) throw new ArgumentException("Dependency graph contains an unsolvable cycle.");
            report.DependencyLayers=report.LongestDependencyChain; report.AverageDependencyDepth=sumDepth/count;
            report.SafePaths=safe.ToArray(); report.BlockedPaths=blocked.ToArray(); report.Dependencies=links.ToArray();
            report.InitialSafe=safe.Count; report.InitialBlocked=blocked.Count; report.InitialSafeFraction=safe.Count/(double)count;
            report.AverageLength=occupied/(double)count; report.AverageTurns=turns/(double)count; report.Coverage=occupied/(double)puzzle.Surface.CellCount;
            report.TurnHistogram=new int[maxTurns+1]; foreach(int bends in pathTurns.Values) report.TurnHistogram[bends]++;
            int crossLinks=0; foreach(var link in links) if(link.CrossFace) crossLinks++;
            report.CrossFaceDependencyFraction=crossLinks/(double)Math.Max(1,links.Count);
            double xMean=sumDistance/count,yMean=report.InitialSafeFraction;
            double variance=sumDistanceSquared/count-xMean*xMean;
            report.EdgeRemovabilityCorrelation=variance>1e-12 && yMean>0 && yMean<1
                ? (sumSafeDistance/count-xMean*yMean)/Math.Sqrt(variance*yMean*(1-yMean)) : 0;
            MeasureDensity(puzzle,grid,report,cancellationToken);
            report.SurfaceRegionCount=regionCount; report.RegionCoverage=new double[regionCount];
            var totals=new int[regionCount]; foreach(int cell in puzzle.Surface.EnumerateCellIds()) { int r=regions.RegionOf(cell); totals[r]++; if(grid.TryGetCellOwner(cell,out _)) report.RegionCoverage[r]++; }
            double mean=0; for(int r=0;r<regionCount;r++) { if(report.RegionCoverage[r]>0) report.OccupiedSurfaceRegions++; report.RegionCoverage[r]/=Math.Max(1,totals[r]); mean+=report.RegionCoverage[r]/regionCount; }
            foreach(double coverage in report.RegionCoverage) report.RegionCoverageDeviation+=(coverage-mean)*(coverage-mean)/regionCount;
            report.RegionCoverageDeviation=Math.Sqrt(report.RegionCoverageDeviation); foreach(var path in puzzle.Paths) report.GeometricFolds+=path.GeometricTransitionCount;
            report.Solvers=new HeuristicSolverReport[4]; var solver=new HeuristicPuzzleSolver();
            for(int i=0;i<4;i++) report.Solvers[i]=solver.Solve(puzzle,(HeuristicStrategy)i,seed,cancellationToken);
            report.AlternativeRandomOrders=new HeuristicSolverReport[alternativeOrderSamples];
            for(int i=0;i<alternativeOrderSamples;i++)
            {
                var result=i==0 ? report.Solvers[3] : solver.Solve(puzzle,HeuristicStrategy.Random,unchecked(seed+i*104729),cancellationToken);
                report.AlternativeRandomOrders[i]=result;
                report.RandomSuccessRateMean+=result.SuccessRate/alternativeOrderSamples;
                report.RandomFailuresPerRemovalMean+=result.FailuresPerRemoval/alternativeOrderSamples;
            }
            MeasureChoices(puzzle,report.Solvers[0].RemovedPaths,blockers,dependants,report);
            Score(report,weights);
            return report;
        }

        private static void MeasureDensity(PuzzleDefinition puzzle,SurfaceGrid grid,PuzzleDifficultyReport report,CancellationToken token)
        {
            double sum=0,squares=0;
            for(int i=0;i<puzzle.Surface.CellCount;i++)
            {
                if((i&255)==0) token.ThrowIfCancellationRequested();
                var cell=puzzle.Surface.GetCoordinate(i); int occupied=0;
                for(int d=0;d<4;d++) if(grid.TryGetCellOwner(puzzle.Surface.Step(cell,(ArrowCube.Topology.SurfaceDirection)d).CellId,out _)) occupied++;
                double density=occupied/4.0; sum+=density; squares+=density*density;
            }
            report.MeanLocalDensity=sum/puzzle.Surface.CellCount;
            report.LocalDensityDeviation=Math.Sqrt(Math.Max(0,squares/puzzle.Surface.CellCount-report.MeanLocalDensity*report.MeanLocalDensity));
        }

        private static void MeasureChoices(PuzzleDefinition puzzle,int[] order,Dictionary<int,HashSet<int>> blockers,
            Dictionary<int,List<int>> dependants,PuzzleDifficultyReport report)
        {
            var remaining=new Dictionary<int,int>(); foreach(var path in puzzle.Paths) remaining.Add(path.Id,blockers[path.Id].Count);
            int safe=report.InitialSafe,total=puzzle.Paths.Count,unlockedTotal=0,sampleStride=Math.Max(1,total/16); double sum=0;
            report.MinSafeFraction=1; var samples=new List<ChoiceSample>();
            for(int i=0;i<order.Length;i++)
            {
                double fraction=safe/(double)(total-i); sum+=fraction; report.MinSafeFraction=Math.Min(report.MinSafeFraction,fraction);
                int unlocked=0; foreach(int parent in dependants[order[i]]) if(--remaining[parent]==0) unlocked++;
                if(i%sampleStride==0 || i==order.Length-1) samples.Add(new ChoiceSample { Remaining=total-i,Safe=safe,Blocked=total-i-safe,NewlyUnlocked=unlocked });
                safe+=unlocked-1; unlockedTotal+=unlocked; report.MaxUnlockedPerRemoval=Math.Max(report.MaxUnlockedPerRemoval,unlocked);
            }
            report.ChoiceSamples=samples.ToArray(); report.AverageUnlockedPerRemoval=unlockedTotal/(double)Math.Max(1,order.Length);
            report.MeanSafeFraction=sum/Math.Max(1,order.Length); report.SafeSetChangeRate=report.AverageUnlockedPerRemoval;
        }

        private static double Clamp(double value) => Math.Max(0,Math.Min(1,value));
        private static void Score(PuzzleDifficultyReport r,DifficultyWeights w)
        {
            double failures=r.Solvers[0].FailuresPerRemoval;
            // Normalize by available path choices, not raw mistakes: repeating independent
            // regions or increasing the path count must not automatically raise the score.
            r.EdgeFirstNormalizedMistakeRate=2*failures/Math.Max(1,r.PathCount-1);
            double dependencyScale=Math.Max(1,Math.Log(r.PathCount+1,2));
            double entropy=0; foreach(int faceSafe in r.SafeByFace) if(faceSafe>0) { double p=faceSafe/(double)Math.Max(1,r.InitialSafe); entropy-=p*Math.Log(p)/Math.Log(Math.Max(2,r.SafeByFace.Length)); }
            var c=new DifficultyComponentScores {
                EdgeResistance=100*Clamp(2*r.EdgeFirstNormalizedMistakeRate),
                Dependencies=100*Clamp(.55*(r.AverageDependencyDepth-1)/dependencyScale+.25*(r.LongestDependencyChain-1)/(2*dependencyScale)+.20*r.Dependencies.Length/(double)Math.Max(1,r.PathCount*3)),
                SafeMoveScarcity=100*(1-r.InitialSafeFraction),
                SpatialUnpredictability=100*Clamp((1-Math.Abs(r.EdgeRemovabilityCorrelation))*entropy*(1-r.InitialSafeFraction)),
                CrossFace=100*Clamp(.5*r.CrossFacePaths/r.PathCount+.5*r.CrossFaceDependencyFraction),
                VisualComplexity=100*Clamp(.5*r.AverageTurns/Math.Max(1,r.AverageLength-2)+.5*r.Coverage)
            };
            r.Components=c;
            if(r.MultiBlocker!=null)
            {
                var m=r.MultiBlocker;
                // Conservative structural modifier: adding geometric contacts cannot
                // inflate the score by itself or compensate for an obvious blocker hub.
                double distributed=Clamp(.35+.30*Math.Min(1,m.IndependentMultiBlockerFraction*2)+.20*Math.Min(1,m.IndependentThreeBlockerFraction*3)+.15*m.DistributionEntropy);
                c.Dependencies*= (.60+.40*distributed)*(1-.50*m.MaxBlockerConcentration);
                c.CrossFace*=.50+.50*m.CrossFaceGeometryFraction;
            }
            double sum=w.EdgeResistance+w.Dependencies+w.SafeMoveScarcity+w.SpatialUnpredictability+w.CrossFace+w.VisualComplexity;
            r.MeasuredScore=Math.Round((w.EdgeResistance*c.EdgeResistance+w.Dependencies*c.Dependencies+w.SafeMoveScarcity*c.SafeMoveScarcity+
                w.SpatialUnpredictability*c.SpatialUnpredictability+w.CrossFace*c.CrossFace+w.VisualComplexity*c.VisualComplexity)/sum,6,MidpointRounding.AwayFromZero);
        }
    }
}
