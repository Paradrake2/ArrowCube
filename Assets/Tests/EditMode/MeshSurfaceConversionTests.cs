using System;
using System.Linq;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using ArrowCube.Topology;
using NUnit.Framework;
using UnityEngine;

namespace ArrowCube.Tests
{
    public class MeshSurfaceConversionTests
    {
        [TestCase(MeshConversionFixture.RectangularPrism,96)] [TestCase(MeshConversionFixture.L,224)]
        [TestCase(MeshConversionFixture.T,288)] [TestCase(MeshConversionFixture.AngularPrism,192)]
        public void NativeQuadFixturesConvertGenerateAndSolve(MeshConversionFixture fixture,int expected)
        {
            var mesh=MeshConversionFixtures.Create(fixture,false,true);
            try
            {
                var surface=MeshSurfaceConverter.Convert(mesh); Assert.That(surface.Shape.Topology.CellCount,Is.EqualTo(expected));
                Assert.That(surface.Report.ConvexBoundaries,Is.GreaterThan(0));
                if(fixture==MeshConversionFixture.L || fixture==MeshConversionFixture.T) Assert.That(surface.Report.ConcaveBoundaries,Is.GreaterThan(0));
                foreach(int c in surface.Shape.Topology.EnumerateCellIds()) for(int d=0;d<4;d++)
                {
                    var dir=(SurfaceDirection)d; var step=surface.Shape.Topology.Step(c,dir); var reverse=surface.Shape.Topology.Step(step.CellId,step.Direction.Opposite());
                    Assert.That(reverse.CellId,Is.EqualTo(c)); Assert.That(reverse.Direction,Is.EqualTo(dir.Opposite()));
                    Assert.That(Vector3.Distance(surface.Geometry.GetBoundaryPosition(c,dir),surface.Geometry.GetBoundaryPosition(step.CellId,step.Direction.Opposite())),Is.LessThan(.000001));
                    var kind=MeshSurfaceConverter.Classify(surface.Geometry.GetCell(c),surface.Geometry.GetCell(step.CellId),dir,new MeshConversionSettings());
                    Assert.That(step.AllowsEdgeExit,Is.EqualTo(kind==MeshFoldKind.Convex)); Assert.That(step.IsGeometricEdge,Is.EqualTo(kind!=MeshFoldKind.Coplanar));
                }
                var recipe=new PuzzleRecipe(12345,surface.Shape,AuthoredGenerationSurfaces.Profile(expected),ExitRules.GateOnly,
                    dependencyControls:new DependencyControlSettings { Amount=.5,MaximumStartingFreeArrows=3 });
                var generated=new PuzzleGenerator().Generate(recipe);
                Assert.That(generated.DependencyGraph.IsValid,Is.True); Assert.That(generated.Statistics.DependencyCount,Is.GreaterThan(0));
                Assert.That(generated.Statistics.ImmediatelyRemovablePaths,Is.InRange(1,3));
                var validation=new PuzzleValidator().Validate(generated,generated.VerifiedSolution,requireClearInitialExit:true); Assert.That(validation.Succeeded,Is.True,validation.Error);
                var repeat=MeshSurfaceConverter.Convert(mesh); Assert.That(repeat.ContentDigest,Is.EqualTo(surface.ContentDigest));
                Assert.That(new PuzzleGenerator().Generate(new PuzzleRecipe(12345,repeat.Shape,AuthoredGenerationSurfaces.Profile(expected),ExitRules.GateOnly,
                    dependencyControls:new DependencyControlSettings { Amount=.5,MaximumStartingFreeArrows=3 })).Fingerprint,Is.EqualTo(generated.Fingerprint));
                Debug.Log($"PHASE5 {fixture} cells={expected} patches={surface.Report.ParentQuads} flat={surface.Report.CoplanarBoundaries} convex={surface.Report.ConvexBoundaries} concave={surface.Report.ConcaveBoundaries} conversion_ms={surface.Report.ConversionMilliseconds:F2} validation_ms={surface.Report.ValidationMilliseconds:F2} memory_delta={surface.Report.ManagedMemoryDelta} generation_ms={generated.Report.Milliseconds:F2} attempts={generated.Report.CandidateAttempts} rejected={generated.Report.RejectedCandidates} paths={generated.Paths.Count} dependencies={generated.Statistics.DependencyCount} depth={generated.Statistics.DependencyDepth} fingerprint={generated.Fingerprint}");
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
        [Test]
        public void TrianglePrismPairsUniquelyAndMatchesNativeQuadConversion()
        {
            var a=MeshConversionFixtures.Create(MeshConversionFixture.RectangularPrism); var b=MeshConversionFixtures.Create(MeshConversionFixture.RectangularPrism,true,true);
            try { Assert.That(MeshSurfaceConverter.Convert(a).ContentDigest,Is.EqualTo(MeshSurfaceConverter.Convert(b).ContentDigest)); }
            finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
        }
    }
}
