using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ArrowCube.Movement;
using ArrowCube.Topology;

namespace ArrowCube.Generation
{
    /// <summary>Deterministic bounded whole-layout selection followed by transactional local mutations.</summary>
    internal sealed class DifficultyAwareGeneration
    {
        public IEnumerable<GeneratedPuzzle> GenerateSteps(PuzzleGenerator generator,PuzzleRecipe recipe,IProgress<GenerationProgress> progress,CancellationToken token)
        {
            var settings=recipe.DifficultySettings; var watch=Stopwatch.StartNew();
            var cache=new RemovalTrajectoryCache(); int analysisVersion=recipe.GenerationVersion>=4 ? 3 : 2;
            var report=new GenerationReport(); var analyzer=new PuzzleDifficultyAnalyzer(); var validator=new PuzzleValidator();
            GeneratedPuzzle best=null; PuzzleDifficultyReport bestDifficulty=null; double bestLoss=double.MaxValue;
            string lastError="No valid candidate.";
            int targetedBudget=recipe.GenerationVersion>=4 && settings.MultiBlocker.Enabled && settings.MultiBlocker.EnableTargetedPlacement ? settings.MultiBlocker.TargetedBlockerAttempts : 0;
            int total=settings.MaxCandidates+settings.OptimizationIterations+targetedBudget;
            for(int index=0;index<settings.MaxCandidates;index++)
            {
                token.ThrowIfCancellationRequested();
                progress?.Report(new GenerationProgress { Completed=index,Total=total,Phase="Constructing and measuring candidates",BestScore=bestDifficulty?.MeasuredScore??0 });
                report.LayoutCandidates++;
                GeneratedPuzzle candidate=null; PuzzleGenerationException rejection=null;
                using(var construction=generator.ConstructSteps(recipe,index,progress,token).GetEnumerator())
                {
                    while(true)
                    {
                        bool more;
                        try { more=construction.MoveNext(); }
                        catch(PuzzleGenerationException error) { rejection=error; break; }
                        if(!more) break;
                        if(construction.Current==null) yield return null; else candidate=construction.Current;
                    }
                }
                if(rejection!=null) { Add(report,rejection.Report); lastError=rejection.Message; continue; }
                Add(report,candidate.Report);
                progress?.Report(new GenerationProgress { Completed=index,Total=total,Phase="Measuring puzzle",BestScore=bestDifficulty?.MeasuredScore??0 });
                yield return null;
                var measured=analyzer.Analyze(candidate,recipe.Seed,settings.Weights,token,settings.AlternativeOrderSamples,analysisVersion,cache);
                double loss=Loss(measured,settings,recipe.DependencyControlSettings);
                if(loss<bestLoss) { best=candidate; bestDifficulty=measured; bestLoss=loss; }
            }
            if(best==null) { report.Milliseconds=watch.Elapsed.TotalMilliseconds; throw new PuzzleGenerationException(recipe,report,lastError); }
            var random=new StableRandom(recipe.Seed,72317);
            for(int iteration=0;iteration<settings.OptimizationIterations;iteration++)
            {
                token.ThrowIfCancellationRequested(); report.OptimizationAttempts++;
                progress?.Report(new GenerationProgress { Completed=settings.MaxCandidates+iteration,Total=total,Phase="Optimizing validated geometry",BestScore=bestDifficulty.MeasuredScore });
                yield return null;
                PuzzleDefinition mutation;
                try { mutation=Mutate(best,bestDifficulty,random,settings); }
                catch(ArgumentException) { report.OptimizationRejected++; continue; }
                if(mutation==null) { report.OptimizationRejected++; continue; }
                var validation=validator.Validate(mutation,cancellationToken:token,requireClearInitialExit:recipe.GenerationVersion>=5);
                if(!validation.Succeeded) { report.OptimizationRejected++; continue; }
                if(PuzzleGenerator.QualityError(recipe,validation.Statistics).Length!=0) { report.OptimizationRejected++; continue; }
                var measured=analyzer.Analyze(mutation,recipe.Seed,settings.Weights,token,settings.AlternativeOrderSamples,analysisVersion,cache);
                double loss=Loss(measured,settings,recipe.DependencyControlSettings);
                if(loss>=bestLoss) continue;
                best=new GeneratedPuzzle(recipe,mutation,validation,report); bestDifficulty=measured; bestLoss=loss; report.OptimizationAccepted++;
            }
            for(int iteration=0;iteration<targetedBudget;iteration++)
            {
                token.ThrowIfCancellationRequested(); report.TargetedAttempts++;
                progress?.Report(new GenerationProgress { Completed=settings.MaxCandidates+settings.OptimizationIterations+iteration,Total=total,Phase="Placing independent blockers",BestScore=bestDifficulty.MeasuredScore });
                yield return null;
                var mutation=generator.TargetBlocker(best,bestDifficulty.Graph,settings,random,report,cache,token);
                if(mutation==null) continue;
                var validation=validator.Validate(mutation.Definition,mutation.Solution,token,recipe.GenerationVersion>=5);
                if(!validation.Succeeded || PuzzleGenerator.QualityError(recipe,validation.Statistics).Length!=0) { report.OptimizationRejected++; continue; }
                var measured=analyzer.Analyze(mutation.Definition,recipe.Seed,settings.Weights,token,settings.AlternativeOrderSamples,analysisVersion,cache);
                double loss=Loss(measured,settings,recipe.DependencyControlSettings);
                if(loss>=bestLoss) continue;
                best=new GeneratedPuzzle(recipe,mutation.Definition,validation,report); bestDifficulty=measured; bestLoss=loss; report.TargetedAccepted++;
            }
            // The accepted immutable candidate is never mutated, even when every optimization fails.
            var final=validator.Validate(best,best.VerifiedSolution,token,recipe.GenerationVersion>=5);
            if(!final.Succeeded) throw new InvalidOperationException("Accepted candidate lost its verified solution: "+final.Error);
            report.Milliseconds=watch.Elapsed.TotalMilliseconds; report.TargetCoverageReached=final.Statistics.Coverage>=recipe.Profile.TargetCoverage;
            bestDifficulty.Seed=recipe.Seed; bestDifficulty.GenerationVersion=recipe.GenerationVersion;
            bestDifficulty.RequestedScore=settings.TargetScore; bestDifficulty.GenerationMilliseconds=report.Milliseconds;
            bestDifficulty.ConfigurationEstimate=settings.ConfigurationEstimate(); bestDifficulty.ProfileName=recipe.Profile.Name;
            bestDifficulty.CandidatesEvaluated=report.LayoutCandidates; bestDifficulty.RejectedCandidates=report.RejectedCandidates;
            bestDifficulty.TargetReached=Math.Abs(bestDifficulty.MeasuredScore-settings.TargetScore)<=settings.ScoreTolerance;
            bestDifficulty.Warning=bestDifficulty.TargetReached ? "" : $"Requested score {settings.TargetScore:F0}; best verified score {bestDifficulty.MeasuredScore:F1}. Target not reached within the generation budget.";
            if(recipe.GenerationVersion>=4)
            {
                var m=settings.MultiBlocker;
                bestDifficulty.RequestedAverageBlockers=m.TargetAverageBlockers; bestDifficulty.RequestedIndependentMultiFraction=m.MultiBlockerPathFraction;
                bestDifficulty.MultiBlockerTargetsReached=!m.Enabled || (Math.Abs(bestDifficulty.MultiBlocker.IndependentMultiBlockerFraction-m.MultiBlockerPathFraction)<=.10
                    && bestDifficulty.MultiBlocker.AverageBlockersPerBlockedPath>=m.TargetAverageBlockers-.5);
                if(!bestDifficulty.MultiBlockerTargetsReached) bestDifficulty.Warning+=$" Independent multi-blocker target {m.MultiBlockerPathFraction:P0}; achieved {bestDifficulty.MultiBlocker.IndependentMultiBlockerFraction:P0}. Blocker targets not reached within the budget.";
                bestDifficulty.TrajectoryCacheHits=cache.Hits; bestDifficulty.TrajectoryCacheMisses=cache.Misses;
            }
            progress?.Report(new GenerationProgress { Completed=total,Total=total,Phase="Complete",BestScore=bestDifficulty.MeasuredScore });
            yield return new GeneratedPuzzle(recipe,best,final,report) { Difficulty=bestDifficulty,DependencyGraph=bestDifficulty.Graph,
                DependencyControlReport=DependencyControlTuner.MakeReport(recipe,final.Statistics,best.DependencyControlReport) };
        }

