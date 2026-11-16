using System;
using System.IO;
using System.Text;
using ArrowCube.Generation;
using ArrowCube.Movement;

internal static class OpeningControlProbe
{
    public static int Run(string[] args)
    {
        int count=args.Length>1 ? int.Parse(args[1]) : 3;
        string output=args.Length>2 ? args[2] : "TestResults/OpeningControls/probe.csv";
        int size=args.Length>3 ? int.Parse(args[3]) : 8;
        int cap=args.Length>4 ? int.Parse(args[4]) : 3;
        bool measured=args.Length>5 && args[5]=="measured";
        var profile=new DifficultyProfile("Custom",size,2,14,14,8,.60f,.78f,.20f,1,.55f,(int)Math.Ceiling(48*Math.Max(1,size*(double)size/64)));
        var csv=new StringBuilder("mode,seed,amount,paths,free,average_blockers,baseline_free,flips,endpoints,milliseconds,fingerprint\n"); int failures=0;
        for(int mode=0;mode<3;mode++)
        for(int seed=0;seed<count;seed++)
        {
            var baseline=new PuzzleGenerator().Generate(new PuzzleRecipe(seed,profile,DifficultyBenchmark.Rules(mode)));
            foreach(double amount in new[] { 0.0,.75,1.0 })
            {
                try
                {
                    var difficulty=measured ? new DifficultySettings { MaxCandidates=2,OptimizationIterations=4,ScoreTolerance=0 } : null;
                    if(difficulty!=null) difficulty.MultiBlocker.TargetedBlockerAttempts=4;
                    var p=new PuzzleGenerator().Generate(new PuzzleRecipe(seed,profile,DifficultyBenchmark.Rules(mode),difficulty:difficulty,
                        dependencyControls:new DependencyControlSettings { Amount=amount,MaximumStartingFreeArrows=cap }));
                    var r=p.DependencyControlReport;
                    Console.WriteLine(FormattableString.Invariant($"mode={mode} seed={seed} amount={amount}: free {baseline.Statistics.ImmediatelyRemovablePaths}->{r.InitiallyFree}, blockers={r.AverageBlockersPerBlockedArrow:F2}, coverage={p.Statistics.Coverage:P1}, restarts={p.Report.Restarts}, flips={r.OrientationChanges}, ends={r.EndpointChanges}, {p.Report.Milliseconds:F0}ms"));
                    if(!new PuzzleValidator().Validate(p,p.VerifiedSolution,requireClearInitialExit:true).Succeeded) throw new Exception("Replay/clearance failed");
                    csv.AppendLine(FormattableString.Invariant($"{mode},{seed},{amount},{p.Paths.Count},{r.InitiallyFree},{r.AverageBlockersPerBlockedArrow:F6},{baseline.Statistics.ImmediatelyRemovablePaths},{r.OrientationChanges},{r.EndpointChanges},{p.Report.Milliseconds:F3},{p.Fingerprint}"));
                }
                catch(Exception error) { failures++; Console.WriteLine(error.Message); }
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))); File.WriteAllText(output,csv.ToString());
        return failures==0 ? 0 : 1;
    }
}
