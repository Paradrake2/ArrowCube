using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ArrowCube.Movement;
using ArrowCube.Topology;

namespace ArrowCube.Generation
{
    /// <summary>Bounded deterministic reverse construction; every accepted candidate uses the gameplay simulator.</summary>
    public sealed class PuzzleGenerator
    {
        public const int CurrentVersion=5;
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
                var t=new CubeTopology(recipe.Profile.GridSize);
                var grid=new SurfaceGrid(t,Array.Empty<ArrowPath>());
                var paths=new List<ArrowPath>(); var gates=new List<ExitGate>();
                var gateLookup=new HashSet<ExitGate>(); var exits=new ExitMap(t,gates);
                var free=new FreeCells(t.CellCount); var faces=new FaceComponents();
                var proposalContext=new ProposalContext();
                bool dependencyOrder=recipe.DependencyControlSettings!=null && (restart+(recipe.Profile.GridSize>8 ? 1 : 0))%2==1;
                int crossPaths=0, stalls=0, limit=t.CellCount*recipe.AttemptsPerCell+2000;
                if(recipe.DifficultySettings!=null) limit=Math.Min(limit,recipe.DifficultySettings.MaxConstructionAttempts);
                // Version 1 is retained for saved recipes. Larger surfaces need an area-based
                // rejection budget: a linear budget stops packing long before the coverage floor.
                int stallLimit=800+t.GridSize*50;
                if(recipe.GenerationVersion>=2) stallLimit=Math.Max(stallLimit,t.CellCount*2);
                int attempt=0;
                while(attempt<limit && free.Count>=recipe.Profile.MinPathLength && paths.Count<recipe.Profile.MaxPathCount)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return null;
                    if(progress!=null && (attempt&63)==0) progress.Report(new GenerationProgress { Completed=attempt,Total=limit,Phase=$"Packing cube ({paths.Count} arrows)" });
                    if((attempt&63)==0) cancellationToken.ThrowIfCancellationRequested();
                    bool forceCross=crossPaths<recipe.Profile.MinCrossFacePaths || faces.Count>1;
                    ArrowPath path; ExitGate? proposedGate; bool found;
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
                        var proposed=new List<ExitGate>(gates) { proposedGate.Value };
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
                    foreach(var cell in path.Cells) free.Remove(t.GetCellIndex(cell));
                    foreach(var segment in path.Segments) if(segment.CrossesFace) faces.Join((int)segment.A.Face,(int)segment.B.Face);
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
            if(stats.CrossFacePaths<p.MinCrossFacePaths || stats.ConnectedFaces!=6) return "Cross-face coverage does not connect all six faces.";
            if(stats.AverageTurns<p.MinimumAverageTurns) return "Too few bends.";
            if(stats.BlockedFraction<p.MinimumBlockedFraction) return $"Too few dependencies ({stats.BlockedFraction:P1} initially blocked).";
            if(stats.ImmediatelyRemovablePaths<1) return "No safe opening move.";
            if(checkDependencyControls && recipe.DependencyControlSettings!=null && stats.ImmediatelyRemovablePaths>recipe.DependencyControlSettings.MaximumStartingFreeArrows)
                return $"Starting free arrows {stats.ImmediatelyRemovablePaths} exceeds the configured maximum {recipe.DependencyControlSettings.MaximumStartingFreeArrows}. Increase generation restarts or relax the opening limit.";
            if(recipe.Rules.EnableCubeEdgeExits && stats.EdgeExitsInSolution<1) return "No verified edge exit.";
            if(recipe.Rules.EnableDirectionalGates && stats.GateExitsInSolution<1) return "No verified gate exit.";
            return string.Empty;
        }

