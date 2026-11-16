using System;
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
    public class DependencyControlTests
    {
        private static SurfaceCoordinate F(int x,int y) => new SurfaceCoordinate(CubeFace.Front,x,y);
        private static DifficultyProfile Profile(int size=8) => new DifficultyProfile("Opening controls",size,2,14,14,8,
            .60f,.78f,.20f,1,.55f,(int)Math.Ceiling(48*Math.Max(1,size*(double)size/64)));

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void GenerationRejectsAnArrowPointingIntoItsOwnTailEvenWhenTailVacatingMakesItPlayable(int mode)
        {
            var t=new CubeTopology(8);
            var path=new ArrowPath(0,t,new[] { F(2,3),F(1,3),F(1,2),F(2,2),F(3,2),F(3,3) },SurfaceDirection.Right);
            var puzzle=new PuzzleDefinition("Self pointing",t,new[] { path },new ExitMap(t,new[] { new ExitGate(F(7,3),SurfaceDirection.Right) }),PuzzleGeneratorTests.Rules(mode));
            var plan=new PathMovementSimulator().Simulate(puzzle.CreateGrid(),0,puzzle.Exits,puzzle.Rules);
            Assert.That(plan.Succeeded, Is.True,"Existing tail-vacating movement remains compatible with old puzzles.");
            Assert.That(InitialExitClearance.IsClear(plan), Is.False);
            Assert.That(new PuzzleValidator().Validate(puzzle).Succeeded, Is.True);
            var strict=new PuzzleValidator().Validate(puzzle,requireClearInitialExit:true);
            Assert.That(strict.Succeeded, Is.False); Assert.That(strict.Error, Does.Contain("own starting body"));
        }

        [Test]
        public void ActualSelfCollisionAndAPathWithoutAnExitCannotPassClearance()
        {
            var t=new CubeTopology(8);
            var path=new ArrowPath(0,t,new[] { F(2,3),F(1,3),F(1,2),F(2,2),F(3,2),F(3,3),F(4,3),F(4,4) },SurfaceDirection.Right);
            var exits=new ExitMap(t,Array.Empty<ExitGate>());
            var plan=new PathMovementSimulator().Simulate(new SurfaceGrid(t,new[] { path }),0,exits,ExitRules.EdgeOnly);
            Assert.That(plan.Outcome, Is.EqualTo(MovementOutcome.SelfCollision)); Assert.That(InitialExitClearance.IsClear(plan), Is.False);
            var straight=new ArrowPath(1,t,new[] { F(2,4),F(1,4) },SurfaceDirection.Right);
            var noExit=new PathMovementSimulator().Simulate(new SurfaceGrid(t,new[] { straight }),1,exits,ExitRules.GateOnly);
            Assert.That(InitialExitClearance.IsClear(noExit), Is.False);
        }

        [TestCase(0,false)] [TestCase(1,false)] [TestCase(2,false)]
        [TestCase(0,true)] [TestCase(1,true)] [TestCase(2,true)]
        public void FewOpeningMovesAndNoSelfBlockingSurviveEveryExitModeWithOrWithoutMeasuredSearch(int mode,bool measured)
        {
            for(int seed=0;seed<3;seed++)
            {
                var settings=measured ? new DifficultySettings { MaxCandidates=2,OptimizationIterations=4,ScoreTolerance=0 } : null;
                if(settings!=null) settings.MultiBlocker.TargetedBlockerAttempts=4;
                var recipe=new PuzzleRecipe(seed,Profile(),PuzzleGeneratorTests.Rules(mode),difficulty:settings,
                    dependencyControls:new DependencyControlSettings { Amount=.75,MaximumStartingFreeArrows=3 });
                var p=new PuzzleGenerator().Generate(recipe);
                Assert.That(p.Statistics.ImmediatelyRemovablePaths, Is.InRange(1,3));
                Assert.That(PuzzleGenerator.QualityError(recipe,p.Statistics), Is.Empty);
                Assert.That(new PuzzleValidator().Validate(p,p.VerifiedSolution,requireClearInitialExit:true).Succeeded, Is.True);
                Assert.That(p.DependencyControlReport.InitiallyFree, Is.EqualTo(p.Statistics.ImmediatelyRemovablePaths));
                Assert.That(p.DependencyControlReport.AverageBlockersPerBlockedArrow,
                    Is.EqualTo(p.Statistics.DependencyCount/(double)p.Statistics.BlockedPaths).Within(.000001));
                Assert.That(p.Difficulty==null, Is.EqualTo(!measured));
                foreach(var path in p.Paths)
                {
                    var solo=new PathMovementSimulator().Simulate(new SurfaceGrid(p.Topology,new[] { path }),path.Id,p.Exits,p.Rules);
                    Assert.That(InitialExitClearance.IsClear(solo), Is.True,$"seed {seed}, arrow {path.Id}");
                }
            }
        }

        [TestCase(16,0)] [TestCase(16,1)] [TestCase(16,2)]
        [TestCase(30,0)] [TestCase(30,1)] [TestCase(30,2)]
        public void LargerSurfacesKeepFewOpeningMovesWithoutRelaxingCoverageOrAllowingCycles(int size,int mode)
        {
            var recipe=new PuzzleRecipe(0,Profile(size),PuzzleGeneratorTests.Rules(mode),
                dependencyControls:new DependencyControlSettings { Amount=.75,MaximumStartingFreeArrows=3 });
            var puzzle=new PuzzleGenerator().Generate(recipe);
            Assert.That(PuzzleGenerator.QualityError(recipe,puzzle.Statistics), Is.Empty);
            Assert.That(puzzle.Statistics.ImmediatelyRemovablePaths, Is.InRange(1,3));
            Assert.That(puzzle.Report.ValidationFailures, Is.Zero);
            Assert.That(new PuzzleValidator().Validate(puzzle,puzzle.VerifiedSolution,requireClearInitialExit:true).Succeeded, Is.True);
        }

        [Test]
        public void IncreasingDependencyAmountRaisesActualDistinctBlockersWithoutChangingSizeOrQualityBounds()
        {
            double low=0,high=0;
            for(int mode=0;mode<3;mode++) for(int seed=0;seed<5;seed++)
            foreach(double amount in new[] { 0.0,1.0 })
            {
                var recipe=new PuzzleRecipe(seed,Profile(),PuzzleGeneratorTests.Rules(mode),
                    dependencyControls:new DependencyControlSettings { Amount=amount });
                var puzzle=new PuzzleGenerator().Generate(recipe);
                Assert.That(puzzle.Difficulty, Is.Null); Assert.That(PuzzleGenerator.QualityError(recipe,puzzle.Statistics), Is.Empty);
                Assert.That(puzzle.Topology.GridSize, Is.EqualTo(8));
                if(amount==0) low+=puzzle.DependencyControlReport.AverageBlockersPerBlockedArrow;
                else high+=puzzle.DependencyControlReport.AverageBlockersPerBlockedArrow;
            }
            Assert.That(high, Is.GreaterThan(low*1.15),"Compare many seeds: individual geometry may saturate or differ.");
        }

        [TestCase(1)] [TestCase(3)] [TestCase(5)]
        public void OpeningCountIsAnExplicitMaximumAndRecipeSnapshotsAreDeterministic(int cap)
        {
            var settings=new DependencyControlSettings { Amount=.75,MaximumStartingFreeArrows=cap };
            var recipe=new PuzzleRecipe(1,Profile(),ExitRules.EdgeOnly,dependencyControls:settings);
            settings.Amount=0; settings.MaximumStartingFreeArrows=99; recipe.DependencyControls.MaximumStartingFreeArrows=99;
            var first=new PuzzleGenerator().Generate(recipe); var second=new PuzzleGenerator().Generate(recipe);
            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint)); Assert.That(second.VerifiedSolution, Is.EqualTo(first.VerifiedSolution));
            Assert.That(first.Statistics.ImmediatelyRemovablePaths, Is.InRange(1,cap)); Assert.That(recipe.DependencyControls.Amount, Is.EqualTo(.75));
        }

        [Test]
        public void InvalidControlsAreRejectedAndCancellationIsObserved()
        {
            foreach(double amount in new[] { double.NaN,double.PositiveInfinity,-.1,1.1 })
                Assert.Throws<ArgumentOutOfRangeException>(()=>new PuzzleRecipe(0,Profile(),dependencyControls:new DependencyControlSettings { Amount=amount }));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new PuzzleRecipe(0,Profile(),dependencyControls:new DependencyControlSettings { MaximumStartingFreeArrows=0 }));
            Assert.Throws<ArgumentException>(()=>new PuzzleRecipe(0,Profile(),generationVersion:4,dependencyControls:new DependencyControlSettings()));
            using(var token=new CancellationTokenSource())
            {
                token.Cancel();
                Assert.Throws<OperationCanceledException>(()=>new PuzzleGenerator().Generate(new PuzzleRecipe(0,Profile(),dependencyControls:new DependencyControlSettings()),token.Token));
            }
            var plain=new PuzzleGenerator().Generate(new PuzzleRecipe(0,Profile()));
            var restricted=new PuzzleRecipe(0,Profile(),dependencyControls:new DependencyControlSettings());
            Assert.That(PuzzleGenerator.QualityError(restricted,plain.Statistics), Does.Contain("Starting free arrows"));
            Assert.That(plain.DependencyControlReport, Is.Null);
        }

        [Test]
        public void InspectorBlockerControlsAreIndependentOfTheMeasuredDifficultyToggle()
        {
            var asset=ScriptableObject.CreateInstance<DifficultyProfileAsset>();
            try
            {
                Assert.That(asset.ToDependencyControls(), Is.Null);
                asset.advancedDifficulty=false; asset.controlDependencies=true; asset.dependencyAmount=.9f; asset.maximumStartingFreeArrows=2;
                var snapshot=asset.ToDependencyControls(); asset.maximumStartingFreeArrows=8;
                Assert.That(snapshot.MaximumStartingFreeArrows, Is.EqualTo(2)); Assert.That(snapshot.Amount, Is.EqualTo(.9).Within(.000001));
                Assert.That(asset.advancedDifficulty, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }
    }
}