        private static double Loss(PuzzleDifficultyReport report,DifficultySettings settings,DependencyControlSettings controls)
        {
            double resistance=report.Components.EdgeResistance/100;
            double loss=Math.Abs(report.MeasuredScore-settings.TargetScore)
                +6*Math.Abs(report.InitialSafeFraction-settings.DesiredSafeFraction)
                +3*Math.Abs(Math.Min(1,report.AverageDependencyDepth/settings.DesiredDependencyDepth)-1)
                +3*Math.Abs(resistance-settings.EdgeResistance)
                +2*Math.Abs(report.Components.SpatialUnpredictability/100-settings.SpatialUnpredictability);
            if(report.MultiBlocker!=null && settings.MultiBlocker.Enabled)
            {
                var m=report.MultiBlocker; var target=settings.MultiBlocker;
                loss+=12*Math.Abs(m.IndependentMultiBlockerFraction-target.MultiBlockerPathFraction)
                    +6*Math.Abs(m.AverageBlockersPerBlockedPath-target.TargetAverageBlockers)/Math.Max(1,target.TargetAverageBlockers)
                    +6*Math.Max(0,m.MaxBlockerConcentration-target.MaxBlockerConcentration)
                    +3*Math.Abs(m.CrossFaceGeometryFraction-target.CrossFacePreference);
                loss+=60*Math.Max(0,settings.EdgeResistance-resistance);
                double sum=0; foreach(double v in target.BlockerDistribution) sum+=v;
                var actual=new double[target.BlockerDistribution.Length];
                for(int i=0;i<m.BlockerHistogram.Length;i++) actual[Math.Min(i,actual.Length-1)]+=m.BlockerHistogram[i]/(double)report.PathCount;
                for(int i=0;i<actual.Length;i++) loss+=3*Math.Abs(actual[i]-target.BlockerDistribution[i]/sum);
            }
            if(controls!=null && report.MultiBlocker!=null)
                loss+=10*Math.Max(0,controls.TargetBlockersPerBlockedArrow-report.MultiBlocker.AverageBlockersPerBlockedPath);
            return Math.Round(loss,6,MidpointRounding.AwayFromZero);
        }

