using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ArrowCube.Movement;
using ArrowCube.Topology;

namespace ArrowCube.Generation
{
    /// <summary>Bounded deterministic dependency construction; every accepted candidate uses the gameplay simulator.</summary>
    public sealed class PuzzleGenerator
    {
        public const int LegacyCubeVersion=5;
        public const int CurrentVersion=6;
        private readonly PathMovementSimulator simulator=new PathMovementSimulator();
        private readonly PuzzleValidator validator=new PuzzleValidator();

        public GeneratedPuzzle Generate(PuzzleRecipe recipe,CancellationToken cancellationToken=default)
            => Generate(recipe,null,cancellationToken);

        public GeneratedPuzzle Generate(PuzzleRecipe recipe,IProgress<GenerationProgress> progress,CancellationToken cancellationToken=default)
        {
            GeneratedPuzzle result=null;
            foreach(var step in GenerateSteps(recipe,progress,cancellationToken)) if(step!=null) result=step;
            return result ?? throw new InvalidOperationException("Generation completed without a puzzle.");
        }

        /// <summary>Same deterministic generation, with cooperative work boundaries.
        /// Null yields let a single-threaded host render/input/cancel between chunks;
        /// the only non-null yield is the fully validated final puzzle.</summary>
        public IEnumerable<GeneratedPuzzle> GenerateSteps(PuzzleRecipe recipe,IProgress<GenerationProgress> progress=null,CancellationToken cancellationToken=default)
        {
            if(recipe==null) throw new ArgumentNullException(nameof(recipe));
            var steps=recipe.DifficultySettings==null ? ConstructSteps(recipe,0,progress,cancellationToken)
                : new DifficultyAwareGeneration().GenerateSteps(this,recipe,progress,cancellationToken);
            foreach(var step in steps) yield return step;
        }

        internal IEnumerable<GeneratedPuzzle> ConstructSteps(PuzzleRecipe recipe,int candidateIndex,IProgress<GenerationProgress> progress,CancellationToken cancellationToken)
        {
            if(recipe==null) throw new ArgumentNullException(nameof(recipe));
            var watch=Stopwatch.StartNew(); var report=new GenerationReport();
            string lastReason="No candidate met the requested bounds.";
            for(int restart=0;restart<recipe.MaxRestarts;restart++)
            {
                cancellationToken.ThrowIfCancellationRequested(); report.Restarts=restart;
                var random=new StableRandom(recipe.Seed,unchecked(restart+candidateIndex*recipe.MaxRestarts));
                var t=recipe.Surface;
                if(recipe.Shape!=null && (recipe.Profile.MaxPathLength>t.CellCount || (long)recipe.Profile.MinPathCount*recipe.Profile.MinPathLength>t.CellCount))
                    throw new PuzzleGenerationException(recipe,report,"Path count or length bounds exceed the surface capacity.");
                if(recipe.Shape!=null && recipe.Profile.MinCrossRegionPaths>0 && t.Generation().RegionCount<2)
                    throw new PuzzleGenerationException(recipe,report,"Cross-region paths were requested on a single-region surface.");
                if(recipe.Rules.EnableCubeEdgeExits && !recipe.Rules.EnableDirectionalGates && !t.Generation().HasEdgeExits)
                    throw new PuzzleGenerationException(recipe,report,"This surface has no eligible edge exits; enable directional gates.");
                var grid=new SurfaceGrid(t,Array.Empty<ArrowPath>());
                var paths=new List<ArrowPath>(); var gates=new List<CellGate>();
                var gateLookup=new HashSet<CellGate>(); var exits=new ExitMap(t,gates);
                var free=new FreeCells(t.CellCount); var faces=new FaceComponents(t.Generation().RegionCount);
                var proposalContext=new ProposalContext();
                bool dependencyOrder=recipe.Shape!=null || recipe.DependencyControlSettings!=null && (restart+(recipe.Profile.GridSize>8 ? 1 : 0))%2==1;
                int crossPaths=0, stalls=0, limit=t.CellCount*recipe.AttemptsPerCell+2000;
                if(recipe.DifficultySettings!=null) limit=Math.Min(limit,recipe.DifficultySettings.MaxConstructionAttempts);
                // Version 1 is retained for saved recipes. Larger surfaces need an area-based
                // rejection budget: a linear budget stops packing long before the coverage floor.
                int stallLimit=800+t.Generation().Scale*50;
                if(recipe.GenerationVersion>=2) stallLimit=Math.Max(stallLimit,t.CellCount*2);
                int attempt=0;
                while(attempt<limit && free.Count>=recipe.Profile.MinPathLength && paths.Count<recipe.Profile.MaxPathCount)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return null;
                    if(progress!=null && (attempt&63)==0) progress.Report(new GenerationProgress { Completed=attempt,Total=limit,Phase=$"Packing surface ({paths.Count} arrows)" });
                    if((attempt&63)==0) cancellationToken.ThrowIfCancellationRequested();
                    bool forceCross=crossPaths<recipe.Profile.MinCrossFacePaths || recipe.Shape==null && faces.Count>1;
                    ArrowPath path; CellGate? proposedGate; bool found;
                    if(dependencyOrder)
                        found=SelectDependencyCandidate(t,grid,free,faces,forceCross,gateLookup,gates,exits,recipe,random,paths,
                            proposalContext,ref attempt,limit,report,cancellationToken,out path,out proposedGate);
                    else if(recipe.DependencyControlSettings!=null)
                        found=SelectControlledCandidate(t,grid,free,faces,forceCross,gateLookup,gates,exits,recipe,random,paths,
                            ref attempt,limit,report,cancellationToken,out path,out proposedGate);
                    else if(recipe.DifficultySettings==null)
                    {
                        attempt++; report.CandidateAttempts++;
                        found=TryCandidate(t,grid,free,faces,forceCross,gateLookup,gates.Count,recipe,random,paths.Count,out path,out proposedGate);
                        if(!found) report.RejectedCandidates++;
                    }
                    else found=SelectCandidate(t,grid,free,faces,forceCross,gateLookup,gates,exits,recipe,random,paths,
                        proposalContext,ref attempt,limit,report,cancellationToken,out path,out proposedGate);
                    if(!found) { if(++stalls>stallLimit) break; continue; }
                    var proposedExits=exits;
                    if(proposedGate.HasValue)
                    {
                        var proposed=new List<CellGate>(gates) { proposedGate.Value };
                        proposedExits=new ExitMap(t,proposed);
                    }
                    grid.AddPath(path);
                    var plan=simulator.Simulate(grid,path.Id,proposedExits,recipe.Rules);
                    var solo=dependencyOrder ? simulator.Simulate(new SurfaceGrid(t,new[] { path }),path.Id,proposedExits,recipe.Rules) : plan;
                    if((!dependencyOrder && !plan.Succeeded) || (recipe.GenerationVersion>=5 && !InitialExitClearance.IsClear(solo)))
                    {
                        grid.RemovePath(path.Id); report.RejectedCandidates++;
                        if(++stalls>stallLimit) break;
                        continue;
                    }
                    stalls=0; paths.Add(path);
                    if(proposedGate.HasValue) { gates.Add(proposedGate.Value); gateLookup.Add(proposedGate.Value); exits=proposedExits; }
                    foreach(var cell in path.CellIds) free.Remove(t.GetCellIndex(cell));
                    foreach(var segment in path.CellSegments) faces.Join(t.Generation().RegionOf(segment.A),t.Generation().RegionOf(segment.B));
                    if(path.FaceTransitionCount>0) crossPaths++;
                    if(grid.OccupiedCellCount/(double)t.CellCount>=recipe.Profile.TargetCoverage && !forceCross)
                    {
                        if(recipe.DependencyControlSettings==null) break;
                        int safe=0; foreach(var old in paths) if(simulator.Simulate(grid,old.Id,exits,recipe.Rules).Succeeded) safe++;
                        if(safe<=recipe.DependencyControlSettings.MaximumStartingFreeArrows) break;
                    }
                }
                foreach(var step in FillTailsSteps(t,grid,free,paths,exits,recipe,random,cancellationToken,dependencyOrder)) yield return step;
                progress?.Report(new GenerationProgress { Phase="Verifying puzzle",Completed=restart,Total=recipe.MaxRestarts });
                yield return null;
                var definition=new PuzzleDefinition($"{recipe.Profile.Name} · seed {recipe.Seed}",t,paths,exits,recipe.Rules);
                var solution=new List<int>(paths.Count);
                for(int i=paths.Count-1;i>=0;i--) solution.Add(paths[i].Id);
                var validation=validator.Validate(definition,dependencyOrder ? null : solution,cancellationToken,recipe.GenerationVersion>=5);
                if(!validation.Succeeded)
                { report.ValidationFailures++; lastReason=validation.Error; continue; }
                lastReason=QualityError(recipe,validation.Statistics,false);
                if(lastReason.Length!=0) { report.QualityRejections++; continue; }
                var accepted=new GeneratedPuzzle(recipe,definition,validation,report);
                if(recipe.DependencyControlSettings!=null)
                {
                    progress?.Report(new GenerationProgress { Phase="Tuning blockers",Completed=restart,Total=recipe.MaxRestarts });
                    foreach(var step in new DependencyControlTuner().TuneSteps(accepted,random,cancellationToken))
                        if(step==null) yield return null; else accepted=step;
                    lastReason=QualityError(recipe,accepted.Statistics);
                    if(lastReason.Length!=0) { report.QualityRejections++; continue; }
                }
                if(recipe.Shape!=null)
                {
                    accepted.DependencyGraph=PuzzleDependencyGraph.Analyze(accepted,cancellationToken);
                    if(!accepted.DependencyGraph.IsValid) { report.ValidationFailures++; lastReason="Dependency verification failed."; continue; }
                }
                report.Milliseconds=watch.Elapsed.TotalMilliseconds;
                report.TargetCoverageReached=accepted.Statistics.Coverage>=recipe.Profile.TargetCoverage;
                yield return accepted;
                yield break;
            }
            report.Milliseconds=watch.Elapsed.TotalMilliseconds;
            throw new PuzzleGenerationException(recipe,report,lastReason);
        }

