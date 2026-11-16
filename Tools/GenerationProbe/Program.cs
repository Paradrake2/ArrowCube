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
        if(args.Length>0 && args[0]=="--surface-demo") return SurfaceDemo(args);
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

    private static int SurfaceDemo(string[] args)
    {
        var puzzle=ManualSurfacePuzzle.Create(); var grid=puzzle.CreateGrid(); var simulator=new PathMovementSimulator();
        var report=new StringBuilder(); report.AppendLine(puzzle.Name);
        report.AppendLine($"Topology: {puzzle.Surface.GetType().Name}, {puzzle.Surface.CellCount} cells; cube coordinates are not used.");
        report.AppendLine("Initial attempts (no occupancy changes):");
        foreach(var path in puzzle.Paths)
        {
            var plan=simulator.Simulate(grid,path.Id,puzzle.Exits,puzzle.Rules);
            report.AppendLine($"Arrow {path.Id}: {plan.Outcome}; blocker={plan.BlockingPathId}; blocked cell={plan.BlockedCellId}; safe steps={plan.StepCount}");
        }
        var verified=new PuzzleValidator().Validate(puzzle,ManualSurfacePuzzle.RemovalOrder);
        if(!verified.Succeeded) throw new InvalidOperationException(verified.Error);
        report.AppendLine($"Verified dependencies: {verified.Statistics.DependencyCount}; depth: {verified.Statistics.DependencyDepth}; initially free: {verified.Statistics.ImmediatelyRemovablePaths}");
        foreach(int id in verified.Solution)
        {
            var plan=simulator.Simulate(grid,id,puzzle.Exits,puzzle.Rules);
            if(!grid.TryCommit(plan)) throw new InvalidOperationException("Verified removal failed: "+id);
            report.Append($"Remove {id}: {plan.ExitType}; steps={plan.StepCount}; head route:");
            for(int step=0;step<=plan.StepCount;step++)
            {
                var point=plan.GetCellPosition(step,0);
                report.Append(" "+point.CellId+(point.IsOnSurface ? "" : "+exit"+point.ExitDistance));
            }
            report.AppendLine();
        }
        report.AppendLine($"Complete: paths={grid.Paths.Count}, occupied cells={grid.OccupiedCellCount}, occupied segments={grid.OccupiedSegmentCount}");
        string output=args.Length>1 ? args[1] : "TestResults/SurfacePuzzle/demo.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))); File.WriteAllText(output,report.ToString()); Console.Write(report);
        return grid.Paths.Count==0 && grid.OccupiedCellCount==0 && grid.OccupiedSegmentCount==0 ? 0 : 1;
    }
}
