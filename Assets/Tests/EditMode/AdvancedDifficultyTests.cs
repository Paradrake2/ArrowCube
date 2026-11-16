using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using ArrowCube.Topology;
using NUnit.Framework;
using UnityEngine;

namespace ArrowCube.Tests
{
    public class AdvancedDifficultyTests
    {
        private static PuzzleDefinition Interleaved()
        {
            var t=new CubeTopology(4);
            var paths=new[] {
                new ArrowPath(0,t,new[] { new SurfaceCoordinate(CubeFace.Front,0,1),new SurfaceCoordinate(CubeFace.Left,3,1) },SurfaceDirection.Right),
                new ArrowPath(1,t,new[] { new SurfaceCoordinate(CubeFace.Right,1,1),new SurfaceCoordinate(CubeFace.Right,0,1),new SurfaceCoordinate(CubeFace.Front,3,1) },SurfaceDirection.Right),
                new ArrowPath(2,t,new[] { new SurfaceCoordinate(CubeFace.Front,2,3),new SurfaceCoordinate(CubeFace.Front,2,2) },SurfaceDirection.Up) };
            return new PuzzleDefinition("Interior blocker of edge path",t,paths,new ExitMap(t,Array.Empty<ExitGate>()),ExitRules.EdgeOnly);
        }
        [Test]
        public void HeuristicsUseObservedAttemptsAndNeverRepeatABlockedPathWithoutAStateChange()
        {
            var puzzle=Interleaved(); var simulator=new PathMovementSimulator();
            foreach(HeuristicStrategy strategy in Enum.GetValues(typeof(HeuristicStrategy)))
            {
                var report=new HeuristicPuzzleSolver().Solve(puzzle,strategy,17);
                Assert.That(report.Completed, Is.True); Assert.That(report.SuccessfulRemovals, Is.EqualTo(3));
                var grid=puzzle.CreateGrid(); var failedThisState=new HashSet<int>(); int failures=0;
                foreach(int id in report.AttemptedPaths)
                {
                    Assert.That(failedThisState.Contains(id), Is.False);
                    var plan=simulator.Simulate(grid,id,puzzle.Exits,puzzle.Rules);
                    if(plan.Succeeded) { Assert.That(grid.TryCommit(plan), Is.True); failedThisState.Clear(); }
                    else { failures++; failedThisState.Add(id); }
                }
                Assert.That(report.FailedAttempts, Is.EqualTo(failures));
                Assert.That(report.TotalAttempts, Is.EqualTo(report.SuccessfulRemovals+report.FailedAttempts));
                Assert.That(new HeuristicPuzzleSolver().Solve(puzzle,strategy,17).AttemptedPaths, Is.EqualTo(report.AttemptedPaths));
                if(strategy==HeuristicStrategy.NearestEdge) Assert.That(failures, Is.GreaterThan(0));
            }
            Assert.That(puzzle.CreateGrid().Paths.Count, Is.EqualTo(3));
        }
        [Test]
        public void DependencyAnalysisFindsNecessaryCrossFaceBlockersAndSafeInteriorHeads()
        {
            var puzzle=Interleaved(); var r=new PuzzleDifficultyAnalyzer().Analyze(puzzle);
            Assert.That(r.InitialSafe, Is.EqualTo(2)); Assert.That(r.InitialBlocked, Is.EqualTo(1));
            Assert.That(r.SafePaths, Does.Contain(1)); Assert.That(r.BlockedPaths, Does.Contain(0));
            Assert.That(r.CrossFacePaths, Is.EqualTo(2)); Assert.That(r.Dependencies.Length, Is.EqualTo(1));
            Assert.That(r.Dependencies[0].Path, Is.EqualTo(0)); Assert.That(r.Dependencies[0].Blocker, Is.EqualTo(1));
            Assert.That(r.Dependencies[0].CrossFace, Is.True);
            Assert.That(r.DependencyLayers, Is.EqualTo(2)); Assert.That(r.AverageDependencyDepth, Is.EqualTo(4.0/3).Within(1e-9));
            Assert.That(r.ChoiceSamples[0].Safe, Is.EqualTo(2)); Assert.That(r.AllSafeOrdersEquivalent, Is.True);
            Assert.That(r.EdgeRemovabilityCorrelation, Is.GreaterThan(0),"The interior head is safe while an edge head is blocked.");
            Assert.That(r.LengthHistogram.Sum(), Is.EqualTo(3)); Assert.That(r.TurnHistogram.Sum(), Is.EqualTo(3));
        }
        [Test]
        public void EveryReportedDependencyIsNecessaryUnderTheActualFixedRouteRules()
        {
            var preset=DifficultyPresets.All[2]; var settings=preset.CreateSettings(); settings.ApplyQuality(GenerationQuality.Fast);
            var puzzle=new PuzzleGenerator().Generate(new PuzzleRecipe(12,preset.CreateProfile(),difficulty:settings));
            var paths=puzzle.Paths.ToDictionary(path=>path.Id); var simulator=new PathMovementSimulator();
            foreach(var link in puzzle.Difficulty.Dependencies)
            {
                var grid=new SurfaceGrid(puzzle.Topology,new[] { paths[link.Path],paths[link.Blocker] });
                Assert.That(simulator.Simulate(grid,link.Path,puzzle.Exits,puzzle.Rules).Succeeded, Is.False);
                grid.RemovePath(link.Blocker);
                Assert.That(simulator.Simulate(grid,link.Path,puzzle.Exits,puzzle.Rules).Succeeded, Is.True);
            }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void AdvancedGenerationIsDeterministicVerifiedAndPreservesAllExitModes(int mode)
        {
            var preset=DifficultyPresets.All[2]; var settings=preset.CreateSettings(); settings.ApplyQuality(GenerationQuality.Fast);
            var recipe=new PuzzleRecipe(19,preset.CreateProfile(),PuzzleGeneratorTests.Rules(mode),difficulty:settings);
            var a=new PuzzleGenerator().Generate(recipe); var b=new PuzzleGenerator().Generate(recipe);
            Assert.That(b.Fingerprint, Is.EqualTo(a.Fingerprint));
            Assert.That(b.Difficulty.MeasuredScore, Is.EqualTo(a.Difficulty.MeasuredScore));
            Assert.That(b.Difficulty.Solvers[0].AttemptedPaths, Is.EqualTo(a.Difficulty.Solvers[0].AttemptedPaths));
            Assert.That(new PuzzleValidator().Validate(a,a.VerifiedSolution).Succeeded, Is.True);
            Assert.That(PuzzleGenerator.QualityError(recipe,a.Statistics), Is.Empty);
            Assert.That(a.Statistics.ConnectedFaces, Is.EqualTo(6));
            if(mode==0) Assert.That(a.Exits.Gates, Is.Empty);
            if(mode==1) Assert.That(a.Statistics.EdgeExitsInSolution, Is.Zero);
            if(mode==2) { Assert.That(a.Statistics.GateExitsInSolution, Is.GreaterThan(0)); Assert.That(a.Statistics.EdgeExitsInSolution, Is.GreaterThan(0)); }
        }
        [Test]
        public void SearchAndMutationBudgetsAreEnforcedAndTheBestCandidateSurvivesRejectedMutations()
        {
            var preset=DifficultyPresets.All[3]; var settings=preset.CreateSettings();
            settings.MaxCandidates=2; settings.MaxRestarts=2; settings.MaxConstructionAttempts=6000; settings.OptimizationIterations=20;
            var recipe=new PuzzleRecipe(2,preset.CreateProfile(),maxRestarts:2,difficulty:settings);
            var puzzle=new PuzzleGenerator().Generate(recipe);
            Assert.That(puzzle.Report.LayoutCandidates, Is.EqualTo(2)); Assert.That(puzzle.Report.OptimizationAttempts, Is.EqualTo(20));
            Assert.That(puzzle.Report.CandidateAttempts, Is.LessThanOrEqualTo(2*2*6000));
            Assert.That(puzzle.Report.OptimizationRejected, Is.GreaterThan(0));
            Assert.That(new PuzzleValidator().Validate(puzzle,puzzle.VerifiedSolution).Succeeded, Is.True);
        }
        [Test]
        public void SettingsSnapshotsCustomCountsWeightsAndTargetMissesAreExplicit()
        {
            var preset=DifficultyPresets.All[1]; var settings=preset.CreateSettings(); settings.ApplyQuality(GenerationQuality.Fast);
            settings.TargetScore=100; settings.ScoreTolerance=0;
            var recipe=new PuzzleRecipe(3,preset.CreateProfile(),difficulty:settings);
            settings.TargetScore=0; settings.Weights.Dependencies=0; recipe.Difficulty.TargetScore=0;
            var puzzle=new PuzzleGenerator().Generate(recipe);
            Assert.That(puzzle.Difficulty.RequestedScore, Is.EqualTo(100));
            Assert.That(puzzle.Difficulty.TargetReached, Is.False); Assert.That(puzzle.Difficulty.Warning, Does.Contain("not reached"));
            Assert.That(recipe.Difficulty.Weights.Dependencies, Is.EqualTo(25));
            Assert.That(puzzle.Statistics.PathCount, Is.LessThanOrEqualTo(recipe.Profile.MaxPathCount));
            var weights=new DifficultyWeights { EdgeResistance=0,Dependencies=0,SafeMoveScarcity=1,SpatialUnpredictability=0,CrossFace=0,VisualComplexity=0 };
            var r=new PuzzleDifficultyAnalyzer().Analyze(puzzle,weights:weights);
            Assert.That(r.MeasuredScore, Is.EqualTo(Math.Round(100*(1-r.InitialSafeFraction),6,MidpointRounding.AwayFromZero)).Within(1e-9));
        }
        [Test]
        public void CancellationStopsConstructionAndAnalysisAndLegacyRecipesRemainAvailable()
        {
            using(var source=new CancellationTokenSource())
            {
                source.Cancel(); var preset=DifficultyPresets.All[1];
                Assert.Throws<OperationCanceledException>(()=>new PuzzleGenerator().Generate(new PuzzleRecipe(1,preset.CreateProfile(),difficulty:preset.CreateSettings()),source.Token));
                Assert.Throws<OperationCanceledException>(()=>new PuzzleDifficultyAnalyzer().Analyze(Interleaved(),cancellationToken:source.Token));
            }
            Assert.Throws<ArgumentException>(()=>new PuzzleRecipe(1,DifficultyProfiles.All[0],generationVersion:2,difficulty:new DifficultySettings()));
            var old=new PuzzleGenerator().Generate(new PuzzleRecipe(0,DifficultyProfiles.All[0],ExitRules.Hybrid,generationVersion:1));
            Assert.That(old.Fingerprint, Is.EqualTo("f26fc7b76e70e945c0d490c086884036ce043301900a2b79a710206d6f3f5dbe"));
            var v2=new PuzzleGenerator().Generate(new PuzzleRecipe(0,DifficultyProfiles.All[0],ExitRules.EdgeOnly,generationVersion:2));
            Assert.That(v2.Fingerprint, Is.EqualTo("bfd25cafcef6edfdc195a5dba9aeb67b3a1e56ee422ddf2af1c88b15289d1ee1"));
        }
        [Test]
        public void PresetsChangeLogicalParametersWithoutNeedingLargerGridsAndScoresFollowTheTiers()
        {
            Assert.That(DifficultyPresets.All[2].CreateProfile().GridSize, Is.EqualTo(DifficultyPresets.All[4].CreateProfile().GridSize));
            var sums=new double[5];
            for(int preset=0;preset<5;preset++)
            for(int seed=0;seed<4;seed++)
            {
                var p=DifficultyPresets.All[preset]; var settings=p.CreateSettings(); settings.MaxCandidates=2; settings.OptimizationIterations=1;
                sums[preset]+=new PuzzleGenerator().Generate(new PuzzleRecipe(seed,p.CreateProfile(),difficulty:settings)).Difficulty.MeasuredScore;
            }
            Assert.That(sums[2], Is.GreaterThan(sums[0]+40)); Assert.That(sums[4], Is.GreaterThan(sums[1]+40));
        }
        [Test]
        public void UnityMatchesTheAdvancedDotnetGoldenAndEveryPlacementStrategyRespectsACustomPathCap()
        {
            var preset=DifficultyPresets.All[1];
            var golden=new PuzzleGenerator().Generate(new PuzzleRecipe(0,preset.CreateProfile(),generationVersion:3,difficulty:preset.CreateLegacySettings()));
            Assert.That(golden.Fingerprint, Is.EqualTo("f7f200b853427713c600499e1836b8e4442d31606d7608c1768b1bfd15b5c111"));
            var profile=new DifficultyProfile("Capped",6,2,12,8,5,.35,.55,0,.2,.55,72,maxPathCount:15);
            foreach(PlacementStrategy placement in Enum.GetValues(typeof(PlacementStrategy)))
            {
                var settings=DifficultyPresets.All[1].CreateSettings(); settings.ApplyQuality(GenerationQuality.Fast); settings.Placement=placement;
                var p=new PuzzleGenerator().Generate(new PuzzleRecipe(7,profile,difficulty:settings));
                Assert.That(p.Paths.Count, Is.InRange(8,15)); Assert.That(p.Recipe.Difficulty.Placement, Is.EqualTo(placement));
                Assert.That(new PuzzleValidator().Validate(p,p.VerifiedSolution).Succeeded, Is.True);
            }
        }
        [Test]
        public void EnlargingTheGridDoesNotAutomaticallyRaiseTheScore()
        {
            double[] scores=new double[2];
            for(int i=0;i<2;i++)
            {
                int size=i==0 ? 4 : 16; var t=new CubeTopology(size);
                var path=new ArrowPath(1,t,new[] { new SurfaceCoordinate(CubeFace.Front,size-1,0),new SurfaceCoordinate(CubeFace.Front,size-2,0) },SurfaceDirection.Right);
                scores[i]=new PuzzleDifficultyAnalyzer().Analyze(new PuzzleDefinition("Independent",t,new[] { path },new ExitMap(t,Array.Empty<ExitGate>()),ExitRules.EdgeOnly)).MeasuredScore;
            }
            Assert.That(scores[1], Is.LessThanOrEqualTo(scores[0]));
        }
        [Test]
        public void AssetMasterAndIndividualCustomEditsAreIndependentAndPresetResetRestoresValues()
        {
            var asset=ScriptableObject.CreateInstance<DifficultyProfileAsset>();
            try
            {
                asset.ApplyPreset(2); int size=asset.gridSize; float coverage=asset.targetCoverage;
                asset.difficultySettings.TargetScore=63;
                Assert.That(asset.gridSize, Is.EqualTo(size)); Assert.That(asset.targetCoverage, Is.EqualTo(coverage));
                asset.ApplyMasterDifficulty(90); Assert.That(asset.difficultySettings.DependencyStrength, Is.GreaterThan(.9));
                asset.ApplyPreset(2); Assert.That(asset.difficultySettings.TargetScore, Is.EqualTo(50)); Assert.That(asset.targetCoverage, Is.EqualTo(coverage));
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }
    }
}