        public static string QualityError(PuzzleRecipe recipe,PuzzleStatistics stats,bool checkDependencyControls=true)
        {
            var p=recipe.Profile;
            if(stats.Coverage<p.MinimumCoverage) return $"Coverage {stats.Coverage:P1} is below {p.MinimumCoverage:P0}.";
            if(stats.PathCount<p.MinPathCount) return "Too few paths.";
            if(stats.PathCount>p.MaxPathCount) return "Too many paths.";
            if(stats.CrossFacePaths<p.MinCrossFacePaths || recipe.Shape==null && stats.ConnectedFaces!=6) return "Insufficient cross-region paths or cube face connectivity.";
            if(stats.AverageTurns<p.MinimumAverageTurns) return "Too few bends.";
            if(stats.BlockedFraction<p.MinimumBlockedFraction) return $"Too few dependencies ({stats.BlockedFraction:P1} initially blocked).";
            if(stats.ImmediatelyRemovablePaths<1) return "No safe opening move.";
            if(checkDependencyControls && recipe.DependencyControlSettings!=null && stats.ImmediatelyRemovablePaths>recipe.DependencyControlSettings.MaximumStartingFreeArrows)
                return $"Starting free arrows {stats.ImmediatelyRemovablePaths} exceeds the configured maximum {recipe.DependencyControlSettings.MaximumStartingFreeArrows}. Increase generation restarts or relax the opening limit.";
            if(recipe.Rules.EnableCubeEdgeExits && recipe.Surface.Generation().HasEdgeExits && stats.EdgeExitsInSolution<1) return "No verified edge exit.";
            if(recipe.Rules.EnableDirectionalGates && stats.GateExitsInSolution<1) return "No verified gate exit.";
            return string.Empty;
        }

