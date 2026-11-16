using System;
using System.IO;
using System.Linq;
using ArrowCube.Game;
using ArrowCube.Gameplay;
using ArrowCube.Generation;
using ArrowCube.Movement;
using NUnit.Framework;
using UnityEngine;

namespace ArrowCube.Tests
{
    public class GameplaySessionTests
    {
        private static GameplaySession Fixture(bool unlimited=true,bool powerUps=true) => new GameplaySession(ManualPuzzles.Create(2),
            new GameplayOptions { UnlimitedAttempts=unlimited,UnlimitedPowerUps=powerUps });
        private static void Attempt(GameplaySession session,int id)
        { var plan=session.BeginAttempt(id); Assert.That(plan, Is.Not.Null); Assert.That(session.CompleteAttempt(plan), Is.True); }
        [Test]
        public void UnlimitedIsDefaultAndMistakesNeverCauseGameOverOrChangeOccupancy()
        {
            var session=Fixture(); var revision=session.Grid.Revision;
            for(int i=0;i<20;i++) Attempt(session,10);
            Assert.That(session.Options.UnlimitedAttempts, Is.True); Assert.That(session.State, Is.EqualTo(PlayState.Playing));
            Assert.That(session.Hearts, Is.EqualTo(3)); Assert.That(session.Mistakes, Is.EqualTo(20)); Assert.That(session.Grid.Revision, Is.EqualTo(revision));
        }
        [Test]
        public void StandardModeDeductsExactlyOncePerCompletedFailureAndRejectsMovesAfterFailure()
        {
            var session=Fixture(false); var plan=session.BeginAttempt(10);
            Assert.That(session.Hearts, Is.EqualTo(3)); Assert.That(session.BeginAttempt(20), Is.Null);
            Assert.That(session.CompleteAttempt(plan), Is.True); Assert.That(session.CompleteAttempt(plan), Is.False); Assert.That(session.Hearts, Is.EqualTo(2));
            Attempt(session,10); Attempt(session,10);
            Assert.That(session.State, Is.EqualTo(PlayState.Failed)); Assert.That(session.BeginAttempt(20), Is.Null);
            var options=session.Options.Copy(); options.UnlimitedAttempts=true; session.Configure(options);
            Assert.That(session.State, Is.EqualTo(PlayState.Playing)); Assert.That(session.Grid.Paths.Count, Is.EqualTo(2));
            options.UnlimitedAttempts=false; session.Configure(options); Assert.That(session.State, Is.EqualTo(PlayState.Failed));
        }
        [Test]
        public void CompletedRemovalUsesTheSameGridAsAnimationAndStaleCallbacksCannotDeductOrRemoveTwice()
        {
            var session=Fixture(false); var plan=session.BeginAttempt(20);
            Assert.That(session.Grid.TryCommit(plan), Is.True);
            Assert.That(session.CompleteAttempt(plan,true), Is.True); Assert.That(session.CompleteAttempt(plan,true), Is.False);
            Assert.That(session.Undo(), Is.True); Assert.That(session.Grid.Paths[20], Is.SameAs(session.Puzzle.Paths[1]));
            plan=session.BeginAttempt(20); session.Restart(); Assert.That(session.CompleteAttempt(plan), Is.False);
            Assert.That(session.Grid.Paths.Count, Is.EqualTo(2)); Assert.That(session.Hearts, Is.EqualTo(3));
        }
        [Test]
        public void HintUsesCurrentStateAndLimitedPowerUpsAreConsumedOnlyOnSuccessfulActions()
        {
            var session=Fixture(powerUps:false);
            Assert.That(session.Hint(), Is.EqualTo(20)); Assert.That(session.HintsLeft, Is.EqualTo(2));
            Assert.That(session.Hammer(999), Is.False); Assert.That(session.HammersLeft, Is.EqualTo(3));
            Assert.That(session.Hammer(10), Is.True); Assert.That(session.HammersLeft, Is.EqualTo(2)); Assert.That(session.Grid.Paths.ContainsKey(10), Is.False);
            Assert.That(session.Undo(), Is.True); Assert.That(session.Grid.Paths.ContainsKey(10), Is.True); Assert.That(session.HammersLeft, Is.EqualTo(3));
            Assert.That(session.UndosLeft, Is.EqualTo(2));
            var options=session.Options.Copy(); options.UnlimitedPowerUps=true; session.Configure(options);
            for(int i=0;i<12;i++) Assert.That(session.Hint(), Is.EqualTo(20));
            Assert.That(session.HintsLeft, Is.EqualTo(2));
        }
        [Test]
        public void PauseBlocksActionsAndTimeButCapturedPendingAttemptsAreSafeToReload()
        {
            var session=Fixture(); session.Tick(2); var plan=session.BeginAttempt(20); session.Pause(true);
            Assert.That(session.Hint(), Is.Null); Assert.That(session.Hammer(10), Is.False); session.Tick(9);
            Assert.That(session.Seconds, Is.EqualTo(2));
            var restored=GameplaySession.Restore(session.Puzzle,session.Capture());
            Assert.That(restored.HasPendingAttempt, Is.False); Assert.That(restored.Grid.Paths.Count, Is.EqualTo(2));
            Assert.That(session.CompleteAttempt(plan), Is.True); session.Pause(false); Assert.That(session.Grid.Paths.Count, Is.EqualTo(1));
        }
        [Test]
        public void VictoryStarsUnlocksAndRestartPreserveExactPuzzleWhileReplayKeepsBestRatings()
        {
            var session=Fixture(); var progress=new CampaignProgress(); var definition=session.Puzzle;
            Attempt(session,20); Attempt(session,10); Assert.That(session.State, Is.EqualTo(PlayState.Victory)); Assert.That(session.Stars, Is.EqualTo(3));
            progress.Complete(1,session); Assert.That(progress.HighestUnlocked, Is.EqualTo(2)); Assert.That(progress.IsUnlocked(3), Is.False);
            session.Restart(); Assert.That(session.Puzzle, Is.SameAs(definition)); Assert.That(session.Grid.OccupiedCellCount, Is.EqualTo(definition.Paths.Sum(p=>p.Cells.Count)));
            Attempt(session,10); session.Hammer(20); session.Hammer(10); Assert.That(session.Stars, Is.EqualTo(1));
            progress.Complete(1,session); Assert.That(progress.Find(1).BestStars, Is.EqualTo(3)); Assert.That(progress.Find(1).Completions, Is.EqualTo(2));
            Assert.DoesNotThrow(()=>progress.Validate());
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void SavedGeometryRecipeAndUndoHistorySurviveJsonReloadInEveryExitMode(int mode)
        {
            var preset=DifficultyPresets.All[0]; var settings=preset.CreateSettings(); settings.ApplyQuality(GenerationQuality.Fast);
            var generated=new PuzzleGenerator().Generate(new PuzzleRecipe(14,preset.CreateProfile(),PuzzleGeneratorTests.Rules(mode),difficulty:settings));
            var session=new GameplaySession(generated); Attempt(session,generated.VerifiedSolution[0]);
            var level=new ActiveLevelSave { Puzzle=PuzzleSave.Capture(generated),Recipe=RecipeSave.Capture(generated.Recipe),Session=session.Capture(),Fingerprint=generated.Fingerprint };
            var restoredSave=JsonUtility.FromJson<ActiveLevelSave>(JsonUtility.ToJson(level)); var restored=restoredSave.Restore();
            Assert.That(new PuzzleGenerator().Generate(restoredSave.Recipe.Restore()).Fingerprint, Is.EqualTo(generated.Fingerprint));
            Assert.That(restored.Puzzle.Paths.SelectMany(p=>p.Cells), Is.EqualTo(generated.Paths.SelectMany(p=>p.Cells)));
            Assert.That(restored.Puzzle.Exits.Gates, Is.EqualTo(generated.Exits.Gates)); Assert.That(restored.Grid.Paths.Count, Is.EqualTo(generated.Paths.Count-1));
            Assert.That(restored.Undo(), Is.True); Assert.That(restored.Grid.Paths.Count, Is.EqualTo(generated.Paths.Count));
        }
        [Test]
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void RecipeEncodingRetainsEveryGenerationVersionAndOptionalSettingBitForBit(int version)
        {
            var preset=DifficultyPresets.All[0];
            var recipe=new PuzzleRecipe(-213,preset.CreateProfile(),generationVersion:version,
                difficulty:version>=3 ? preset.CreateSettings() : null,
                dependencyControls:version>=5 ? new DependencyControlSettings { Amount=.631415 } : null);
            var saved=RecipeSave.Capture(recipe); var roundTrip=JsonUtility.FromJson<RecipeSave>(JsonUtility.ToJson(saved));
            Assert.That(RecipeSave.Capture(roundTrip.Restore()).ExactData, Is.EqualTo(saved.ExactData));
            roundTrip.ExactData="not-base64"; Assert.Throws<FormatException>(()=>roundTrip.Restore());
        }

        [Test]
        public void TamperedHistoryCannotRestoreBlockedNormalRemovalsOrDuplicatePaths()
        {
            var session=Fixture(); var save=session.Capture(); save.History.Add(new RemovalSave { PathId=10 });
            Assert.Throws<ArgumentException>(()=>GameplaySession.Restore(session.Puzzle,save));
            save=session.Capture(); save.History.Add(new RemovalSave { PathId=20 }); save.History.Add(new RemovalSave { PathId=20 });
            Assert.Throws<ArgumentException>(()=>GameplaySession.Restore(session.Puzzle,save));
            save=session.Capture(); save.Hearts=-1; Assert.Throws<ArgumentException>(()=>GameplaySession.Restore(session.Puzzle,save));
        }
        [Test]
        public void LocalSaveUsesChecksumAndBackupAndNeverDependsOnRegenerationForResume()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/Stage5Saves",Guid.NewGuid().ToString("N")));
            try
            {
                var store=new LocalSaveStore(Path.Combine(folder,"player.json")); var session=Fixture(false);
                var player=new PlayerSave { Active=new ActiveLevelSave { Puzzle=PuzzleSave.Capture(session.Puzzle),Session=session.Capture() } };
                store.Save(player); Attempt(session,20); player.Active.Session=session.Capture(); store.Save(player);
                var loaded=store.Load(out var warning); Assert.That(warning, Is.Empty); Assert.That(loaded.Active.Restore().Grid.Paths.Count, Is.EqualTo(1));
                File.WriteAllText(store.Path,"broken"); loaded=store.Load(out warning);
                Assert.That(loaded, Is.Not.Null); Assert.That(warning, Does.Contain("backup")); Assert.That(loaded.Active.Restore().Grid.Paths.Count, Is.EqualTo(2));
                store.Save(loaded); File.WriteAllText(store.Path,"broken again");
                Assert.That(store.Load(out warning), Is.Not.Null,"Recovery must not replace the good backup with the broken main file.");
                File.WriteAllText(store.Path+".bak","also broken"); Assert.That(store.Load(out warning), Is.Null); Assert.That(warning, Is.Not.Empty);
                Assert.That(File.Exists(store.Path), Is.True,"Corrupt files stay available for recovery.");
            }
            finally { if(Directory.Exists(folder)) Directory.Delete(folder,true); }
        }
        [Test]
        public void OlderSavesWithoutInversionFieldsUseTheCorrectedDefaultDirections()
        {
            var old=JsonUtility.FromJson<PlayerSave>("{\"Schema\":1}");
            Assert.That(old.InvertMouseRotation, Is.False); Assert.That(old.InvertTouchRotation, Is.False);
        }
        [TestCase(false,false)]
        [TestCase(false,true)]
        [TestCase(true,false)]
        [TestCase(true,true)]
        public void IndependentRotationPreferencesSurviveLocalSave(bool mouse,bool touch)
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/RotationSaves",Guid.NewGuid().ToString("N")));
            try
            {
                var store=new LocalSaveStore(Path.Combine(folder,"player.json"));
                store.Save(new PlayerSave { InvertMouseRotation=mouse,InvertTouchRotation=touch });
                var saved=store.Load(out var warning); Assert.That(saved, Is.Not.Null,warning);
                Assert.That(saved.InvertMouseRotation, Is.EqualTo(mouse)); Assert.That(saved.InvertTouchRotation, Is.EqualTo(touch));
            }
            finally { if(Directory.Exists(folder)) Directory.Delete(folder,true); }
        }
        [Test]
        public void LevelRecipesAreDeterministicAndSettingsDoNotChangeAnExistingSnapshot()
        {
            var settings=ScriptableObject.CreateInstance<GameplaySettingsAsset>();
            try
            {
                Assert.That(settings.defaults.UnlimitedAttempts, Is.True);
                var first=settings.RecipeFor(1,1729); var same=settings.RecipeFor(1,1729);
                Assert.That(first.Seed, Is.EqualTo(same.Seed)); Assert.That(first.Profile.GridSize, Is.EqualTo(6));
                Assert.That(settings.RecipeFor(21,1729).Profile.GridSize, Is.EqualTo(8));
                var generated=new PuzzleGenerator().Generate(first); settings.dependencyAmount=0; settings.maximumStartingFreeArrows=12;
                Assert.That(generated.Recipe.DependencyControls.MaximumStartingFreeArrows, Is.EqualTo(3));
                Assert.That(generated.Fingerprint, Is.EqualTo(new PuzzleGenerator().Generate(same).Fingerprint));
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }
    }
}
