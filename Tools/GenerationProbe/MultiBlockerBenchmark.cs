using System;
using System.IO;
using System.Text;
using ArrowCube.Generation;
using ArrowCube.Movement;

internal static class MultiBlockerBenchmark
{
    public static int Run(string[] args)
    {
        int seeds=args.Length>1 ? int.Parse(args[1]) : 70;
        string directory=args.Length>2 ? args[2] : "TestResults/MultiBlocker/Benchmark";
        var quality=args.Length>3 ? (GenerationQuality)Enum.Parse(typeof(GenerationQuality),args[3]) : GenerationQuality.Balanced;
        bool control=args.Length>4 && args[4]=="disabled";
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"comparison.txt"),control ? "Version 4 feature disabled versus enabled, with identical settings and search budgets." : "Version 3 original preset defaults versus version 4 multi-blocker presets; identical seed, geometry bounds, density targets and exit rules.");
        var csv=new StringBuilder("preset,mode,seed,grid,old_paths,new_paths,old_coverage,new_coverage,old_average_blockers,new_average_blockers,old_blockers_per_blocked,new_blockers_per_blocked,old_multi_fraction,new_multi_fraction,old_independent_multi,new_independent_multi,old_three_fraction,new_three_fraction,old_depth,new_depth,old_cross_geometry,new_cross_geometry,old_safe,new_safe,old_concentration,new_concentration,old_edge_failures,new_edge_failures,old_normalized_edge,new_normalized_edge,old_ms,new_ms,targeted_attempts,targeted_accepted,validation_failures,old_independent_three,new_independent_three,old_random_failures,new_random_failures,old_edge_correlation,new_edge_correlation,old_fingerprint,new_fingerprint\n");
        var errors=new StringBuilder(); int failures=0,total=0;
        foreach(var preset in DifficultyPresets.All)
        for(int mode=0;mode<3;mode++)
        {
            double oldMulti=0,newMulti=0,oldMistakes=0,newMistakes=0; int accepted=0;
            for(int seed=0;seed<seeds;seed++)
            {
                try
                {
                    var settings=preset.CreateSettings(); settings.ApplyQuality(quality); var rules=DifficultyBenchmark.Rules(mode);
                    var profile=preset.CreateProfile();
                    var oldSettings=control ? settings.Copy() : preset.CreateLegacySettings();
                    if(control) oldSettings.MultiBlocker.Enabled=false; else oldSettings.ApplyQuality(quality);
                    var old=new PuzzleGenerator().Generate(new PuzzleRecipe(seed,profile,rules,generationVersion:control ? 4 : 3,maxRestarts:oldSettings.MaxRestarts,difficulty:oldSettings));
                    var oldReport=new PuzzleDifficultyAnalyzer().Analyze(old,seed,settings.Weights);
                    var puzzle=new PuzzleGenerator().Generate(new PuzzleRecipe(seed,profile,rules,maxRestarts:settings.MaxRestarts,difficulty:settings));
                    if(!new PuzzleValidator().Validate(puzzle,puzzle.VerifiedSolution).Succeeded) throw new InvalidOperationException("Final replay failed.");
                    var a=oldReport; var b=puzzle.Difficulty; var am=a.MultiBlocker; var bm=b.MultiBlocker;
                    csv.AppendLine(FormattableString.Invariant($"{preset.Name},{mode},{seed},{profile.GridSize},{a.PathCount},{b.PathCount},{a.Coverage:F6},{b.Coverage:F6},{am.AverageBlockersPerPath:F6},{bm.AverageBlockersPerPath:F6},{am.AverageBlockersPerBlockedPath:F6},{bm.AverageBlockersPerBlockedPath:F6},{am.AtLeastTwoFraction:F6},{bm.AtLeastTwoFraction:F6},{am.IndependentMultiBlockerFraction:F6},{bm.IndependentMultiBlockerFraction:F6},{am.AtLeastThreeFraction:F6},{bm.AtLeastThreeFraction:F6},{a.LongestDependencyChain},{b.LongestDependencyChain},{am.CrossFaceGeometryFraction:F6},{bm.CrossFaceGeometryFraction:F6},{a.InitialSafeFraction:F6},{b.InitialSafeFraction:F6},{am.MaxBlockerConcentration:F6},{bm.MaxBlockerConcentration:F6},{a.Solvers[0].FailuresPerRemoval:F6},{b.Solvers[0].FailuresPerRemoval:F6},{a.EdgeFirstNormalizedMistakeRate:F6},{b.EdgeFirstNormalizedMistakeRate:F6},{old.Report.Milliseconds:F3},{puzzle.Report.Milliseconds:F3},{puzzle.Report.TargetedAttempts},{puzzle.Report.TargetedAccepted},{puzzle.Report.ValidationFailures},{am.IndependentThreeBlockerFraction:F6},{bm.IndependentThreeBlockerFraction:F6},{a.RandomFailuresPerRemovalMean:F6},{b.RandomFailuresPerRemovalMean:F6},{a.EdgeRemovabilityCorrelation:F6},{b.EdgeRemovabilityCorrelation:F6},{old.Fingerprint},{puzzle.Fingerprint}"));
                    oldMulti+=am.IndependentMultiBlockerFraction; newMulti+=bm.IndependentMultiBlockerFraction; oldMistakes+=a.Solvers[0].FailuresPerRemoval; newMistakes+=b.Solvers[0].FailuresPerRemoval;
                    total++; accepted++;
                    if(seeds<=3) Console.WriteLine(FormattableString.Invariant($"{preset.Name}/{mode}/{seed}: independent multi {am.IndependentMultiBlockerFraction:P0}->{bm.IndependentMultiBlockerFraction:P0}; edge {a.Solvers[0].FailuresPerRemoval:F2}->{b.Solvers[0].FailuresPerRemoval:F2}; targeted {puzzle.Report.TargetedAccepted}; {puzzle.Report.Milliseconds:F0}ms"));
                }
                catch(Exception error) { failures++; errors.AppendLine($"{preset.Name}/{mode}/{seed}: {error}"); Console.WriteLine(error.Message); }
            }
            Console.WriteLine(FormattableString.Invariant($"MULTI {preset.Name}/{mode}: {accepted}/{seeds}; independent {oldMulti/Math.Max(1,accepted):P1}->{newMulti/Math.Max(1,accepted):P1}; edge {oldMistakes/Math.Max(1,accepted):F2}->{newMistakes/Math.Max(1,accepted):F2}"));
            File.WriteAllText(Path.Combine(directory,"benchmark.csv"),csv.ToString()); File.WriteAllText(Path.Combine(directory,"errors.txt"),errors.ToString());
        }
        Console.WriteLine($"Multi-blocker benchmark complete: {total}; failures={failures}"); return failures==0 ? 0 : 1;
    }
}
