using System;
using System.IO;
using System.Text;
using ArrowCube.Generation;
using NUnit.Framework;
using UnityEngine;

namespace ArrowCube.Tests
{
    public class GenerationStressTests
    {
        [Test,Category("GenerationStress")]
        public void GeneratesAndReplays1008PuzzlesAcrossSixDifficultiesAndThreeExitModes()
        {
            var csv=new StringBuilder(GenerationCsv.Header);
            var generator=new PuzzleGenerator(); var validator=new PuzzleValidator();
            int total=0,failures=0,validationFailures=0;
            var errors=new StringBuilder();
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../TestResults"));
            Directory.CreateDirectory(directory);
            try
            {
                for(int profile=0;profile<6;profile++)
                for(int mode=0;mode<3;mode++)
                {
                    double elapsed=0,coverage=0; int count=0;
                    for(int seed=0;seed<56;seed++)
                    {
                        try
                        {
                            var recipe=new PuzzleRecipe(seed,DifficultyProfiles.All[profile],PuzzleGeneratorTests.Rules(mode));
                            var puzzle=generator.Generate(recipe);
                            var replay=validator.Validate(puzzle,puzzle.VerifiedSolution);
                            if(!replay.Succeeded) { validationFailures++; throw new InvalidOperationException(replay.Error); }
                            string quality=PuzzleGenerator.QualityError(recipe,puzzle.Statistics);
                            if(quality.Length>0) throw new InvalidOperationException(quality);
                            csv.Append(GenerationCsv.Row(puzzle));
                            elapsed+=puzzle.Report.Milliseconds; coverage+=puzzle.Statistics.Coverage;
                            validationFailures+=puzzle.Report.ValidationFailures; total++; count++;
                        }
                        catch(Exception error)
                        {
                            failures++;
                            errors.AppendLine($"profile={profile}, mode={mode}, seed={seed}: {error.Message}");
                        }
                    }
                    TestContext.Progress.WriteLine($"{DifficultyProfiles.All[profile].Name}, mode {mode}: {count}/56; mean generation {elapsed/Math.Max(1,count):F1} ms, mean coverage {coverage/Math.Max(1,count):P1}");
                }
            }
            finally
            {
                File.WriteAllText(Path.Combine(directory,"stage3-generation-1008.csv"),csv.ToString());
                File.WriteAllText(Path.Combine(directory,"stage3-generation-errors.txt"),errors.ToString());
            }
            Assert.That(failures, Is.Zero,errors.ToString());
            Assert.That(validationFailures, Is.Zero,"No accepted/rejected full layout may have an unverified reverse solution.");
            Assert.That(total, Is.EqualTo(1008));
        }
    }
}