        private IEnumerable<GeneratedPuzzle> FillTailsSteps(ISurfaceTopology t,SurfaceGrid grid,FreeCells free,List<ArrowPath> paths,
            ExitMap exits,PuzzleRecipe recipe,StableRandom random,CancellationToken token,bool preserveAllRoutes=false)
        {
            // Extending an older tail must never block a newer path's future leading route.
            // Descending construction order lets us protect precisely those routes, while
            // allowing extensions to create dependencies for still older paths.
            var protectedCells=new HashSet<int>();
            // Dependency construction can use either insertion order. Tail fill must
            // preserve all existing dependencies rather than only newer exit routes.
            if(preserveAllRoutes) foreach(var path in paths)
            {
                token.ThrowIfCancellationRequested();
                yield return null;
                var route=simulator.Simulate(new SurfaceGrid(t,new[] { path }),path.Id,exits,recipe.Rules);
                for(int i=path.CellIds.Count;i<route.CellRail.Count;i++) if(route.CellRail[i].IsOnSurface) protectedCells.Add(route.CellRail[i].CellId);
            }
            for(int index=paths.Count-1;index>=0;index--)
            {
                token.ThrowIfCancellationRequested();
                yield return null;
                var original=paths[index];
                var alone=new SurfaceGrid(t,new[] { original });
                var plan=simulator.Simulate(alone,original.Id,exits,recipe.Rules);
                if(!plan.Succeeded) continue;
                for(int i=original.CellIds.Count;i<plan.CellRail.Count;i++)
                    if(plan.CellRail[i].IsOnSurface) protectedCells.Add(plan.CellRail[i].CellId);
                if(grid.OccupiedCellCount/(double)t.CellCount>=recipe.Profile.TargetCoverage) continue;
                var cells=new List<int>(original.CellIds);
                var options=new int[4];
                while(cells.Count<recipe.Profile.MaxPathLength)
                {
                    var tail=cells[cells.Count-1]; int count=0;
                    for(int d=0;d<4;d++)
                    {
                        var neighbor=t.Step(tail,(SurfaceDirection)d).CellId;
                        if(!protectedCells.Contains(neighbor) && !grid.TryGetCellOwner(neighbor,out _) && !cells.Contains(neighbor))
                            options[count++]=neighbor;
                    }
                    if(count==0) break;
                    var next=options[random.Next(count)]; cells.Add(next);
                    // Mark immediately so another branch cannot reuse a just-added extension.
                    free.Remove(t.GetCellIndex(next));
                }
                if(cells.Count==original.CellIds.Count) continue;
                var replacement=GenerationPaths.Create(original.Id,t,cells,original.HeadDirection);
                grid.RemovePath(original.Id); grid.AddPath(replacement); paths[index]=replacement;
            }
        }

        private bool TryCandidate(ISurfaceTopology t,SurfaceGrid grid,FreeCells free,FaceComponents faces,bool forceCross,
            HashSet<CellGate> gates,int gateCount,PuzzleRecipe recipe,StableRandom random,int id,
            out ArrowPath path,out CellGate? proposedGate,int? requiredCell=null,bool allowNewGate=true,
            bool allowBlockedExit=false,SurfaceDirection? requiredHeading=null)
        {
            path=null; proposedGate=null;
            int head; SurfaceDirection heading;
            if(requiredCell.HasValue) { head=requiredCell.Value; heading=requiredHeading ?? (SurfaceDirection)random.Next(4); }
            else if(forceCross)
            {
                if(t is CubeTopology cube) LegacyCrossPlacement(cube,faces,random,out head,out heading);
                else
                {
                    var folds=t.Generation().Folds;
                    if(folds.Count==0) return false;
                    int first=random.Next(folds.Count),chosen=first;
                    for(int i=0;i<folds.Count;i++)
                    {
                        int index=(first+i)%folds.Count; var fold=folds[index];
                        if(grid.TryGetCellOwner(fold.Cell,out _)) continue;
                        if(faces.Count>1 && faces.Same(t.Generation().RegionOf(fold.Cell),t.Generation().RegionOf(t.Step(fold.Cell,fold.Direction).CellId))) continue;
                        chosen=index; break;
                    }
                    head=folds[chosen].Cell; heading=folds[chosen].Direction.Opposite();
                }
                if(grid.TryGetCellOwner(head,out _)) return false;
            }
            else
            {
                head=t.GetCoordinate(free.At(random.Next(free.Count))); heading=(SurfaceDirection)random.Next(4);
                if(recipe.DifficultySettings!=null) SelectPlacement(t,free,recipe.DifficultySettings,random,ref head,ref heading);
                else if(t is CubeTopology && recipe.GenerationVersion>=2 && t.Generation().Scale>20 && recipe.Rules.EnableCubeEdgeExits && !recipe.Rules.EnableDirectionalGates)
                {
                    // Build inner paths first so outer paths can later block them. Uniform
                    // sampling packs the perimeter first and cuts off the interior of large faces.
                    if(random.Chance(.8))
                        for(int sample=0;sample<12;sample++)
                        {
                            var candidate=t.GetCoordinate(free.At(random.Next(free.Count)));
                            if(t.Generation().ExitDistance(candidate)>t.Generation().ExitDistance(head)) head=candidate;
                        }
                    int distance=t.Generation().ExitDistance(head);
                    int start=random.Next(4);
                    for(int d=0;d<4;d++)
                    {
                        var outward=(SurfaceDirection)((start+d)%4);
                        if(DistanceToEdge(((CubeTopology)t).GetCoordinate(head),outward,t.Generation().Scale)==distance) { heading=outward; break; }
                    }
                }
            }
            var neck=t.Step(head,heading.Opposite());
            if(neck.CellId==head || grid.TryGetCellOwner(head,out _) || grid.TryGetCellOwner(neck.CellId,out _)) return false;
            bool mayAddGate=allowNewGate && recipe.Rules.EnableDirectionalGates && gateCount<recipe.Profile.MaxGates;
            bool preferGate=mayAddGate && (!recipe.Rules.EnableCubeEdgeExits || random.Chance(.35));
            int gateDistance=1+random.Next(Math.Max(1,t.Generation().Scale/2));
            var corridor=new HashSet<int> { head };
            var directed=new HashSet<CellGate>();
            var current=head; var direction=heading;
            bool foundExit=false;
            for(int distance=0;distance<=t.Generation().RouteLimit;distance++)
            {
                var next=t.Step(current,direction);
                bool edge=recipe.Rules.EnableCubeEdgeExits && next.AllowsEdgeExit;
                if(edge || recipe.Rules.EnableDirectionalGates && gates.Contains(new CellGate(current,direction)))
                { foundExit=true; break; }
                if(preferGate && distance>=gateDistance)
                { proposedGate=new CellGate(current,direction); foundExit=true; break; }
                if(!directed.Add(new CellGate(current,direction)) || !allowBlockedExit && grid.TryGetCellOwner(next.CellId,out _))
                {
                    if(mayAddGate && distance>=1)
                    { proposedGate=new CellGate(current,direction); foundExit=true; }
                    break;
                }
                corridor.Add(next.CellId); current=next.CellId; direction=next.Direction;
            }
            if(!foundExit || corridor.Contains(neck.CellId)) return false;
            var cells=new List<int> { head,neck.CellId };
            var visited=new HashSet<int> { head,neck.CellId };
            current=neck.CellId; direction=neck.Direction;
            int length=recipe.Profile.MinPathLength+random.Next(recipe.Profile.MaxPathLength-recipe.Profile.MinPathLength+1);
            var neighbors=new SurfaceCellStep[4]; var weights=new double[4];
            while(cells.Count<length)
            {
                int count=0; double total=0;
                for(int d=0;d<4;d++)
                {
                    var choice=(SurfaceDirection)d;
                    var step=t.Step(current,choice);
                    if(visited.Contains(step.CellId) || corridor.Contains(step.CellId)
                        || grid.TryGetCellOwner(step.CellId,out _)) continue;
                    double weight=choice==direction ? 1-recipe.Profile.TurnProbability : recipe.Profile.TurnProbability*.5;
                    // Favor spare neighbors rather than walking into an avoidable one-cell pocket.
                    int degree=0;
                    for(int n=0;n<4;n++)
                    {
                        var neighbor=t.Step(step.CellId,(SurfaceDirection)n).CellId;
                        if(!visited.Contains(neighbor) && !corridor.Contains(neighbor) && !grid.TryGetCellOwner(neighbor,out _)) degree++;
                    }
                    weight*=1+degree*.25;
                    neighbors[count]=step; weights[count]=weight; total+=weight; count++;
                }
                if(count==0) break;
                double pick=random.NextUInt()/(double)4294967296UL*total;
                int selected=count-1;
                for(int i=0;i<count;i++) { pick-=weights[i]; if(pick<0) { selected=i; break; } }
                var chosen=neighbors[selected]; current=chosen.CellId; direction=chosen.Direction;
                visited.Add(current); cells.Add(current);
            }
            if(cells.Count<recipe.Profile.MinPathLength) return false;
            path=GenerationPaths.Create(id,t,cells,heading);
            return true;
        }