        private IEnumerable<GeneratedPuzzle> FillTailsSteps(CubeTopology t,SurfaceGrid grid,FreeCells free,List<ArrowPath> paths,
            ExitMap exits,PuzzleRecipe recipe,StableRandom random,CancellationToken token,bool preserveAllRoutes=false)
        {
            // Extending an older tail must never block a newer path's future leading route.
            // Descending construction order lets us protect precisely those routes, while
            // allowing extensions to create dependencies for still older paths.
            var protectedCells=new HashSet<SurfaceCoordinate>();
            // Dependency construction can use either insertion order. Tail fill must
            // preserve all existing dependencies rather than only newer exit routes.
            if(preserveAllRoutes) foreach(var path in paths)
            {
                token.ThrowIfCancellationRequested();
                yield return null;
                var route=simulator.Simulate(new SurfaceGrid(t,new[] { path }),path.Id,exits,recipe.Rules);
                for(int i=path.Cells.Count;i<route.Rail.Count;i++) if(route.Rail[i].IsOnSurface) protectedCells.Add(route.Rail[i].Cell);
            }
            for(int index=paths.Count-1;index>=0;index--)
            {
                token.ThrowIfCancellationRequested();
                yield return null;
                var original=paths[index];
                var alone=new SurfaceGrid(t,new[] { original });
                var plan=simulator.Simulate(alone,original.Id,exits,recipe.Rules);
                if(!plan.Succeeded) continue;
                for(int i=original.Cells.Count;i<plan.Rail.Count;i++)
                    if(plan.Rail[i].IsOnSurface) protectedCells.Add(plan.Rail[i].Cell);
                if(grid.OccupiedCellCount/(double)t.CellCount>=recipe.Profile.TargetCoverage) continue;
                var cells=new List<SurfaceCoordinate>(original.Cells);
                var options=new SurfaceCoordinate[4];
                while(cells.Count<recipe.Profile.MaxPathLength)
                {
                    var tail=cells[cells.Count-1]; int count=0;
                    for(int d=0;d<4;d++)
                    {
                        var neighbor=t.Step(tail,(SurfaceDirection)d).Coordinate;
                        if(!protectedCells.Contains(neighbor) && !grid.TryGetCellOwner(neighbor,out _) && !cells.Contains(neighbor))
                            options[count++]=neighbor;
                    }
                    if(count==0) break;
                    var next=options[random.Next(count)]; cells.Add(next);
                    // Mark immediately so another branch cannot reuse a just-added extension.
                    free.Remove(t.GetCellIndex(next));
                }
                if(cells.Count==original.Cells.Count) continue;
                var replacement=new ArrowPath(original.Id,t,cells,original.HeadDirection);
                grid.RemovePath(original.Id); grid.AddPath(replacement); paths[index]=replacement;
            }
        }

