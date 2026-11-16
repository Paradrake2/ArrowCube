using System;
using System.Globalization;
using System.IO;
using System.Text;
using ArrowCube.Generation;
using ArrowCube.Movement;

internal static class DifficultyBenchmark
{
    internal static int Advanced(string[] args)
    {
        int seeds=args.Length>1 ? int.Parse(args[1],CultureInfo.InvariantCulture) : 70;
        string directory=args.Length>2 ? args[2] : "TestResults/AdvancedDifficulty/Benchmark";
        var quality=args.Length>3 ? (GenerationQuality)Enum.Parse(typeof(GenerationQuality),args[3]) : GenerationQuality.Balanced;
        Directory.CreateDirectory(directory);
        var csv=new StringBuilder("preset,mode,seed,grid,target,score,baseline_score,paths,baseline_paths,safe_fraction,depth,mean_depth,edge_failures_per_removal,baseline_edge_failures_per_removal,exit_failures_per_removal,outer_failures_per_removal,random_failures_per_removal,generation_ms,baseline_generation_ms,candidates,mutations,accepted_mutations,target_reached,normalized_edge_mistakes,baseline_normalized_edge_mistakes,validation_failures,baseline_exit_failures_per_removal,baseline_outer_failures_per_removal,baseline_random_failures_per_removal,rejected_proposals,rejected_mutations,fingerprint\n");
        var errors=new StringBuilder(); int accepted=0,failures=0;
        foreach(var preset in DifficultyPresets.All)
        for(int mode=0;mode<3;mode++)
        {
            int good=0; double score=0,edge=0,oldEdge=0;
            for(int seed=0;seed<seeds;seed++)
            {
                try
                {
                    var settings=preset.CreateLegacySettings(); settings.ApplyQuality(quality);
                    var profile=preset.CreateProfile(); var rules=Rules(mode);
                    var old=new PuzzleGenerator().Generate(new PuzzleRecipe(seed,profile,rules,generationVersion:2));
                    var oldReport=new PuzzleDifficultyAnalyzer().Analyze(old,seed,settings.Weights,alternativeOrderSamples:settings.AlternativeOrderSamples,analysisVersion:2);
                    var puzzle=new PuzzleGenerator().Generate(new PuzzleRecipe(seed,profile,rules,generationVersion:3,maxRestarts:settings.MaxRestarts,difficulty:settings));
                    var replay=new PuzzleValidator().Validate(puzzle,puzzle.VerifiedSolution);
                    if(!replay.Succeeded) throw new InvalidOperationException(replay.Error);
                    var r=puzzle.Difficulty; var g=puzzle.Report;
                    csv.AppendLine(FormattableString.Invariant($"{preset.Name},{mode},{seed},{profile.GridSize},{settings.TargetScore:F1},{r.MeasuredScore:F4},{oldReport.MeasuredScore:F4},{r.PathCount},{oldReport.PathCount},{r.InitialSafeFraction:F6},{r.LongestDependencyChain},{r.AverageDependencyDepth:F4},{r.Solvers[0].FailuresPerRemoval:F6},{oldReport.Solvers[0].FailuresPerRemoval:F6},{r.Solvers[1].FailuresPerRemoval:F6},{r.Solvers[2].FailuresPerRemoval:F6},{r.RandomFailuresPerRemovalMean:F6},{g.Milliseconds:F3},{old.Report.Milliseconds:F3},{g.LayoutCandidates},{g.OptimizationAttempts},{g.OptimizationAccepted},{r.TargetReached},{r.EdgeFirstNormalizedMistakeRate:F6},{oldReport.EdgeFirstNormalizedMistakeRate:F6},{g.ValidationFailures},{oldReport.Solvers[1].FailuresPerRemoval:F6},{oldReport.Solvers[2].FailuresPerRemoval:F6},{oldReport.RandomFailuresPerRemovalMean:F6},{g.RejectedCandidates},{g.OptimizationRejected},{puzzle.Fingerprint}"));
                    good++; accepted++; score+=r.MeasuredScore; edge+=r.Solvers[0].FailuresPerRemoval; oldEdge+=oldReport.Solvers[0].FailuresPerRemoval;
                    if(seeds<=3) Console.WriteLine(FormattableString.Invariant($"{preset.Name}/{mode}/{seed}: score={r.MeasuredScore:F2}, safe={r.InitialSafeFraction:P0}, depth={r.LongestDependencyChain}, mistakes={r.Solvers[0].FailuresPerRemoval:F2} vs {oldReport.Solvers[0].FailuresPerRemoval:F2}, {g.Milliseconds:F0}ms; {r.Warning}"));
                }
                catch(Exception error) { failures++; errors.AppendLine($"{preset.Name}/{mode}/{seed}: {error}"); Console.WriteLine(error.Message); }
            }
            Console.WriteLine(FormattableString.Invariant($"ADVANCED {preset.Name}/{mode}: {good}/{seeds}; score={score/Math.Max(1,good):F2}; edge mistakes/move={edge/Math.Max(1,good):F3} vs {oldEdge/Math.Max(1,good):F3}"));
            File.WriteAllText(Path.Combine(directory,"benchmark.csv"),csv.ToString()); File.WriteAllText(Path.Combine(directory,"errors.txt"),errors.ToString());
        }
        Console.WriteLine($"Advanced complete: {accepted}; failures={failures}"); return failures==0 ? 0 : 1;
    }
    internal static int Baseline(string[] args)
    {
        int seeds=args.Length>1 ? int.Parse(args[1],CultureInfo.InvariantCulture) : 56;
        string directory=args.Length>2 ? args[2] : "TestResults/AdvancedDifficulty/Baseline";
        Directory.CreateDirectory(directory);
        var csv=new StringBuilder("profile,mode,seed,version,score,paths,coverage,safe_fraction,depth,mean_depth,edge_failures_per_removal,exit_failures_per_removal,outer_failures_per_removal,random_failures_per_removal,correlation,generation_ms,analysis_ms,fingerprint\n");
        int total=0,failures=0;
        foreach(var profile in DifficultyProfiles.All)
        for(int mode=0;mode<3;mode++)
        {
            double score=0,edge=0; int accepted=0;
            for(int seed=0;seed<seeds;seed++)
            {
                try
                {
                    var puzzle=new PuzzleGenerator().Generate(new PuzzleRecipe(seed,profile,Rules(mode),generationVersion:2));
                    var watch=System.Diagnostics.Stopwatch.StartNew();
                    var r=new PuzzleDifficultyAnalyzer().Analyze(puzzle,seed);
                    double elapsed=watch.Elapsed.TotalMilliseconds;
                    csv.AppendLine(FormattableString.Invariant($"\"{profile.Name}\",{mode},{seed},2,{r.MeasuredScore:F4},{r.PathCount},{r.Coverage:F6},{r.InitialSafeFraction:F6},{r.LongestDependencyChain},{r.AverageDependencyDepth:F4},{r.Solvers[0].FailuresPerRemoval:F6},{r.Solvers[1].FailuresPerRemoval:F6},{r.Solvers[2].FailuresPerRemoval:F6},{r.Solvers[3].FailuresPerRemoval:F6},{r.EdgeRemovabilityCorrelation:F6},{puzzle.Report.Milliseconds:F3},{elapsed:F3},{puzzle.Fingerprint}"));
                    total++; accepted++; score+=r.MeasuredScore; edge+=r.Solvers[0].FailuresPerRemoval;
                }
                catch(Exception error) { failures++; Console.WriteLine(error.ToString()); }
            }
            Console.WriteLine(FormattableString.Invariant($"BASELINE {profile.Name}/{mode}: {accepted}/{seeds}, score={score/Math.Max(1,accepted):F2}, edge mistakes/move={edge/Math.Max(1,accepted):F3}"));
            File.WriteAllText(Path.Combine(directory,"baseline.csv"),csv.ToString());
        }
        Console.WriteLine($"Baseline complete: {total} puzzles; failures={failures}");
        return failures==0 ? 0 : 1;
    }
    internal static ExitRules Rules(int mode) => mode==0 ? ExitRules.EdgeOnly : mode==1 ? ExitRules.GateOnly : ExitRules.Hybrid;
}