        private static void SelectPlacement(ISurfaceTopology t,FreeCells free,DifficultySettings settings,StableRandom random,
            ref int head,ref SurfaceDirection heading)
        {
            var strategy=settings.Placement;
            if(strategy==PlacementStrategy.Mixed || strategy==PlacementStrategy.DifficultyOptimized)
                strategy=(PlacementStrategy)random.Next(3);
            if(strategy==PlacementStrategy.UniformRandom) return;
            for(int i=0;i<6;i++)
            {
                var next=t.GetCoordinate(free.At(random.Next(free.Count)));
                int difference=t.Generation().ExitDistance(next)-t.Generation().ExitDistance(head);
                if(strategy==PlacementStrategy.InteriorBiased ? difference>0 : difference<0) head=next;
            }
            // Headings remain varied. Placement no longer forces nearest-edge exits.
        }

        private bool SelectCandidate(ISurfaceTopology t,SurfaceGrid grid,FreeCells free,FaceComponents faces,bool forceCross,
            HashSet<CellGate> gateLookup,List<CellGate> gates,ExitMap exits,PuzzleRecipe recipe,StableRandom random,List<ArrowPath> paths,
            ProposalContext context,ref int attempts,int limit,GenerationReport report,CancellationToken token,out ArrowPath selected,out CellGate? selectedGate)
        {
            selected=null; selectedGate=null; double best=double.NegativeInfinity;
            var settings=recipe.DifficultySettings;
            bool multi=recipe.GenerationVersion>=4 && settings.MultiBlocker.Enabled;
            var routes=context.Routes; var oldSafe=context.OldSafe;
            // Cache unobstructed routes for this fixed grid and exit map, shared by the proposal pool.
            if(context.PathCount!=paths.Count)
            {
                context.PathCount=paths.Count; routes.Clear(); oldSafe.Clear(); context.Owners.Clear();
                foreach(var old in paths)
                {
                    var rail=recipe.GenerationVersion>=4 ? context.Cache.Get(t,old,exits,recipe.Rules,token).CellRail
                        : simulator.Simulate(new SurfaceGrid(t,new[] { old }),old.Id,exits,recipe.Rules).CellRail;
                    var cells=new HashSet<int>();
                    for(int i=old.CellIds.Count;i<rail.Count;i++) if(rail[i].IsOnSurface) cells.Add(rail[i].CellId);
                    routes.Add(old.Id,cells);
                    var owners=new HashSet<int>(); foreach(var cell in cells) if(grid.TryGetCellOwner(cell,out int owner) && owner!=old.Id) owners.Add(owner);
                    context.Owners.Add(old.Id,owners);
                    if(simulator.Simulate(grid,old.Id,exits,recipe.Rules).Succeeded) oldSafe.Add(old.Id);
                }
            }
            int proposals=settings.CandidatePoolSize;
            for(int trial=0;trial<proposals && attempts<limit;trial++)
            {
                token.ThrowIfCancellationRequested(); attempts++; report.CandidateAttempts++;
                bool cross=forceCross || random.Chance(settings.CrossFaceFrequency);
                int? required=null;
                if(multi && !forceCross && settings.MultiBlocker.EnableTargetedPlacement && paths.Count>=8 && random.Chance(.60))
                    required=ChooseTargetCell(t,grid,paths,context,settings.MultiBlocker,random);
                if(!TryCandidate(t,grid,free,faces,cross && !required.HasValue,gateLookup,gates.Count,recipe,random,paths.Count,out var path,out var gate,required))
                { report.RejectedCandidates++; continue; }
                var proposed=gate.HasValue ? new ExitMap(t,new List<CellGate>(gates) { gate.Value }) : exits;
                grid.AddPath(path);
                var candidatePlan=simulator.Simulate(grid,path.Id,proposed,recipe.Rules);
                if(!candidatePlan.Succeeded)
                { grid.RemovePath(path.Id); report.RejectedCandidates++; continue; }
                int hits=0,newlyBlocked=0,crossLinks=0,inversions=0,unblocked=0,lostLinks=0;
                double multiGain=0; var affected=multi ? new HashSet<int>() : null;
                foreach(var old in paths)
                {
                    var routeCells=routes[old.Id];
                    bool gateOnRoute=gate.HasValue && routeCells.Contains(gate.Value.CellId);
                    if(gateOnRoute)
                    {
                        var route=simulator.Simulate(new SurfaceGrid(t,new[] { old }),old.Id,proposed,recipe.Rules);
                        routeCells=new HashSet<int>();
                        for(int i=old.CellIds.Count;i<route.CellRail.Count;i++) if(route.CellRail[i].IsOnSurface) routeCells.Add(route.CellRail[i].CellId);
                        if(multi)
                        {
                            var retainedOwners=new HashSet<int>();
                            foreach(var cell in routeCells) if(grid.TryGetCellOwner(cell,out int owner) && owner!=old.Id && owner!=path.Id) retainedOwners.Add(owner);
                            lostLinks+=Math.Max(0,context.Owners[old.Id].Count-retainedOwners.Count);
                        }
                    }
                    bool hit=false; foreach(var cell in path.CellIds) if(routeCells.Contains(cell)) { hit=true; break; }
                    if(hit)
                    {
                        hits++; if(oldSafe.Contains(old.Id)) newlyBlocked++;
                        if(t.Generation().RegionOf(old.HeadCellId)!=t.Generation().RegionOf(path.HeadCellId)) crossLinks++;
                        if(t.Generation().ExitDistance(old.HeadCellId)<t.Generation().ExitDistance(path.HeadCellId)) inversions++;
                        if(multi)
                        {
                            affected.Add(old.Id); int before=context.Owners[old.Id].Count;
                            multiGain+=before>=settings.MultiBlocker.MaxTargetBlockers ? -.6 : before==0 ? .8 : 1.5/(1+.25*Math.Max(0,before-1));
                            bool multiple=PuzzleDependencyGraph.SpansRegions(old,path);
                            if(multiple) multiGain+=settings.MultiBlocker.CrossFacePreference*.5;
                        }
                    }
                    if(gateOnRoute && !oldSafe.Contains(old.Id) && simulator.Simulate(grid,old.Id,proposed,recipe.Rules).Succeeded) unblocked++;
                }
                grid.RemovePath(path.Id);
                double safeFraction=(oldSafe.Count-newlyBlocked+unblocked+1)/(double)(paths.Count+1);
                double strength=settings.DependencyStrength;
                int leadingCells=0;
                for(int i=path.CellIds.Count;i<candidatePlan.CellRail.Count;i++) if(candidatePlan.CellRail[i].IsOnSurface) leadingCells++;
                double value=-(Math.Abs(safeFraction-settings.DesiredSafeFraction)*4)
                    +(strength*2-1)*(newlyBlocked+hits*.35)
                    +strength*(crossLinks*.6+inversions*settings.SpatialUnpredictability)
                    +settings.EdgeResistance*inversions*.8
                    +(strength*2-1)*3*leadingCells/t.Generation().Scale;
                if(multi)
                {
                    // A new blocker of a sibling blocker merely extends a chain. Prefer
                    // separate branches, then validate true independence on the finished DAG.
                    foreach(int target in affected) foreach(int sibling in context.Owners[target]) if(affected.Contains(sibling)) multiGain-=1.4;
                    double hub=Math.Max(0,hits/(double)Math.Max(1,paths.Count)-settings.MultiBlocker.MaxBlockerConcentration);
                    double safeSurplus=Math.Max(0,Math.Min(1,(oldSafe.Count-settings.DesiredSafeFraction*paths.Count)/Math.Max(1,paths.Count*.15)));
                    value+=2.5*strength*multiGain-8*hub*settings.MultiBlocker.BranchingPreference+6*safeSurplus*newlyBlocked
                        -strength*(4*lostLinks+6*unblocked)+settings.EdgeResistance*inversions*.7;
                }
                value=Math.Round(value,6,MidpointRounding.AwayFromZero);
                if(value>best) { best=value; selected=path; selectedGate=gate; }
            }
            return selected!=null;
        }

