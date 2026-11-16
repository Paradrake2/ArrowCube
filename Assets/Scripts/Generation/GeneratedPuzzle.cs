using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ArrowCube.Movement;
using ArrowCube.Topology;

namespace ArrowCube.Generation
{
    public sealed class GenerationReport
    {
        public double Milliseconds { get; internal set; }
        public int Restarts { get; internal set; }
        public int CandidateAttempts { get; internal set; }
        public int RejectedCandidates { get; internal set; }
        public int ValidationFailures { get; internal set; }
        public int QualityRejections { get; internal set; }
        public bool TargetCoverageReached { get; internal set; }
        public int LayoutCandidates, OptimizationAttempts, OptimizationAccepted;
        public int OptimizationRejected;
        public int TargetedAttempts,TargetedCandidateAttempts,TargetedAccepted;
    }

    public sealed class GeneratedPuzzle : PuzzleDefinition
    {
        public PuzzleRecipe Recipe { get; }
        public IReadOnlyList<int> VerifiedSolution { get; }
        public PuzzleStatistics Statistics { get; }
        public GenerationReport Report { get; }
        public string Fingerprint { get; }
        public PuzzleDifficultyReport Difficulty { get; internal set; }
        public PuzzleDependencyGraph DependencyGraph { get; internal set; }
        public DependencyControlReport DependencyControlReport { get; internal set; }
        internal GeneratedPuzzle(PuzzleRecipe recipe,PuzzleDefinition puzzle,PuzzleValidation validation,GenerationReport report)
            : base(puzzle.Name,puzzle.Topology,puzzle.Paths,puzzle.Exits,puzzle.Rules)
        {
            Recipe=recipe; VerifiedSolution=validation.Solution; Statistics=validation.Statistics; Report=report;
            Fingerprint=ComputeFingerprint();
        }

        private string ComputeFingerprint()
        {
            using(var stream=new MemoryStream())
            {
                using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
                {
                    writer.Write(Recipe.GenerationVersion); writer.Write(Recipe.Seed);
                    writer.Write(Recipe.MaxRestarts); writer.Write(Recipe.AttemptsPerCell);
                    var p=Recipe.Profile;
                    writer.Write(p.Name); writer.Write(p.GridSize); writer.Write(p.MinPathLength); writer.Write(p.MaxPathLength);
                    writer.Write(p.MinPathCount); writer.Write(p.MinCrossFacePaths); writer.Write(p.MinimumCoverage);
                    writer.Write(p.TargetCoverage); writer.Write(p.MinimumBlockedFraction); writer.Write(p.MinimumAverageTurns);
                    writer.Write(p.TurnProbability); writer.Write(p.MaxGates);
                    if(Recipe.GenerationVersion>=3)
                    {
                        writer.Write(p.MaxPathCount); writer.Write(Recipe.DifficultySettings!=null);
                        Recipe.DifficultySettings?.Write(writer,Recipe.GenerationVersion);
                    }
                    writer.Write(Rules.EnableCubeEdgeExits); writer.Write(Rules.EnableDirectionalGates);
                    if(Recipe.GenerationVersion>=5)
                    { writer.Write(Recipe.DependencyControlSettings!=null); Recipe.DependencyControlSettings?.Write(writer); }
                    writer.Write(Exits.Gates.Count);
                    foreach(var gate in Exits.Gates) { Cell(gate.Cell); writer.Write((int)gate.Direction); }
                    writer.Write(Paths.Count);
                    foreach(var path in Paths)
                    {
                        writer.Write(path.Id); writer.Write((int)path.HeadDirection); writer.Write(path.Cells.Count);
                        foreach(var cell in path.Cells) Cell(cell);
                    }
                    writer.Write(VerifiedSolution.Count);
                    foreach(int id in VerifiedSolution) writer.Write(id);
                    void Cell(SurfaceCoordinate cell) { writer.Write((int)cell.Face); writer.Write(cell.X); writer.Write(cell.Y); }
                }
                using(var sha=SHA256.Create())
                {
                    var digest=sha.ComputeHash(stream.ToArray());
                    var result=new StringBuilder(64);
                    foreach(byte value in digest) result.Append(value.ToString("x2",System.Globalization.CultureInfo.InvariantCulture));
                    return result.ToString();
                }
            }
        }
    }

    public sealed class PuzzleGenerationException : Exception
    {
        public PuzzleRecipe Recipe { get; }
        public GenerationReport Report { get; }
        internal PuzzleGenerationException(PuzzleRecipe recipe,GenerationReport report,string reason)
            : base($"Generation rejected seed {recipe.Seed}, version {recipe.GenerationVersion}, {recipe.Profile.Name}: {reason}")
        { Recipe=recipe; Report=report; }
    }
}
