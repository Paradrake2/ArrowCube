using System;
using System.Collections.Generic;
using System.Linq;
using ArrowCube.Topology;
using NUnit.Framework;

namespace ArrowCube.Tests
{
    public class CubeTopologyTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(6)]
        [TestCase(8)]
        [TestCase(20)]
        public void EveryStepIsReversibleAndAllSeamsMatchIndependentFaceGeometry(int size)
        {
            var topology = new CubeTopology(size);
            int crossings = 0;
            var physicalEdges = new HashSet<string>();
            foreach (var cell in topology.EnumerateCells())
            {
                Assert.That(topology.GetCoordinate(topology.GetCellIndex(cell)), Is.EqualTo(cell));
                var neighbors = new HashSet<SurfaceCoordinate>();
                for (int d = 0; d < 4; d++)
                {
                    var direction = (SurfaceDirection)d;
                    var step = topology.Step(cell, direction);
                    Assert.That(topology.Contains(step.Coordinate), Is.True);
                    Assert.That(neighbors.Add(step.Coordinate), Is.True, $"Duplicate neighbor of {cell}");
                    var reverse = topology.Step(step.Coordinate, step.Direction.Opposite());
                    Assert.That(reverse.Coordinate, Is.EqualTo(cell), $"Round trip from {cell} / {direction}");
                    Assert.That(reverse.Direction, Is.EqualTo(direction.Opposite()));
                    Assert.That(reverse.CrossedEdge, Is.EqualTo(step.CrossedEdge));
                    Assert.That(new SurfaceSegment(topology, cell, step.Coordinate),
                        Is.EqualTo(new SurfaceSegment(topology, step.Coordinate, cell)));

                    var sourceFrame = topology.GetOrientation(cell.Face);
                    if (!step.CrossedEdge)
                    {
                        Assert.That(step.Direction, Is.EqualTo(direction));
                        Assert.That(topology.GetDoubledPosition(step.Coordinate),
                            Is.EqualTo(topology.GetDoubledPosition(cell) + sourceFrame.GetTangent(direction) * 2));
                        continue;
                    }

                    crossings++;
                    int a = Math.Min((int)cell.Face, (int)step.Coordinate.Face);
                    int b = Math.Max((int)cell.Face, (int)step.Coordinate.Face);
                    physicalEdges.Add($"{a}-{b}");
                    var destinationFrame = topology.GetOrientation(step.Coordinate.Face);
                    Assert.That(destinationFrame.Normal, Is.EqualTo(sourceFrame.GetTangent(direction)));
                    Assert.That(destinationFrame.GetTangent(step.Direction), Is.EqualTo(-sourceFrame.Normal));
                    var seam = topology.GetDoubledEdgeMidpoint(cell, direction);
                    Assert.That(seam, Is.EqualTo(topology.GetDoubledEdgeMidpoint(step.Coordinate, step.Direction.Opposite())));
                    Assert.That(seam, Is.EqualTo(topology.GetDoubledPosition(step.Coordinate)
                        + destinationFrame.GetTangent(step.Direction.Opposite())));
                }
                Assert.That(neighbors.Count, Is.EqualTo(4));
            }
            Assert.That(crossings, Is.EqualTo(24 * size));
            Assert.That(physicalEdges.Count, Is.EqualTo(12));
        }

        // Independent concrete examples for all twelve physical edges, using a non-central offset.
        [TestCase(CubeFace.Front, 4, 1, SurfaceDirection.Right, CubeFace.Right, 0, 1, SurfaceDirection.Right)]
        [TestCase(CubeFace.Front, 1, 4, SurfaceDirection.Up, CubeFace.Top, 1, 0, SurfaceDirection.Up)]
        [TestCase(CubeFace.Front, 0, 1, SurfaceDirection.Left, CubeFace.Left, 4, 1, SurfaceDirection.Left)]
        [TestCase(CubeFace.Front, 1, 0, SurfaceDirection.Down, CubeFace.Bottom, 1, 4, SurfaceDirection.Down)]
        [TestCase(CubeFace.Right, 4, 1, SurfaceDirection.Right, CubeFace.Back, 0, 1, SurfaceDirection.Right)]
        [TestCase(CubeFace.Right, 1, 4, SurfaceDirection.Up, CubeFace.Top, 4, 1, SurfaceDirection.Left)]
        [TestCase(CubeFace.Right, 1, 0, SurfaceDirection.Down, CubeFace.Bottom, 4, 3, SurfaceDirection.Left)]
        [TestCase(CubeFace.Back, 4, 1, SurfaceDirection.Right, CubeFace.Left, 0, 1, SurfaceDirection.Right)]
        [TestCase(CubeFace.Back, 1, 4, SurfaceDirection.Up, CubeFace.Top, 3, 4, SurfaceDirection.Down)]
        [TestCase(CubeFace.Back, 1, 0, SurfaceDirection.Down, CubeFace.Bottom, 3, 0, SurfaceDirection.Up)]
        [TestCase(CubeFace.Left, 1, 4, SurfaceDirection.Up, CubeFace.Top, 0, 3, SurfaceDirection.Right)]
        [TestCase(CubeFace.Left, 1, 0, SurfaceDirection.Down, CubeFace.Bottom, 0, 1, SurfaceDirection.Right)]
        public void KnownEdgeMappings(CubeFace fromFace, int x, int y, SurfaceDirection direction,
            CubeFace toFace, int toX, int toY, SurfaceDirection heading)
        {
            var step = new CubeTopology(5).Step(new SurfaceCoordinate(fromFace, x, y), direction);
            Assert.That(step.Coordinate, Is.EqualTo(new SurfaceCoordinate(toFace, toX, toY)));
            Assert.That(step.Direction, Is.EqualTo(heading));
            Assert.That(step.CrossedEdge, Is.True);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(6)]
        [TestCase(20)]
        public void AllSixFacesAreOneConnectedSurface(int size)
        {
            var topology = new CubeTopology(size);
            var origin = new SurfaceCoordinate(CubeFace.Front, 0, 0);
            var visited = new HashSet<SurfaceCoordinate> { origin };
            var pending = new Queue<SurfaceCoordinate>();
            pending.Enqueue(origin);
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    var neighbor = topology.Step(cell, (SurfaceDirection)d).Coordinate;
                    if (visited.Add(neighbor)) pending.Enqueue(neighbor);
                }
            }
            Assert.That(visited.Count, Is.EqualTo(6 * size * size));
        }

        [Test]
        public void FaceFramesAreOrthonormalAndOutwardFacing()
        {
            var topology = new CubeTopology(6);
            for (int f = 0; f < 6; f++)
            {
                var frame = topology.GetOrientation((CubeFace)f);
                Assert.That(Dot(frame.Normal, frame.Normal), Is.EqualTo(1));
                Assert.That(Dot(frame.Right, frame.Right), Is.EqualTo(1));
                Assert.That(Dot(frame.Up, frame.Up), Is.EqualTo(1));
                Assert.That(Dot(frame.Normal, frame.Right), Is.Zero);
                Assert.That(Dot(frame.Normal, frame.Up), Is.Zero);
                Assert.That(Dot(frame.Right, frame.Up), Is.Zero);
                Assert.That(new GridVector3(frame.Right.Y * frame.Up.Z - frame.Right.Z * frame.Up.Y,
                    frame.Right.Z * frame.Up.X - frame.Right.X * frame.Up.Z,
                    frame.Right.X * frame.Up.Y - frame.Right.Y * frame.Up.X), Is.EqualTo(frame.Normal));
            }
        }

        [Test]
        public void CornersHaveTwoDistinctCardinalWrapsAndNoDiagonalShortcut()
        {
            var topology = new CubeTopology(6);
            foreach (CubeFace face in Enum.GetValues(typeof(CubeFace)))
            foreach (int x in new[] { 0, 5 })
            foreach (int y in new[] { 0, 5 })
            {
                var cell = new SurfaceCoordinate(face, x, y);
                var horizontal = topology.Step(cell, x == 0 ? SurfaceDirection.Left : SurfaceDirection.Right);
                var vertical = topology.Step(cell, y == 0 ? SurfaceDirection.Down : SurfaceDirection.Up);
                Assert.That(horizontal.CrossedEdge && vertical.CrossedEdge, Is.True);
                Assert.That(horizontal.Coordinate.Face, Is.Not.EqualTo(vertical.Coordinate.Face));
                Assert.That(topology.TryGetDirection(horizontal.Coordinate, vertical.Coordinate, out _), Is.True,
                    "Three corner-adjacent cells form three legal edge links, each requiring its own step.");
            }
            Assert.That(topology.TryGetDirection(new SurfaceCoordinate(CubeFace.Front, 2, 2),
                new SurfaceCoordinate(CubeFace.Front, 3, 3), out _), Is.False);
            Assert.That(topology.TryGetDirection(new SurfaceCoordinate(CubeFace.Front, 5, 5),
                new SurfaceCoordinate(CubeFace.Top, 4, 0), out _), Is.False);
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(6)]
        public void StraightTraversalWrapsFourFacesAndReturnsWithoutExiting(int size)
        {
            var topology = new CubeTopology(size);
            var origin = new SurfaceCoordinate(CubeFace.Front, 0, size / 2);
            var cell = origin;
            var direction = SurfaceDirection.Right;
            int crossings = 0;
            for (int i = 0; i < size * 4; i++)
            {
                var step = topology.Step(cell, direction);
                if (step.CrossedEdge) crossings++;
                cell = step.Coordinate;
                direction = step.Direction;
            }
            Assert.That(cell, Is.EqualTo(origin));
            Assert.That(direction, Is.EqualTo(SurfaceDirection.Right));
            Assert.That(crossings, Is.EqualTo(4));
        }

        [Test]
        public void InvalidSizesCoordinatesFacesAndDirectionsFailExplicitly()
        {
            var topology = new CubeTopology(6);
            Assert.Throws<ArgumentOutOfRangeException>(() => new CubeTopology(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CubeTopology(18919));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SurfaceCoordinate((CubeFace)6, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SurfaceCoordinate(CubeFace.Front, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => topology.Step(new SurfaceCoordinate(CubeFace.Top, 6, 0), SurfaceDirection.Up));
            Assert.Throws<ArgumentOutOfRangeException>(() => topology.Step(default, (SurfaceDirection)4));
            Assert.Throws<ArgumentOutOfRangeException>(() => topology.GetOrientation((CubeFace)(-1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => topology.GetCoordinate(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => topology.GetCoordinate(topology.CellCount));
            Assert.Throws<ArgumentException>(() => topology.GetDoubledEdgeMidpoint(new SurfaceCoordinate(CubeFace.Front, 2, 2), SurfaceDirection.Right));
            Assert.That(new CubeTopology(18918).CellCount, Is.GreaterThan(0));
        }

        private static int Dot(GridVector3 a, GridVector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    }
}
