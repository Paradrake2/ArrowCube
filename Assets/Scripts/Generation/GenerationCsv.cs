using System.Globalization;

namespace ArrowCube.Generation
{
    public static class GenerationCsv
    {
        public const string Header="profile,mode,seed,version,generation_ms,paths,coverage,cross_face_paths,initial_safe,blocked_fraction,average_turns,dependencies,depth,edge_exits,gate_exits,gates,restarts,candidate_attempts,target_reached,validation_failures,fingerprint\n";
        public static string Row(GeneratedPuzzle puzzle)
        {
            var s=puzzle.Statistics; var r=puzzle.Report; var recipe=puzzle.Recipe;
            string mode=recipe.Rules.EnableCubeEdgeExits ? (recipe.Rules.EnableDirectionalGates ? "Hybrid" : "EdgeOnly") : "GateOnly";
            // Profile labels are arbitrary user input; quote/escape them for valid CSV.
            string name="\""+recipe.Profile.Name.Replace("\"","\"\"")+"\"";
            return string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4:F3},{5},{6:F6},{7},{8},{9:F6},{10:F3},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20}\n",
                name,mode,recipe.Seed,recipe.GenerationVersion,r.Milliseconds,s.PathCount,s.Coverage,s.CrossFacePaths,
                s.ImmediatelyRemovablePaths,s.BlockedFraction,s.AverageTurns,s.DependencyCount,s.DependencyDepth,
                s.EdgeExitsInSolution,s.GateExitsInSolution,puzzle.Exits.CellGates.Count,r.Restarts,r.CandidateAttempts,
                r.TargetCoverageReached,r.ValidationFailures,puzzle.Fingerprint);
        }
    }
}