        private static PuzzleDefinition Mutate(GeneratedPuzzle puzzle,PuzzleDifficultyReport measured,StableRandom random,DifficultySettings settings)
        {
            var paths=new List<ArrowPath>(puzzle.Paths); var gates=new List<ExitGate>(puzzle.Exits.Gates);
            int kind=random.Next(3),index=random.Next(paths.Count);
            if(settings.DependencyStrength>.7 && random.Chance(.6) && measured.SafePaths.Length>0)
            {
                kind=0;
                int id=measured.SafePaths[random.Next(measured.SafePaths.Length)];
                for(int i=0;i<paths.Count;i++) if(paths[i].Id==id) { index=i; break; }
            }
            var path=paths[index];
            if(kind==0)
            {
                var cells=new List<SurfaceCoordinate>(path.Cells); cells.Reverse();
                puzzle.Topology.TryGetDirection(cells[1],cells[0],out var incoming);
                var heading=puzzle.Topology.Step(cells[1],incoming).Direction;
                paths[index]=new ArrowPath(path.Id,puzzle.Topology,cells,heading);
            }
            else if(kind==1 && gates.Count>0)
            {
                int g=random.Next(gates.Count); var gate=gates[g]; var next=puzzle.Topology.Step(gate.Cell,gate.Direction);
                gates[g]=new ExitGate(next.Coordinate,next.Direction);
            }
            else
            {
                if(path.Cells.Count<3) return null;
                var cells=new List<SurfaceCoordinate>(path.Cells); cells.RemoveAt(cells.Count-1);
                var next=puzzle.Topology.Step(cells[cells.Count-1],(SurfaceDirection)random.Next(4)).Coordinate;
                var grid=puzzle.CreateGrid(); if(grid.TryGetCellOwner(next,out _)) return null;
                cells.Add(next); paths[index]=new ArrowPath(path.Id,puzzle.Topology,cells,path.HeadDirection);
            }
            return new PuzzleDefinition(puzzle.Name,puzzle.Topology,paths,new ExitMap(puzzle.Topology,gates),puzzle.Rules);
        }
        private static void Add(GenerationReport target,GenerationReport source)
        {
            target.CandidateAttempts+=source.CandidateAttempts; target.RejectedCandidates+=source.RejectedCandidates;
            target.ValidationFailures+=source.ValidationFailures; target.QualityRejections+=source.QualityRejections; target.Restarts+=source.Restarts;
        }
    }
}
