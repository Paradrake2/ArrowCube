using System;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Topology;
using NUnit.Framework;

namespace ArrowCube.Tests
{
    public class PuzzleValidatorTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(5)] [TestCase(6)]
        public void SolvesManualPuzzlesUsingTheirActualExitSettings(int fixture)
        {
            var p=ManualPuzzles.Create(fixture);
            var result=new PuzzleValidator().Validate(p);
            Assert.That(result.Succeeded, Is.True,result.Error);
            Assert.That(result.Solution.Count, Is.EqualTo(p.Paths.Count));
            Assert.That(p.CreateGrid().Paths.Count, Is.EqualTo(p.Paths.Count),"Validation must not change the definition.");
        }

        [Test]
        public void RejectsUnsolvableEmptyOverlappingAndIncorrectSolutionPuzzles()
        {
            var validator=new PuzzleValidator();
            Assert.That(validator.Validate(ManualPuzzles.Create(4)).Succeeded, Is.False);
            var p=ManualPuzzles.Create(2);
            Assert.That(validator.Validate(p,new[] { 10,20 }).Succeeded, Is.False);
            Assert.That(validator.Validate(p,new[] { 20 }).Succeeded, Is.False);
            Assert.That(validator.Validate(p,new[] { 20,20,10 }).Succeeded, Is.False);
            Assert.That(validator.Validate(p,new[] { 999 }).Succeeded, Is.False);
            var empty=new PuzzleDefinition("Empty",p.Topology,Array.Empty<ArrowPath>(),p.Exits,p.Rules);
            Assert.That(validator.Validate(empty).Succeeded, Is.False);
            var overlap=new PuzzleDefinition("Overlap",p.Topology,new[] { p.Paths[0],p.Paths[0] },p.Exits,p.Rules);
            Assert.That(validator.Validate(overlap).Succeeded, Is.False);
        }

        [Test]
        public void ReportsAllDiagramDependenciesAndOnlyCountsActualBends()
        {
            var result=new PuzzleValidator().Validate(ManualPuzzles.Create(5));
            Assert.That(result.Statistics.ImmediatelyRemovablePaths, Is.EqualTo(2));
            Assert.That(result.Statistics.DependencyCount, Is.EqualTo(2));
            Assert.That(result.Statistics.DependencyDepth, Is.EqualTo(3));
            var t=new CubeTopology(4);
            var straightSeam=new ArrowPath(1,t,new[] { new SurfaceCoordinate(CubeFace.Top,3,1),new SurfaceCoordinate(CubeFace.Right,1,3),new SurfaceCoordinate(CubeFace.Right,1,2) },SurfaceDirection.Left);
            Assert.That(PuzzleValidator.CountTurns(t,straightSeam), Is.Zero,"A folded straight seam must not count as a 90-degree body bend.");
        }
    }
}
