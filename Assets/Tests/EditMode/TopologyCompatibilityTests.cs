using System;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Topology;
using NUnit.Framework;

namespace ArrowCube.Tests
{
    public class TopologyCompatibilityTests
    {
        [TestCase(1)] [TestCase(2)] [TestCase(6)] [TestCase(29)] [TestCase(64)]
        public void CubeCoordinatesCellIdsBoundariesAndSharedTraversalAgree(int size)
        {
            var cube=new CubeTopology(size); ISurfaceTopology topology=cube;
            foreach(int id in topology.EnumerateCellIds())
            for(int d=0;d<4;d++)
            {
                var direction=(SurfaceDirection)d; var from=cube.GetCoordinate(id);
                var step=cube.Step(from,direction); var generic=topology.Step(id,direction);
                Assert.That(cube.GetCellIndex(step.Coordinate), Is.EqualTo(generic.CellId));
                Assert.That(step.Direction, Is.EqualTo(generic.Direction));
                Assert.That(step.CrossedEdge, Is.EqualTo(from.Face!=step.Coordinate.Face));
                Assert.That(step.AllowsEdgeExit, Is.EqualTo(step.CrossedEdge));
                var boundary=topology.GetBoundary(id,direction);
                Assert.That(boundary.NeighborCellId, Is.EqualTo(generic.CellId));
                Assert.That(boundary.IsGeometricEdge, Is.EqualTo(step.CrossedEdge));
                var reverse=topology.Step(generic.CellId,generic.Direction.Opposite());
                Assert.That(reverse.CellId, Is.EqualTo(id)); Assert.That(reverse.Direction, Is.EqualTo(direction.Opposite()));
            }
        }

        [Test]
        public void CubeLargeGridSupportRemainsLazyAndInvalidIdsAndDirectionsFail()
        {
            var cube=new CubeTopology(18918);
            var last=cube.GetCell(cube.CellCount-1);
            Assert.That(last.Id, Is.EqualTo(cube.CellCount-1));
            Assert.That(cube.ContainsCell(-1) || cube.ContainsCell(cube.CellCount), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(()=>cube.GetCell(-1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>cube.GetBoundary(0,(SurfaceDirection)4));
        }

        // Captured from the untouched implementation immediately before this refactor.
        [TestCase(0,0,"913854b005e10272cf42471a80818f5d5612aaf680005c3a23b95e983a7c4063")]
        [TestCase(0,1,"f342a503a15a6b6545f540bfbea515b4a762640f1b5d5327326414b81a42d2d4")]
        [TestCase(0,2,"bc1a8f65fd2303004e586692cbde1c45956bfe51796a3aefe8e319a34af6c923")]
        [TestCase(5,0,"108bb45eb25ecf29b77ba603c7d24a26af45380b6096061b7cb198b7d71b061a")]
        [TestCase(5,1,"c65b3091f2bff3e1e7240a054ce10c9685d7b0fbb57edf080ac554198aaf1628")]
        [TestCase(5,2,"504fcaa7c92bc07856cc5a4f8e0c2220bd79adf75f190fdef1a56293ce93b69b")]
        public void Version5RecipesKeepTheirPreRefactorFingerprints(int profile,int mode,string expected)
        {
            var rules=mode==0 ? ExitRules.EdgeOnly : mode==1 ? ExitRules.GateOnly : ExitRules.Hybrid;
            var puzzle=new PuzzleGenerator().Generate(new PuzzleRecipe(0,DifficultyProfiles.All[profile],rules,generationVersion:5));
            Assert.That(puzzle.Fingerprint, Is.EqualTo(expected));
            Assert.That(new PuzzleValidator().Validate(puzzle,puzzle.VerifiedSolution).Succeeded, Is.True);
        }
    }
}
