using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Topology;
using ArrowCube.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace ArrowCube.Tests
{
    public class PuzzleGeneratorTests
    {
        public static IEnumerable<TestCaseData> ProfilesAndModes()
        {
            for(int profile=0;profile<DifficultyProfiles.All.Count;profile++)
            for(int mode=0;mode<3;mode++)
                yield return new TestCaseData(profile,mode).SetName($"GeneratesVerified_{DifficultyProfiles.All[profile].Name}_{mode}");
        }
        internal static ExitRules Rules(int mode) => mode==0 ? ExitRules.EdgeOnly : mode==1 ? ExitRules.GateOnly : ExitRules.Hybrid;

        [TestCaseSource(nameof(ProfilesAndModes))]
        public void EveryDifficultyAndExitModeHasConnectedCrossFaceGeometryAndAVerifiedSolution(int profile,int mode)
        {
            var recipe=new PuzzleRecipe(19,DifficultyProfiles.All[profile],Rules(mode));
            var p=new PuzzleGenerator().Generate(recipe);
            Assert.That(PuzzleGenerator.QualityError(recipe,p.Statistics), Is.Empty);
            Assert.That(p.Statistics.ConnectedFaces, Is.EqualTo(6));
            Assert.That(p.Statistics.CrossFacePaths, Is.GreaterThanOrEqualTo(recipe.Profile.MinCrossFacePaths));
            Assert.That(p.Statistics.DependencyCount, Is.GreaterThan(0));
            Assert.That(p.VerifiedSolution, Is.EquivalentTo(p.Paths.Select(path=>path.Id)));
            Assert.That(p.Paths.Select(path=>path.Cells.Count).Distinct().Count(), Is.GreaterThan(1));
            Assert.That(p.Paths.Select(path=>path.HeadDirection).Distinct().Count(), Is.GreaterThan(1));
            Assert.That(p.Paths.All(path=>path.Cells.Count>=recipe.Profile.MinPathLength && path.Cells.Count<=recipe.Profile.MaxPathLength), Is.True);
            Assert.That(p.Report.ValidationFailures, Is.Zero);
            Assert.That(new PuzzleValidator().Validate(p,p.VerifiedSolution).Succeeded, Is.True);
            var greedy=new PuzzleValidator().Validate(p);
            Assert.That(greedy.Succeeded, Is.True,greedy.Error);
            if(mode==0) { Assert.That(p.Exits.Gates, Is.Empty); Assert.That(p.Statistics.GateExitsInSolution, Is.Zero); }
            if(mode==1) Assert.That(p.Statistics.EdgeExitsInSolution, Is.Zero);
            if(mode==2)
            { Assert.That(p.Statistics.EdgeExitsInSolution, Is.GreaterThan(0)); Assert.That(p.Statistics.GateExitsInSolution, Is.GreaterThan(0)); }
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(int.MinValue)] [TestCase(int.MaxValue)]
        public void SeedRecipeAndVersionReproduceExactGeometryGatesRulesAndSolution(int seed)
        {
            var recipe=new PuzzleRecipe(seed,DifficultyProfiles.All[2],ExitRules.Hybrid);
            var a=new PuzzleGenerator().Generate(recipe);
            var b=new PuzzleGenerator().Generate(recipe);
            Assert.That(b.Fingerprint, Is.EqualTo(a.Fingerprint));
            Assert.That(b.VerifiedSolution, Is.EqualTo(a.VerifiedSolution));
            Assert.That(b.Exits.Gates, Is.EqualTo(a.Exits.Gates));
            Assert.That(b.Paths.Count, Is.EqualTo(a.Paths.Count));
            for(int i=0;i<a.Paths.Count;i++) Assert.That(b.Paths[i].Cells, Is.EqualTo(a.Paths[i].Cells));
            var grid=a.CreateGrid();
            foreach(int id in a.VerifiedSolution)
                Assert.That(grid.TryCommit(new PathMovementSimulator().Simulate(grid,id,a.Exits,a.Rules)), Is.True);
            var restarted=a.CreateGrid();
            Assert.That(restarted.OccupiedCellCount, Is.EqualTo(a.Statistics.OccupiedCells));
            Assert.That(restarted.Paths.Count, Is.EqualTo(a.Paths.Count));
            var different=new PuzzleGenerator().Generate(new PuzzleRecipe(unchecked(seed+1),recipe.Profile,recipe.Rules));
            Assert.That(different.Fingerprint, Is.Not.EqualTo(a.Fingerprint));
        }

        [Test]
        public void VersionMismatchDisabledExitsAndCancellationFailExplicitly()
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>new PuzzleRecipe(1,DifficultyProfiles.All[0],generationVersion:999));
            Assert.Throws<ArgumentException>(()=>new PuzzleRecipe(1,DifficultyProfiles.All[0],ExitRules.None));
            Assert.Throws<ArgumentNullException>(()=>new PuzzleGenerator().Generate(null));
            using(var source=new CancellationTokenSource())
            {
                source.Cancel();
                Assert.Throws<OperationCanceledException>(()=>new PuzzleGenerator().Generate(new PuzzleRecipe(1,DifficultyProfiles.All[0]),source.Token));
            }
        }

        [Test]
        public void UnityMatchesTheEngineIndependentDotnetGoldenFingerprint()
        {
            var p=new PuzzleGenerator().Generate(new PuzzleRecipe(0,DifficultyProfiles.All[0],ExitRules.Hybrid,generationVersion:1));
            Assert.That(p.Fingerprint, Is.EqualTo("f26fc7b76e70e945c0d490c086884036ce043301900a2b79a710206d6f3f5dbe"));
        }

        [Test]
        public void ImpossibleQualityBoundsAreRejectedInsteadOfReturningAPartialPuzzle()
        {
            var impossible=new DifficultyProfile("Impossible",4,2,8,1000,5,1,1,.99,6,.5,12);
            var recipe=new PuzzleRecipe(4,impossible,ExitRules.EdgeOnly,maxRestarts:1,attemptsPerCell:1);
            var error=Assert.Throws<PuzzleGenerationException>(()=>new PuzzleGenerator().Generate(recipe));
            Assert.That(error.Recipe, Is.SameAs(recipe));
            Assert.That(error.Report.QualityRejections, Is.EqualTo(1));
        }

        [TestCase(1,0)] [TestCase(5,0)] [TestCase(6,1)] [TestCase(15,1)] [TestCase(16,2)]
        [TestCase(30,2)] [TestCase(31,3)] [TestCase(60,3)] [TestCase(61,4)] [TestCase(100,4)] [TestCase(101,5)]
        public void SuggestedLevelBandsSelectProfilesWithoutConstrainingCustomProfiles(int level,int profile)
        { Assert.That(DifficultyProfiles.ForLevel(level), Is.SameAs(DifficultyProfiles.All[profile])); }

        [Test]
        public void CustomGridProfilesWorkAndCollectionsCannotBeMutated()
        {
            var profile=new DifficultyProfile("Custom 7",7,2,12,8,6,.50,.70,.10,.6,.55,42);
            var p=new PuzzleGenerator().Generate(new PuzzleRecipe(42,profile));
            Assert.That(p.Topology.GridSize, Is.EqualTo(7));
            Assert.Throws<NotSupportedException>(()=>((IList<int>)p.VerifiedSolution).Clear());
            Assert.Throws<NotSupportedException>(()=>((IList<ArrowPath>)p.Paths).Clear());
        }

        [Test]
        public void InspectorProfileAssetsCreateImmutableRecipeSnapshots()
        {
            var asset=ScriptableObject.CreateInstance<DifficultyProfileAsset>();
            try
            {
                asset.gridSize=7;
                var recipe=new PuzzleRecipe(42,asset.ToProfile());
                asset.gridSize=12;
                var p=new PuzzleGenerator().Generate(recipe);
                Assert.That(p.Topology.GridSize, Is.EqualTo(7));
                Assert.That(p.Recipe.Profile.GridSize, Is.EqualTo(7));
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }

        [Test]
        public void CustomGateBudgetsScaleWithAreaAndCanRemainAnAbsoluteCap()
        {
            var asset=ScriptableObject.CreateInstance<DifficultyProfileAsset>();
            try
            {
                asset.gridSize=30;
                Assert.That(asset.ToProfile().MaxGates, Is.EqualTo(675));
                var snapshot=asset.ToProfile();
                asset.scaleGateBudgetWithGridSize=false;
                Assert.That(asset.ToProfile().MaxGates, Is.EqualTo(48));
                Assert.That(snapshot.MaxGates, Is.EqualTo(675));
                asset.scaleGateBudgetWithGridSize=true; asset.gridSize=4;
                Assert.That(asset.ToProfile().MaxGates, Is.EqualTo(48));
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }

        [TestCase(29,0)] [TestCase(29,1)] [TestCase(29,2)]
        [TestCase(30,0)] [TestCase(30,1)] [TestCase(30,2)]
        [TestCase(32,0)] [TestCase(32,1)] [TestCase(32,2)]
        [TestCase(48,0)] [TestCase(48,1)] [TestCase(48,2)]
        [TestCase(64,0)] [TestCase(64,1)] [TestCase(64,2)]
        public void LargeCustomAssetsGenerateAndReplayWithoutRelaxingTheirQualityBounds(int size,int mode)
        {
            var asset=ScriptableObject.CreateInstance<DifficultyProfileAsset>();
            try
            {
                asset.gridSize=size;
                var recipe=new PuzzleRecipe(1,asset.ToProfile(),Rules(mode));
                var puzzle=new PuzzleGenerator().Generate(recipe);
                Assert.That(puzzle.Topology.GridSize, Is.EqualTo(size));
                Assert.That(PuzzleGenerator.QualityError(recipe,puzzle.Statistics), Is.Empty);
                Assert.That(puzzle.Exits.Gates.Count, Is.LessThanOrEqualTo(recipe.Profile.MaxGates));
                Assert.That(new PuzzleValidator().Validate(puzzle,puzzle.VerifiedSolution).Succeeded, Is.True);
                if(mode==0) Assert.That(puzzle.Exits.Gates, Is.Empty);
                if(mode==1) Assert.That(puzzle.Statistics.EdgeExitsInSolution, Is.Zero);
                if(mode==2)
                {
                    Assert.That(puzzle.Statistics.EdgeExitsInSolution, Is.GreaterThan(0));
                    Assert.That(puzzle.Statistics.GateExitsInSolution, Is.GreaterThan(0));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }

        [Test]
        public void ReconstructingAllRecipeValuesReproducesTheSavedGenerationInputs()
        {
            var original=new PuzzleRecipe(-315,DifficultyProfiles.All[2],ExitRules.GateOnly);
            var p=original.Profile;
            var restoredProfile=new DifficultyProfile(p.Name,p.GridSize,p.MinPathLength,p.MaxPathLength,p.MinPathCount,
                p.MinCrossFacePaths,p.MinimumCoverage,p.TargetCoverage,p.MinimumBlockedFraction,p.MinimumAverageTurns,p.TurnProbability,p.MaxGates);
            var restored=new PuzzleRecipe(original.Seed,restoredProfile,
                new ExitRules(original.Rules.EnableCubeEdgeExits,original.Rules.EnableDirectionalGates),
                original.GenerationVersion,original.MaxRestarts,original.AttemptsPerCell);
            Assert.That(new PuzzleGenerator().Generate(restored).Fingerprint,
                Is.EqualTo(new PuzzleGenerator().Generate(original).Fingerprint));
        }
    }
}