        private sealed class ProposalContext
        {
            public int PathCount=-1;
            public readonly Dictionary<int,HashSet<int>> Routes=new Dictionary<int,HashSet<int>>();
            public readonly HashSet<int> OldSafe=new HashSet<int>();
            public readonly Dictionary<int,HashSet<int>> Owners=new Dictionary<int,HashSet<int>>();
            public readonly RemovalTrajectoryCache Cache=new RemovalTrajectoryCache();
        }

        // Dependency construction allows blocked additions and rejects cycles before committing.
        // Unlike reverse-only construction, growth does not need another free opening move.
        private bool SelectDependencyCandidate(ISurfaceTopology t,SurfaceGrid grid,FreeCells free,FaceComponents faces,bool forceCross,
            HashSet<CellGate> gateLookup,List<CellGate> gates,ExitMap exits,PuzzleRecipe recipe,StableRandom random,List<ArrowPath> paths,
            ProposalContext context,ref int attempts,int limit,GenerationReport report,CancellationToken token,out ArrowPath selected,out CellGate? selectedGate)
        {
            selected=null; selectedGate=null; var controls=recipe.DependencyControlSettings ?? new DependencyControlSettings { MaximumStartingFreeArrows=int.MaxValue };
            if(context.PathCount!=paths.Count)
            {
                context.PathCount=paths.Count; context.Routes.Clear(); context.Owners.Clear();
                foreach(var old in paths)
                {
                    token.ThrowIfCancellationRequested();
                    var route=simulator.Simulate(new SurfaceGrid(t,new[] { old }),old.Id,exits,recipe.Rules);
                    var cells=new HashSet<int>(); var owners=new HashSet<int>();
                    for(int i=old.CellIds.Count;i<route.CellRail.Count;i++) if(route.CellRail[i].IsOnSurface)
                    {
                        var cell=route.CellRail[i].CellId; cells.Add(cell);
                        if(grid.TryGetCellOwner(cell,out int owner) && owner!=old.Id) owners.Add(owner);
                    }
                    context.Routes.Add(old.Id,cells); context.Owners.Add(old.Id,owners);
                }
            }
            double best=double.NegativeInfinity;
            int proposals=recipe.DifficultySettings==null ? 24 : Math.Min(24,recipe.DifficultySettings.CandidatePoolSize);
            for(int trial=0;trial<proposals && attempts<limit;trial++)
            {
                token.ThrowIfCancellationRequested(); attempts++; report.CandidateAttempts++;
                int? required=null; SurfaceDirection? heading=null;
                if(paths.Count>0 && random.Chance(.75))
                {
                    var blocker=paths[random.Next(paths.Count)]; var contact=blocker.CellIds[random.Next(blocker.CellIds.Count)];
                    var outward=(SurfaceDirection)random.Next(4);
                    if(t is CubeTopology cube && forceCross)
                    {
                        var coordinate=cube.GetCoordinate(contact); int start=(int)outward;
                        for(int d=0;d<4;d++)
                        {
                            var edge=(SurfaceDirection)((start+d)%4); var transition=cube.GetEdgeTransition(coordinate.Face,edge);
                            if(faces.Count>1 && faces.Same((int)coordinate.Face,(int)transition.DestinationFace)) continue;
                            outward=edge; break;
                        }
                        int end=cube.GridSize-1;
                        required=cube.GetCellIndex(new SurfaceCoordinate(coordinate.Face,
                            outward==SurfaceDirection.Left ? 0 : outward==SurfaceDirection.Right ? end : coordinate.X,
                            outward==SurfaceDirection.Down ? 0 : outward==SurfaceDirection.Up ? end : coordinate.Y));
                        heading=outward.Opposite();
                    }
                    else if(!forceCross)
                    {
                        var head=contact; int distance=1+random.Next(Math.Max(1,t.Generation().Scale-1));
                        for(int step=0;step<distance;step++)
                        {
                            var next=t.Step(head,outward);
                            if(t is CubeTopology && next.IsGeometricEdge) break;
                            head=next.CellId; if(!(t is CubeTopology)) outward=next.Direction;
                        }
                        required=head; heading=outward.Opposite();
                    }
                }
                if(!TryCandidate(t,grid,free,faces,forceCross,gateLookup,gates.Count,recipe,random,paths.Count,out var candidate,out var gate,
                    requiredCell:required,allowBlockedExit:true,requiredHeading:heading)) { report.RejectedCandidates++; continue; }
                var map=gate.HasValue ? new ExitMap(t,new List<CellGate>(gates) { gate.Value }) : exits;
                var solo=simulator.Simulate(new SurfaceGrid(t,new[] { candidate }),candidate.Id,map,recipe.Rules);
                if(!InitialExitClearance.IsClear(solo)) { report.RejectedCandidates++; continue; }
                grid.AddPath(candidate); int safe=0;
                var affected=new HashSet<int>(); var effectiveOwners=new Dictionary<int,HashSet<int>>();
                foreach(var old in paths)
                {
                    var route=context.Routes[old.Id]; var owners=context.Owners[old.Id];
                    // A new gate can shorten an older route and remove existing dependencies.
                    if(gate.HasValue && route.Contains(gate.Value.CellId))
                    {
                        var changed=simulator.Simulate(new SurfaceGrid(t,new[] { old }),old.Id,map,recipe.Rules);
                        route=new HashSet<int>(); owners=new HashSet<int>();
                        for(int i=old.CellIds.Count;i<changed.CellRail.Count;i++) if(changed.CellRail[i].IsOnSurface)
                        {
                            var cell=changed.CellRail[i].CellId; route.Add(cell);
                            if(grid.TryGetCellOwner(cell,out int id) && id!=old.Id && id!=candidate.Id) owners.Add(id);
                        }
                    }
                    bool hit=false; foreach(var cell in candidate.CellIds) if(route.Contains(cell)) { hit=true; break; }
                    if(hit) affected.Add(old.Id);
                    if(!hit && owners.Count==0) safe++;
                    effectiveOwners.Add(old.Id,owners);
                }
                var blockers=new HashSet<int>();
                for(int i=candidate.CellIds.Count;i<solo.CellRail.Count;i++)
                    if(solo.CellRail[i].IsOnSurface && grid.TryGetCellOwner(solo.CellRail[i].CellId,out int owner) && owner!=candidate.Id) blockers.Add(owner);
                if(blockers.Count==0) safe++;
                grid.RemovePath(candidate.Id);
                if(safe>controls.MaximumStartingFreeArrows) { report.RejectedCandidates++; continue; }
                // Every new cycle must include the new arrow. Its dependencies must not
                // reach an old arrow whose exit is now obstructed by this new body.
                var ancestors=new HashSet<int>(); var pending=new Stack<int>(blockers); bool cycle=false;
                while(pending.Count>0)
                {
                    token.ThrowIfCancellationRequested(); int id=pending.Pop();
                    if(affected.Contains(id)) { cycle=true; break; }
                    if(ancestors.Add(id)) foreach(int dependency in effectiveOwners[id]) pending.Push(dependency);
                }
                if(cycle) { report.RejectedCandidates++; continue; }
                // Establish short dependency chains first. The independent Amount setting
                // is applied after packing, so raising density cannot starve construction.
                double value=-100*safe-Math.Abs(blockers.Count-1)+candidate.CellIds.Count*.05;
                if(value>best) { best=value; selected=candidate; selectedGate=gate; }
            }
            return selected!=null;
        }