        private bool TryCandidate(CubeTopology t,SurfaceGrid grid,FreeCells free,FaceComponents faces,bool forceCross,
            HashSet<ExitGate> gates,int gateCount,PuzzleRecipe recipe,StableRandom random,int id,
            out ArrowPath path,out ExitGate? proposedGate,SurfaceCoordinate? requiredCell=null,bool allowNewGate=true,
            bool allowBlockedExit=false,SurfaceDirection? requiredHeading=null)
        {
            path=null; proposedGate=null;
            SurfaceCoordinate head; SurfaceDirection heading;
            if(requiredCell.HasValue) { head=requiredCell.Value; heading=requiredHeading ?? (SurfaceDirection)random.Next(4); }
            else if(forceCross)
            {
                int face=random.Next(6); var outward=(SurfaceDirection)random.Next(4);
                if(faces.Count>1)
                {
                    // Search the fixed cube edge order from a randomized starting point for a bridge.
                    int start=face*4+(int)outward;
                    for(int i=0;i<24;i++)
                    {
                        int edge=(start+i)%24;
                        var transition=t.GetEdgeTransition((CubeFace)(edge/4),(SurfaceDirection)(edge%4));
                        if(faces.Same(edge/4,(int)transition.DestinationFace)) continue;
                        face=edge/4; outward=(SurfaceDirection)(edge%4); break;
                    }
                }
                int offset=random.Next(t.GridSize), end=t.GridSize-1;
                head=new SurfaceCoordinate((CubeFace)face,
                    outward==SurfaceDirection.Left ? 0 : outward==SurfaceDirection.Right ? end : offset,
                    outward==SurfaceDirection.Down ? 0 : outward==SurfaceDirection.Up ? end : offset);
                heading=outward.Opposite();
                if(grid.TryGetCellOwner(head,out _)) return false;
            }
            else
            {
                head=t.GetCoordinate(free.At(random.Next(free.Count))); heading=(SurfaceDirection)random.Next(4);
                if(recipe.DifficultySettings!=null) SelectPlacement(t,free,recipe.DifficultySettings,random,ref head,ref heading);
                else if(recipe.GenerationVersion>=2 && t.GridSize>20 && recipe.Rules.EnableCubeEdgeExits && !recipe.Rules.EnableDirectionalGates)
                {
                    // Build inner paths first so outer paths can later block them. Uniform
                    // sampling packs the perimeter first and cuts off the interior of large faces.
                    if(random.Chance(.8))
                        for(int sample=0;sample<12;sample++)
                        {
                            var candidate=t.GetCoordinate(free.At(random.Next(free.Count)));
                            if(EdgeDistance(candidate,t.GridSize)>EdgeDistance(head,t.GridSize)) head=candidate;
                        }
                    int distance=EdgeDistance(head,t.GridSize);
                    int start=random.Next(4);
                    for(int d=0;d<4;d++)
                    {
                        var outward=(SurfaceDirection)((start+d)%4);
                        if(DistanceToEdge(head,outward,t.GridSize)==distance) { heading=outward; break; }
                    }
                }
            }
            var neck=t.Step(head,heading.Opposite());
            if(grid.TryGetCellOwner(head,out _) || grid.TryGetCellOwner(neck.Coordinate,out _)) return false;
            bool mayAddGate=allowNewGate && recipe.Rules.EnableDirectionalGates && gateCount<recipe.Profile.MaxGates;
            bool preferGate=mayAddGate && (!recipe.Rules.EnableCubeEdgeExits || random.Chance(.35));
            int gateDistance=1+random.Next(Math.Max(1,t.GridSize/2));
            var corridor=new HashSet<SurfaceCoordinate> { head };
            var directed=new HashSet<ExitGate>();
            var current=head; var direction=heading;
            bool foundExit=false;
            for(int distance=0;distance<=4*t.GridSize;distance++)
            {
                var next=t.Step(current,direction);
                bool edge=recipe.Rules.EnableCubeEdgeExits && next.CrossedEdge;
                if(edge || recipe.Rules.EnableDirectionalGates && gates.Contains(new ExitGate(current,direction)))
                { foundExit=true; break; }
                if(preferGate && distance>=gateDistance)
                { proposedGate=new ExitGate(current,direction); foundExit=true; break; }
                if(!directed.Add(new ExitGate(current,direction)) || !allowBlockedExit && grid.TryGetCellOwner(next.Coordinate,out _))
                {
                    if(mayAddGate && distance>=1)
                    { proposedGate=new ExitGate(current,direction); foundExit=true; }
                    break;
                }
                corridor.Add(next.Coordinate); current=next.Coordinate; direction=next.Direction;
            }
            if(!foundExit || corridor.Contains(neck.Coordinate)) return false;
            var cells=new List<SurfaceCoordinate> { head,neck.Coordinate };
            var visited=new HashSet<SurfaceCoordinate> { head,neck.Coordinate };
            current=neck.Coordinate; direction=neck.Direction;
            int length=recipe.Profile.MinPathLength+random.Next(recipe.Profile.MaxPathLength-recipe.Profile.MinPathLength+1);
            var neighbors=new SurfaceStep[4]; var weights=new double[4];
            while(cells.Count<length)
            {
                int count=0; double total=0;
                for(int d=0;d<4;d++)
                {
                    var choice=(SurfaceDirection)d;
                    var step=t.Step(current,choice);
                    if(visited.Contains(step.Coordinate) || corridor.Contains(step.Coordinate)
                        || grid.TryGetCellOwner(step.Coordinate,out _)) continue;
                    double weight=choice==direction ? 1-recipe.Profile.TurnProbability : recipe.Profile.TurnProbability*.5;
                    // Favor spare neighbors rather than walking into an avoidable one-cell pocket.
                    int degree=0;
                    for(int n=0;n<4;n++)
                    {
                        var neighbor=t.Step(step.Coordinate,(SurfaceDirection)n).Coordinate;
                        if(!visited.Contains(neighbor) && !corridor.Contains(neighbor) && !grid.TryGetCellOwner(neighbor,out _)) degree++;
                    }
                    weight*=1+degree*.25;
                    neighbors[count]=step; weights[count]=weight; total+=weight; count++;
                }
                if(count==0) break;
                double pick=random.NextUInt()/(double)4294967296UL*total;
                int selected=count-1;
                for(int i=0;i<count;i++) { pick-=weights[i]; if(pick<0) { selected=i; break; } }
                var chosen=neighbors[selected]; current=chosen.Coordinate; direction=chosen.Direction;
                visited.Add(current); cells.Add(current);
            }
            if(cells.Count<recipe.Profile.MinPathLength) return false;
            path=new ArrowPath(id,t,cells,heading);
            return true;
        }

