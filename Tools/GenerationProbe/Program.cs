using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Text;
using ArrowCube.Generation;
using ArrowCube.Movement;

internal static class Program
{
    private static int Main(string[] args)
    {
        if(args.Length>0 && args[0]=="--dependency-controls") return OpeningControlProbe.Run(args);
        if(args.Length>0 && args[0]=="--difficulty-baseline") return DifficultyBenchmark.Baseline(args);
        if(args.Length>0 && args[0]=="--multi-blocker-benchmark") return MultiBlockerBenchmark.Run(args);
        if(args.Length>0 && args[0]=="--difficulty-benchmark") return DifficultyBenchmark.Advanced(args);
        int count=args.Length>0 ? int.Parse(args[0],CultureInfo.InvariantCulture) : 1;
        string output=args.Length>1 ? args[1] : "TestResults/stage3-generation.csv";
        var rules=new[] { ExitRules.EdgeOnly,ExitRules.GateOnly,ExitRules.Hybrid };
        var modes=new[] { "EdgeOnly","GateOnly","Hybrid" };
        var csv=new StringBuilder(GenerationCsv.Header);
        int total=0,failures=0;
        var profiles=new List<DifficultyProfile>(DifficultyProfiles.All);
        // Optional comma-separated sizes exercise the default custom asset settings.
        if(args.Length>2)
        {
            profiles.Clear();
            foreach(string size in args[2].Split(','))
            {
                int gridSize=int.Parse(size,CultureInfo.InvariantCulture);
                int gateBudget=(int)Math.Ceiling(48*Math.Max(1,gridSize*(double)gridSize/64));
                profiles.Add(new DifficultyProfile("Custom "+size,gridSize,
                    2,14,14,8,.60f,.78f,.20f,1,.55f,gateBudget));
            }
        }
        foreach(var profile in profiles)
        for(int mode=0;mode<rules.Length;mode++)
        {
            double time=0,coverage=0,safe=0; int rejected=0;
            for(int seed=0;seed<count;seed++)
            {
                try
                {
                    var puzzle=new PuzzleGenerator().Generate(new PuzzleRecipe(seed,profile,rules[mode]));
                    var replay=new PuzzleValidator().Validate(puzzle,puzzle.VerifiedSolution);
                    if(!replay.Succeeded) throw new InvalidOperationException(replay.Error);
                    var s=puzzle.Statistics; var r=puzzle.Report;
                    csv.Append(GenerationCsv.Row(puzzle));
                    total++; time+=r.Milliseconds; coverage+=s.Coverage; safe+=s.ImmediatelyRemovablePaths;
                    if(count<=3) Console.WriteLine(FormattableString.Invariant($"{profile.Name} {modes[mode]} seed={seed}: {s.PathCount} paths, {s.Coverage:P1} coverage, {s.CrossFacePaths} cross, {s.BlockedFraction:P0} blocked, {s.AverageTurns:F1} turns, {r.Milliseconds:F1}ms, restart={r.Restarts}"));
                }
                catch(PuzzleGenerationException error) { failures++; rejected++; Console.WriteLine(error.Message); }
            }
            Console.WriteLine(FormattableString.Invariant($"SUMMARY {profile.Name}/{modes[mode]}: {count-rejected}/{count}, mean_ms={time/Math.Max(1,count-rejected):F1}, mean_coverage={coverage/Math.Max(1,count-rejected):P1}, mean_safe={safe/Math.Max(1,count-rejected):F1}"));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        File.WriteAllText(output,csv.ToString());
        Console.WriteLine($"Validated {total} puzzles; failures={failures}; CSV={Path.GetFullPath(output)}");
        return failures==0 ? 0 : 1;
    }
}