        private bool SelectControlledCandidate(ISurfaceTopology t,SurfaceGrid grid,FreeCells free,FaceComponents faces,bool forceCross,
            HashSet<CellGate> gateLookup,List<CellGate> gates,ExitMap exits,PuzzleRecipe recipe,StableRandom random,List<ArrowPath> paths,
            ref int attempts,int limit,GenerationReport report,CancellationToken token,out ArrowPath selected,out CellGate? selectedGate)
        {
            selected=null; selectedGate=null; var controls=recipe.DependencyControlSettings ?? new DependencyControlSettings { MaximumStartingFreeArrows=int.MaxValue };
            var safe=new List<ArrowPath>();
            var targets=new Dictionary<int,int>();
            foreach(var old in paths)
            {
                token.ThrowIfCancellationRequested();
                var actual=simulator.Simulate(grid,old.Id,exits,recipe.Rules);
                if(!actual.Succeeded) continue;
                safe.Add(old);
                var alone=simulator.Simulate(new SurfaceGrid(t,new[] { old }),old.Id,exits,recipe.Rules);
                var corridor=new HashSet<int>();
                for(int i=old.CellIds.Count;i<alone.CellRail.Count;i++) if(alone.CellRail[i].IsOnSurface) corridor.Add(alone.CellRail[i].CellId);
                foreach(var cell in corridor) if(!grid.TryGetCellOwner(cell,out _)) { targets.TryGetValue(cell,out int count); targets[cell]=count+1; }
            }
            var points=new List<int>(targets.Keys);
            points.Sort((a,b)=> { int compare=targets[b].CompareTo(targets[a]); return compare!=0 ? compare : t.GetCellIndex(a).CompareTo(t.GetCellIndex(b)); });
            double best=double.NegativeInfinity;
            int proposals=recipe.DifficultySettings==null ? 24 : Math.Min(24,recipe.DifficultySettings.CandidatePoolSize);
            for(int trial=0;trial<proposals && attempts<limit;trial++)
            {
                token.ThrowIfCancellationRequested(); attempts++; report.CandidateAttempts++;
                int? required=null;
                if(!forceCross && points.Count>0 && random.Chance(.85)) required=points[random.Next(Math.Max(1,Math.Min(points.Count,trial<12 ? 8 : points.Count)))];
                if(!TryCandidate(t,grid,free,faces,forceCross,gateLookup,gates.Count,recipe,random,paths.Count,out var candidate,out var gate,required))
                { report.RejectedCandidates++; continue; }
                var map=gate.HasValue ? new ExitMap(t,new List<CellGate>(gates) { gate.Value }) : exits;
                grid.AddPath(candidate); var plan=simulator.Simulate(grid,candidate.Id,map,recipe.Rules);
                if(!InitialExitClearance.IsClear(plan)) { grid.RemovePath(candidate.Id); report.RejectedCandidates++; continue; }
                bool futureCell=false;
                for(int i=candidate.CellIds.Count;i<plan.CellRail.Count;i++) if(plan.CellRail[i].IsOnSurface) { futureCell=true; break; }
                if(!futureCell && grid.OccupiedCellCount/(double)t.CellCount<recipe.Profile.MinimumCoverage)
                { grid.RemovePath(candidate.Id); report.RejectedCandidates++; continue; }
                int freeAfter=1;
                var inspect=gate.HasValue ? paths : safe;
                foreach(var old in inspect) if(simulator.Simulate(grid,old.Id,map,recipe.Rules).Succeeded) freeAfter++;
                if(!forceCross && freeAfter>Math.Max(safe.Count,controls.MaximumStartingFreeArrows))
                { grid.RemovePath(candidate.Id); report.RejectedCandidates++; continue; }
                grid.RemovePath(candidate.Id);
                double value=-100*freeAfter;
                if(value>best) { best=value; selected=candidate; selectedGate=gate; }
                if(forceCross) break;
            }
            return selected!=null;
        }