        private static int EdgeDistance(SurfaceCoordinate cell,int size) =>
            Math.Min(Math.Min(cell.X,cell.Y),Math.Min(size-1-cell.X,size-1-cell.Y));

        private static void SelectPlacement(CubeTopology t,FreeCells free,DifficultySettings settings,StableRandom random,
            ref SurfaceCoordinate head,ref SurfaceDirection heading)
        {
            var strategy=settings.Placement;
            if(strategy==PlacementStrategy.Mixed || strategy==PlacementStrategy.DifficultyOptimized)
                strategy=(PlacementStrategy)random.Next(3);
            if(strategy==PlacementStrategy.UniformRandom) return;
            for(int i=0;i<6;i++)
            {
                var next=t.GetCoordinate(free.At(random.Next(free.Count)));
                int difference=EdgeDistance(next,t.GridSize)-EdgeDistance(head,t.GridSize);
                if(strategy==PlacementStrategy.InteriorBiased ? difference>0 : difference<0) head=next;
            }
            // Headings remain varied. Placement no longer forces nearest-edge exits.
        }

        private bool SelectCandidate(CubeTopology t,SurfaceGrid grid,FreeCells free,FaceComponents faces,bool forceCross,
            HashSet<ExitGate> gateLookup,List<ExitGate> gates,ExitMap exits,PuzzleRecipe recipe,StableRandom random,List<ArrowPath> paths,
            ProposalContext context,ref int attempts,int limit,GenerationReport report,CancellationToken token,out ArrowPath selected,out ExitGate? selectedGate)
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
                    var rail=recipe.GenerationVersion>=4 ? context.Cache.Get(t,old,exits,recipe.Rules,token).Rail
                        : simulator.Simulate(new SurfaceGrid(t,new[] { old }),old.Id,exits,recipe.Rules).Rail;
                    var cells=new HashSet<SurfaceCoordinate>();
                    for(int i=old.Cells.Count;i<rail.Count;i++) if(rail[i].IsOnSurface) cells.Add(rail[i].Cell);
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
                SurfaceCoordinate? required=null;
                if(multi && !forceCross && settings.MultiBlocker.EnableTargetedPlacement && paths.Count>=8 && random.Chance(.60))
                    required=ChooseTargetCell(t,grid,paths,context,settings.MultiBlocker,random);
                if(!TryCandidate(t,grid,free,faces,cross && !required.HasValue,gateLookup,gates.Count,recipe,random,paths.Count,out var path,out var gate,required))
                { report.RejectedCandidates++; continue; }
                var proposed=gate.HasValue ? new ExitMap(t,new List<ExitGate>(gates) { gate.Value }) : exits;
                grid.AddPath(path);
                var candidatePlan=simulator.Simulate(grid,path.Id,proposed,recipe.Rules);
                if(!candidatePlan.Succeeded)
                { grid.RemovePath(path.Id); report.RejectedCandidates++; continue; }
                int hits=0,newlyBlocked=0,crossLinks=0,inversions=0,unblocked=0,lostLinks=0;
                double multiGain=0; var affected=multi ? new HashSet<int>() : null;
                foreach(var old in paths)
                {
                    var routeCells=routes[old.Id];
                    bool gateOnRoute=gate.HasValue && routeCells.Contains(gate.Value.Cell);
                    if(gateOnRoute)
                    {
                        var route=simulator.Simulate(new SurfaceGrid(t,new[] { old }),old.Id,proposed,recipe.Rules);
                        routeCells=new HashSet<SurfaceCoordinate>();
                        for(int i=old.Cells.Count;i<route.Rail.Count;i++) if(route.Rail[i].IsOnSurface) routeCells.Add(route.Rail[i].Cell);
                        if(multi)
                        {
                            var retainedOwners=new HashSet<int>();
                            foreach(var cell in routeCells) if(grid.TryGetCellOwner(cell,out int owner) && owner!=old.Id && owner!=path.Id) retainedOwners.Add(owner);
                            lostLinks+=Math.Max(0,context.Owners[old.Id].Count-retainedOwners.Count);
                        }
                    }
                    bool hit=false; foreach(var cell in path.Cells) if(routeCells.Contains(cell)) { hit=true; break; }
                    if(hit)
                    {
                        hits++; if(oldSafe.Contains(old.Id)) newlyBlocked++;
                        if(old.Head.Face!=path.Head.Face) crossLinks++;
                        if(EdgeDistance(old.Head,t.GridSize)<EdgeDistance(path.Head,t.GridSize)) inversions++;
                        if(multi)
                        {
                            affected.Add(old.Id); int before=context.Owners[old.Id].Count;
                            multiGain+=before>=settings.MultiBlocker.MaxTargetBlockers ? -.6 : before==0 ? .8 : 1.5/(1+.25*Math.Max(0,before-1));
                            int union=PuzzleDependencyGraph.FaceMask(old)|PuzzleDependencyGraph.FaceMask(path);
                            if((union&(union-1))!=0) multiGain+=settings.MultiBlocker.CrossFacePreference*.5;
                        }
                    }
                    if(gateOnRoute && !oldSafe.Contains(old.Id) && simulator.Simulate(grid,old.Id,proposed,recipe.Rules).Succeeded) unblocked++;
                }
                grid.RemovePath(path.Id);
                double safeFraction=(oldSafe.Count-newlyBlocked+unblocked+1)/(double)(paths.Count+1);
                double strength=settings.DependencyStrength;
                int leadingCells=0;
                for(int i=path.Cells.Count;i<candidatePlan.Rail.Count;i++) if(candidatePlan.Rail[i].IsOnSurface) leadingCells++;
                double value=-(Math.Abs(safeFraction-settings.DesiredSafeFraction)*4)
                    +(strength*2-1)*(newlyBlocked+hits*.35)
                    +strength*(crossLinks*.6+inversions*settings.SpatialUnpredictability)
                    +settings.EdgeResistance*inversions*.8
                    +(strength*2-1)*3*leadingCells/t.GridSize;
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
            public readonly Dictionary<int,HashSet<SurfaceCoordinate>> Routes=new Dictionary<int,HashSet<SurfaceCoordinate>>();
            public readonly HashSet<int> OldSafe=new HashSet<int>();
            public readonly Dictionary<int,HashSet<int>> Owners=new Dictionary<int,HashSet<int>>();
            public readonly RemovalTrajectoryCache Cache=new RemovalTrajectoryCache();
        }

        // Dependency construction allows blocked additions and rejects cycles before committing.
        // Unlike reverse-only construction, growth does not need another free opening move.
        private bool SelectDependencyCandidate(CubeTopology t,SurfaceGrid grid,FreeCells free,FaceComponents faces,bool forceCross,
            HashSet<ExitGate> gateLookup,List<ExitGate> gates,ExitMap exits,PuzzleRecipe recipe,StableRandom random,List<ArrowPath> paths,
            ProposalContext context,ref int attempts,int limit,GenerationReport report,CancellationToken token,out ArrowPath selected,out ExitGate? selectedGate)
        {
            selected=null; selectedGate=null; var controls=recipe.DependencyControlSettings;
            if(context.PathCount!=paths.Count)
            {
                context.PathCount=paths.Count; context.Routes.Clear(); context.Owners.Clear();
                foreach(var old in paths)
                {
                    token.ThrowIfCancellationRequested();
                    var route=simulator.Simulate(new SurfaceGrid(t,new[] { old }),old.Id,exits,recipe.Rules);
                    var cells=new HashSet<SurfaceCoordinate>(); var owners=new HashSet<int>();
                    for(int i=old.Cells.Count;i<route.Rail.Count;i++) if(route.Rail[i].IsOnSurface)
                    {
                        var cell=route.Rail[i].Cell; cells.Add(cell);
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
                SurfaceCoordinate? required=null; SurfaceDirection? heading=null;
                if(paths.Count>0 && random.Chance(.75))
                {
                    var blocker=paths[random.Next(paths.Count)]; var contact=blocker.Cells[random.Next(blocker.Cells.Count)];
                    var outward=(SurfaceDirection)random.Next(4);
                    if(forceCross)
                    {
                        int start=(int)outward;
                        for(int d=0;d<4;d++)
                        {
                            var edge=(SurfaceDirection)((start+d)%4); var transition=t.GetEdgeTransition(contact.Face,edge);
                            if(faces.Count>1 && faces.Same((int)contact.Face,(int)transition.DestinationFace)) continue;
                            outward=edge; break;
                        }
                        int end=t.GridSize-1;
                        required=new SurfaceCoordinate(contact.Face,
                            outward==SurfaceDirection.Left ? 0 : outward==SurfaceDirection.Right ? end : contact.X,
                            outward==SurfaceDirection.Down ? 0 : outward==SurfaceDirection.Up ? end : contact.Y);
                        heading=outward.Opposite();
                    }
                    else
                    {
                        var head=contact; int distance=1+random.Next(t.GridSize-1);
                        for(int step=0;step<distance;step++)
                        { var next=t.Step(head,outward); if(next.CrossedEdge) break; head=next.Coordinate; }
                        required=head; heading=outward.Opposite();
                    }
                }
                if(!TryCandidate(t,grid,free,faces,forceCross,gateLookup,gates.Count,recipe,random,paths.Count,out var candidate,out var gate,
                    requiredCell:required,allowBlockedExit:true,requiredHeading:heading)) { report.RejectedCandidates++; continue; }
                var map=gate.HasValue ? new ExitMap(t,new List<ExitGate>(gates) { gate.Value }) : exits;
                var solo=simulator.Simulate(new SurfaceGrid(t,new[] { candidate }),candidate.Id,map,recipe.Rules);
                if(!InitialExitClearance.IsClear(solo)) { report.RejectedCandidates++; continue; }
                grid.AddPath(candidate); int safe=0;
                var affected=new HashSet<int>(); var effectiveOwners=new Dictionary<int,HashSet<int>>();
                foreach(var old in paths)
                {
                    var route=context.Routes[old.Id]; var owners=context.Owners[old.Id];
                    // A new gate can shorten an older route and remove existing dependencies.
                    if(gate.HasValue && route.Contains(gate.Value.Cell))
                    {
                        var changed=simulator.Simulate(new SurfaceGrid(t,new[] { old }),old.Id,map,recipe.Rules);
                        route=new HashSet<SurfaceCoordinate>(); owners=new HashSet<int>();
                        for(int i=old.Cells.Count;i<changed.Rail.Count;i++) if(changed.Rail[i].IsOnSurface)
                        {
                            var cell=changed.Rail[i].Cell; route.Add(cell);
                            if(grid.TryGetCellOwner(cell,out int id) && id!=old.Id && id!=candidate.Id) owners.Add(id);
                        }
                    }
                    bool hit=false; foreach(var cell in candidate.Cells) if(route.Contains(cell)) { hit=true; break; }
                    if(hit) affected.Add(old.Id);
                    if(!hit && owners.Count==0) safe++;
                    effectiveOwners.Add(old.Id,owners);
                }
                var blockers=new HashSet<int>();
                for(int i=candidate.Cells.Count;i<solo.Rail.Count;i++)
                    if(solo.Rail[i].IsOnSurface && grid.TryGetCellOwner(solo.Rail[i].Cell,out int owner) && owner!=candidate.Id) blockers.Add(owner);
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
                double value=-100*safe-Math.Abs(blockers.Count-1)+candidate.Cells.Count*.05;
                if(value>best) { best=value; selected=candidate; selectedGate=gate; }
            }
            return selected!=null;
        }

        private bool SelectControlledCandidate(CubeTopology t,SurfaceGrid grid,FreeCells free,FaceComponents faces,bool forceCross,
            HashSet<ExitGate> gateLookup,List<ExitGate> gates,ExitMap exits,PuzzleRecipe recipe,StableRandom random,List<ArrowPath> paths,
            ref int attempts,int limit,GenerationReport report,CancellationToken token,out ArrowPath selected,out ExitGate? selectedGate)
        {
            selected=null; selectedGate=null; var controls=recipe.DependencyControlSettings;
            var safe=new List<ArrowPath>();
            var targets=new Dictionary<SurfaceCoordinate,int>();
            foreach(var old in paths)
            {
                token.ThrowIfCancellationRequested();
                var actual=simulator.Simulate(grid,old.Id,exits,recipe.Rules);
                if(!actual.Succeeded) continue;
                safe.Add(old);
                var alone=simulator.Simulate(new SurfaceGrid(t,new[] { old }),old.Id,exits,recipe.Rules);
                var corridor=new HashSet<SurfaceCoordinate>();
                for(int i=old.Cells.Count;i<alone.Rail.Count;i++) if(alone.Rail[i].IsOnSurface) corridor.Add(alone.Rail[i].Cell);
                foreach(var cell in corridor) if(!grid.TryGetCellOwner(cell,out _)) { targets.TryGetValue(cell,out int count); targets[cell]=count+1; }
            }
            var points=new List<SurfaceCoordinate>(targets.Keys);
            points.Sort((a,b)=> { int compare=targets[b].CompareTo(targets[a]); return compare!=0 ? compare : t.GetCellIndex(a).CompareTo(t.GetCellIndex(b)); });
            double best=double.NegativeInfinity;
            int proposals=recipe.DifficultySettings==null ? 24 : Math.Min(24,recipe.DifficultySettings.CandidatePoolSize);
            for(int trial=0;trial<proposals && attempts<limit;trial++)
            {
                token.ThrowIfCancellationRequested(); attempts++; report.CandidateAttempts++;
                SurfaceCoordinate? required=null;
                if(!forceCross && points.Count>0 && random.Chance(.85)) required=points[random.Next(Math.Max(1,Math.Min(points.Count,trial<12 ? 8 : points.Count)))];
                if(!TryCandidate(t,grid,free,faces,forceCross,gateLookup,gates.Count,recipe,random,paths.Count,out var candidate,out var gate,required))
                { report.RejectedCandidates++; continue; }
                var map=gate.HasValue ? new ExitMap(t,new List<ExitGate>(gates) { gate.Value }) : exits;
                grid.AddPath(candidate); var plan=simulator.Simulate(grid,candidate.Id,map,recipe.Rules);
                if(!InitialExitClearance.IsClear(plan)) { grid.RemovePath(candidate.Id); report.RejectedCandidates++; continue; }
                bool futureCell=false;
                for(int i=candidate.Cells.Count;i<plan.Rail.Count;i++) if(plan.Rail[i].IsOnSurface) { futureCell=true; break; }
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

        private static SurfaceCoordinate? ChooseTargetCell(CubeTopology t,SurfaceGrid grid,List<ArrowPath> paths,ProposalContext context,MultiBlockerSettings settings,StableRandom random)
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
            var cells=new List<SurfaceCoordinate>(); foreach(var cell in context.Routes[chosen.Id]) if(!grid.TryGetCellOwner(cell,out _)) cells.Add(cell);
            cells.Sort((a,b)=>t.GetCellIndex(a).CompareTo(t.GetCellIndex(b)));
            if(cells.Count==0) return null;
            var selected=cells[random.Next(cells.Count)];
            if(EdgeDistance(chosen.Head,t.GridSize)==0)
                for(int sample=0;sample<4;sample++) { var next=cells[random.Next(cells.Count)]; if(EdgeDistance(next,t.GridSize)>EdgeDistance(selected,t.GridSize)) selected=next; }
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
            var nodes=graph.Nodes; int start=random.Next(nodes.Length); DependencyNode target=null; double best=-1;
            for(int i=0;i<nodes.Length;i++)
            {
                var node=nodes[(start+i)%nodes.Length]; int n=node.IndependentBlockers.Length;
                if(n>=settings.MultiBlocker.MaxTargetBlockers) continue;
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
                    if(node.PathId!=target.PathId && node.PathsBlocked.Length==0 && (victim<0 || pathLookup[node.PathId].Cells.Count<pathLookup[victim].Cells.Count)) victim=node.PathId;
                if(victim<0) return null;
            }
            else if(puzzle.Paths.Count>=puzzle.Recipe.Profile.MaxPathCount) return null;
            var retained=new List<ArrowPath>(); foreach(var path in puzzle.Paths) if(path.Id!=victim) retained.Add(path);
            var grid=new SurfaceGrid(puzzle.Topology,retained); var topology=puzzle.Topology;
            var trajectory=cache.Get(topology,pathLookup[target.PathId],puzzle.Exits,puzzle.Rules,token);
            var options=new List<SurfaceCoordinate>();
            foreach(var cell in trajectory.SweptCells) if(!grid.TryGetCellOwner(cell,out _)) options.Add(cell);
            options.Sort((a,b)=>topology.GetCellIndex(a).CompareTo(topology.GetCellIndex(b)));
            if(options.Count==0) return null;
            // Preserve incomparability with existing blockers: do not block any of
            // their prerequisites. The replacement victim is a leaf with no dependants.
            var ancestors=new HashSet<int>(); var stack=new Stack<int>(target.IndependentBlockers);
            while(stack.Count>0) { int id=stack.Pop(); if(!ancestors.Add(id)) continue; foreach(int parent in graph.GetNode(id).ConfirmedBlockers) stack.Push(parent); }
            var gates=new HashSet<ExitGate>(puzzle.Exits.Gates); var free=new FreeCells(topology.CellCount);
            var faces=new FaceComponents();
            for(int trial=0;trial<settings.CandidatePoolSize;trial++)
            {
                token.ThrowIfCancellationRequested(); report.TargetedCandidateAttempts++;
                var point=options[random.Next(options.Count)];
                if(!TryCandidate(topology,grid,free,faces,false,gates,gates.Count,puzzle.Recipe,random,nextId,out var added,out _,point,false)) continue;
                bool blocksSibling=false;
                foreach(int id in ancestors)
                {
                    var sibling=cache.Get(topology,pathLookup[id],puzzle.Exits,puzzle.Rules,token);
                    foreach(var cell in added.Cells) if(sibling.Contains(cell)) { blocksSibling=true; break; }
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
            private readonly int[] parent={0,1,2,3,4,5}; public int Count { get; private set; }=6;
            private int Root(int face) { while(parent[face]!=face) face=parent[face]; return face; }
            public bool Same(int a,int b)=>Root(a)==Root(b);
            public void Join(int a,int b) { a=Root(a); b=Root(b); if(a==b) return; parent[b]=a; Count--; }
        }
    }
}
