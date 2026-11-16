using System;
using ArrowCube.Generation;
using ArrowCube.Presentation;
using NUnit.Framework;

namespace ArrowCube.Tests
{
    public class GenerationOperationTests
    {
        [TestCase(0,false)] [TestCase(1,false)] [TestCase(2,false)]
        [TestCase(0,true)] [TestCase(1,true)] [TestCase(2,true)]
        public void CooperativeGenerationYieldsAndMatchesTheSynchronousFingerprint(int mode,bool measured)
        {
            var preset=DifficultyPresets.All[1]; var settings=measured ? preset.CreateSettings() : null;
            if(settings!=null) { settings.MaxCandidates=2; settings.OptimizationIterations=2; settings.MultiBlocker.TargetedBlockerAttempts=2; }
            var recipe=new PuzzleRecipe(14,preset.CreateProfile(),PuzzleGeneratorTests.Rules(mode),difficulty:settings,
                dependencyControls:new DependencyControlSettings { MaximumStartingFreeArrows=3,Amount=.75 });
            var expected=new PuzzleGenerator().Generate(recipe);
            using(var job=new GenerationOperation(recipe,true))
            {
                job.Tick(0); Assert.That(job.IsCompleted, Is.False,"Construction must yield before running the whole puzzle.");
                int ticks=1;
                while(!job.IsCompleted && ticks++<200000) job.Tick(0);
                Assert.That(job.IsCompleted, Is.True); Assert.That(job.Error, Is.Null); Assert.That(job.IsCanceled, Is.False);
                Assert.That(job.Result.Fingerprint, Is.EqualTo(expected.Fingerprint)); Assert.That(job.Result.VerifiedSolution, Is.EqualTo(expected.VerifiedSolution));
                Assert.That(new PuzzleValidator().Validate(job.Result,job.Result.VerifiedSolution,requireClearInitialExit:true).Succeeded, Is.True);
            }
        }
        [Test]
        public void CooperativeCancellationStopsPartialWorkAndNeverPublishesAPuzzle()
        {
            using(var job=new GenerationOperation(new PuzzleRecipe(14,DifficultyPresets.All[1].CreateProfile()),true))
            {
                for(int i=0;i<3;i++) job.Tick(0);
                job.Cancel(); job.Tick();
                Assert.That(job.IsCanceled, Is.True); Assert.That(job.IsCompleted, Is.True); Assert.That(job.Result, Is.Null); Assert.That(job.Error, Is.Null);
            }
        }
        [Test]
        public void CooperativeFailureCompletesWithAnExplicitErrorInsteadOfAnEndlessLoadingState()
        {
            var profile=new DifficultyProfile("Impossible",4,2,8,1000,5,1,1,.99,6,.5,12);
            using(var job=new GenerationOperation(new PuzzleRecipe(4,profile,PuzzleGeneratorTests.Rules(0),maxRestarts:1,attemptsPerCell:1),true))
            {
                int ticks=0; while(!job.IsCompleted && ticks++<10000) job.Tick(0);
                Assert.That(job.IsCompleted, Is.True); Assert.That(job.Error, Is.TypeOf<PuzzleGenerationException>()); Assert.That(job.Result, Is.Null);
            }
        }
        [Test]
        public void InvalidBudgetsAreRejectedWithoutAdvancingTheJob()
        {
            using(var job=new GenerationOperation(new PuzzleRecipe(0,DifficultyPresets.All[0].CreateProfile()),true))
            {
                Assert.Throws<ArgumentOutOfRangeException>(()=>job.Tick(double.NaN));
                Assert.Throws<ArgumentOutOfRangeException>(()=>job.Tick(-1)); Assert.That(job.IsCompleted, Is.False);
            }
        }
    }
}