        private static int? ChooseTargetCell(ISurfaceTopology t,SurfaceGrid grid,List<ArrowPath> paths,ProposalContext context,MultiBlockerSettings settings,StableRandom random)
        {
            int start=random.Next(paths.Count); ArrowPath chosen=null; double best=-1;
            for(int i=0;i<Math.Min(16,paths.Count);i++)
            {
                var path=paths[(start+i)%paths.Count]; int blockers=context.Owners[path.Id].Count;
                if(blockers>=settings.MaxTargetBlockers) continue;
                double priority=(blockers==0 ? .3 : 1.0)+1.0/(1+blockers);
                if(priority>best) { chosen=path; best=priority; }
            }
            if(chosen==null) return null;
            var cells=new List<int>(); foreach(var cell in context.Routes[chosen.Id]) if(!grid.TryGetCellOwner(cell,out _)) cells.Add(cell);
            cells.Sort((a,b)=>t.GetCellIndex(a).CompareTo(t.GetCellIndex(b)));
            if(cells.Count==0) return null;
            var selected=cells[random.Next(cells.Count)];
            if(t.Generation().ExitDistance(chosen.HeadCellId)==0)
                for(int sample=0;sample<4;sample++) { var next=cells[random.Next(cells.Count)]; if(t.Generation().ExitDistance(next)>t.Generation().ExitDistance(selected)) selected=next; }
            return selected;
        }

        internal sealed class TargetedMutation
        {
            public PuzzleDefinition Definition;
            public List<int> Solution;
        }
        internal TargetedMutation TargetBlocker(GeneratedPuzzle puzzle,PuzzleDependencyGraph graph,DifficultySettings settings,StableRandom random,
            GenerationReport report,RemovalTrajectoryCache cache,CancellationToken token)
        {
            var targetGrid=puzzle.Recipe.Shape!=null ? puzzle.CreateGrid() : null;
            var nodes=graph.Nodes; int start=random.Next(nodes.Length); DependencyNode target=null; double best=-1;
            for(int i=0;i<nodes.Length;i++)
            {
                var node=nodes[(start+i)%nodes.Length]; int n=node.IndependentBlockers.Length;
                if(n>=settings.MultiBlocker.MaxTargetBlockers) continue;
                if(targetGrid!=null)
                {
                    ArrowPath targetPath=targetGrid.Paths[node.PathId]; bool room=false;
                    foreach(int cell in cache.Get(puzzle.Surface,targetPath,puzzle.Exits,puzzle.Rules,token).SweptCellIds)
                        if(!targetGrid.TryGetCellOwner(cell,out _)) { room=true; break; }
                    if(!room) continue;
                }
                double priority=(n==0 ? .3 : 1.5)+1.0/(n+1)-.08*node.Depth+random.NextUInt()/4294967296.0*.5;
                if(priority>best) { best=priority; target=node; }
            }
            if(target==null) return null;
            var pathLookup=new Dictionary<int,ArrowPath>(); int nextId=0;
            foreach(var path in puzzle.Paths) { pathLookup.Add(path.Id,path); nextId=Math.Max(nextId,path.Id+1); }
            int victim=-1;
            if(settings.MultiBlocker.ReplaceExistingPaths)
            {
                foreach(var node in nodes)
                    if(node.PathId!=target.PathId && node.PathsBlocked.Length==0 && (victim<0 || pathLookup[node.PathId].CellIds.Count<pathLookup[victim].CellIds.Count)) victim=node.PathId;
                if(victim<0) return null;
            }
            else if(puzzle.Paths.Count>=puzzle.Recipe.Profile.MaxPathCount) return null;
            var retained=new List<ArrowPath>(); foreach(var path in puzzle.Paths) if(path.Id!=victim) retained.Add(path);
            var grid=new SurfaceGrid(puzzle.Surface,retained); var topology=puzzle.Surface;
            var trajectory=cache.Get(topology,pathLookup[target.PathId],puzzle.Exits,puzzle.Rules,token);
            var options=new List<int>();
            foreach(var cell in trajectory.SweptCellIds) if(!grid.TryGetCellOwner(cell,out _)) options.Add(cell);
            options.Sort((a,b)=>topology.GetCellIndex(a).CompareTo(topology.GetCellIndex(b)));
            if(options.Count==0) return null;
            // Preserve incomparability with existing blockers: do not block any of
            // their prerequisites. The replacement victim is a leaf with no dependants.
            var ancestors=new HashSet<int>(); var stack=new Stack<int>(target.IndependentBlockers);
            while(stack.Count>0) { int id=stack.Pop(); if(!ancestors.Add(id)) continue; foreach(int parent in graph.GetNode(id).ConfirmedBlockers) stack.Push(parent); }
            var gates=new HashSet<CellGate>(puzzle.Exits.CellGates); var free=new FreeCells(topology.CellCount);
            var faces=new FaceComponents(topology.Generation().RegionCount);
            for(int trial=0;trial<settings.CandidatePoolSize;trial++)
            {
                token.ThrowIfCancellationRequested(); report.TargetedCandidateAttempts++;
                var point=options[random.Next(options.Count)];
                if(!TryCandidate(topology,grid,free,faces,false,gates,gates.Count,puzzle.Recipe,random,nextId,out var added,out _,point,false)) continue;
                bool blocksSibling=false;
                foreach(int id in ancestors)
                {
                    var sibling=cache.Get(topology,pathLookup[id],puzzle.Exits,puzzle.Rules,token);
                    foreach(var cell in added.CellIds) if(sibling.Contains(cell)) { blocksSibling=true; break; }
                    if(blocksSibling) break;
                }
                if(blocksSibling) continue;
                grid.AddPath(added); bool safe=simulator.Simulate(grid,added.Id,puzzle.Exits,puzzle.Rules).Succeeded; grid.RemovePath(added.Id);
                if(!safe) continue;
                var paths=new List<ArrowPath>(retained) { added };
                var solution=new List<int> { added.Id }; foreach(int id in puzzle.VerifiedSolution) if(id!=victim) solution.Add(id);
                return new TargetedMutation { Definition=new PuzzleDefinition(puzzle.Name,topology,paths,puzzle.Exits,puzzle.Rules),Solution=solution };
            }
            return null;
        }

        private static void LegacyCrossPlacement(CubeTopology t,FaceComponents faces,StableRandom random,out int head,out SurfaceDirection heading)
        {
            int face=random.Next(6); var outward=(SurfaceDirection)random.Next(4);
            if(faces.Count>1)
            {
                int start=face*4+(int)outward;
                for(int i=0;i<24;i++)
                {
                    int edge=(start+i)%24; var transition=t.GetEdgeTransition((CubeFace)(edge/4),(SurfaceDirection)(edge%4));
                    if(faces.Same(edge/4,(int)transition.DestinationFace)) continue;
                    face=edge/4; outward=(SurfaceDirection)(edge%4); break;
                }
            }
            int offset=random.Next(t.GridSize),end=t.GridSize-1;
            head=t.GetCellIndex(new SurfaceCoordinate((CubeFace)face,
                outward==SurfaceDirection.Left ? 0 : outward==SurfaceDirection.Right ? end : offset,
                outward==SurfaceDirection.Down ? 0 : outward==SurfaceDirection.Up ? end : offset));
            heading=outward.Opposite();
        }

        private static int DistanceToEdge(SurfaceCoordinate cell,SurfaceDirection direction,int size)
        {
            switch(direction)
            {
                case SurfaceDirection.Right: return size-1-cell.X;
                case SurfaceDirection.Up: return size-1-cell.Y;
                case SurfaceDirection.Left: return cell.X;
                default: return cell.Y;
            }
        }

        private sealed class FreeCells
        {
            private readonly List<int> cells; private readonly int[] positions;
            public int Count=>cells.Count;
            public FreeCells(int count)
            { cells=new List<int>(count); positions=new int[count]; for(int i=0;i<count;i++) { cells.Add(i); positions[i]=i; } }
            public int At(int index)=>cells[index];
            public void Remove(int cell)
            {
                int index=positions[cell],last=cells[cells.Count-1];
                cells[index]=last; positions[last]=index; cells.RemoveAt(cells.Count-1); positions[cell]=-1;
            }
        }
        private sealed class FaceComponents
        {
            private readonly int[] parent; public int Count { get; private set; }
            public FaceComponents(int count) { Count=count; parent=new int[count]; for(int i=0;i<count;i++) parent[i]=i; }
            private int Root(int face) { while(parent[face]!=face) face=parent[face]; return face; }
            public bool Same(int a,int b)=>Root(a)==Root(b);
            public void Join(int a,int b) { a=Root(a); b=Root(b); if(a==b) return; parent[b]=a; Count--; }
        }
    }
}
