using System;
using System.Collections.Generic;
using System.Linq;
using ArrowCube.Topology;
using NUnit.Framework;

namespace ArrowCube.Tests
{
    public class ArrowPathTests
    {
        internal static ArrowPath ThreeFacePath()
        {
            // Head on Top, neck on Top, bent tail traverses Right and Front.
            return new ArrowPath(17, new CubeTopology(3), new[]
            {
                C(CubeFace.Top, 0, 0), C(CubeFace.Top, 1, 0), C(CubeFace.Top, 2, 0),
                C(CubeFace.Right, 0, 2), C(CubeFace.Right, 0, 1), C(CubeFace.Right, 0, 0),
                C(CubeFace.Front, 2, 0), C(CubeFace.Front, 1, 0)
            }, SurfaceDirection.Left);
        }

        [Test]
        public void OnePathRetainsOrderedCellsAndSegmentsAcrossThreeFaces()
        {
            var path = ThreeFacePath();
            Assert.That(path.Id, Is.EqualTo(17));
            Assert.That(path.Head, Is.EqualTo(C(CubeFace.Top, 0, 0)));
            Assert.That(path.Tail, Is.EqualTo(C(CubeFace.Front, 1, 0)));
            Assert.That(path.HeadDirection, Is.EqualTo(SurfaceDirection.Left));
            Assert.That(path.FaceTransitionCount, Is.EqualTo(2));
            Assert.That(path.Cells.Select(c => c.Face).Distinct().Count(), Is.EqualTo(3));
            Assert.That(path.Segments.Count, Is.EqualTo(path.Cells.Count - 1));
            Assert.That(path.Segments.Count(s => s.CrossesFace), Is.EqualTo(2));
        }

        [Test]
        public void HeadDirectionUsesTheHeadFaceAfterRotatingAcrossAnEdge()
        {
            var topology = new CubeTopology(3);
            var cells = new[] { C(CubeFace.Top, 2, 0), C(CubeFace.Right, 0, 2) };
            Assert.DoesNotThrow(() => new ArrowPath(1, topology, cells, SurfaceDirection.Left));
            Assert.Throws<ArgumentException>(() => new ArrowPath(1, topology, cells, SurfaceDirection.Up));
        }

        [Test]
        public void GeometryIsDefensivelyCopiedAndReadOnly()
        {
            var cells = new List<SurfaceCoordinate> { C(CubeFace.Front, 1, 0), C(CubeFace.Front, 0, 0) };
            var path = new ArrowPath(1, new CubeTopology(3), cells, SurfaceDirection.Right);
            cells.Clear();
            Assert.That(path.Cells.Count, Is.EqualTo(2));
            Assert.Throws<NotSupportedException>(() => ((IList<SurfaceCoordinate>)path.Cells).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<SurfaceSegment>)path.Segments).Clear());
        }

        [Test]
        public void DisconnectedDiagonalDuplicateAndOutOfBoundsPathsAreRejected()
        {
            var topology = new CubeTopology(3);
            Assert.Throws<ArgumentException>(() => new ArrowPath(0, topology, Array.Empty<SurfaceCoordinate>(), SurfaceDirection.Right));
            Assert.Throws<ArgumentException>(() => new ArrowPath(0, topology, new[] { C(CubeFace.Front, 0, 0), C(CubeFace.Back, 0, 0) }, SurfaceDirection.Right));
            Assert.Throws<ArgumentException>(() => new ArrowPath(0, topology, new[] { C(CubeFace.Front, 0, 0), C(CubeFace.Front, 1, 1) }, SurfaceDirection.Right));
            Assert.Throws<ArgumentException>(() => new ArrowPath(0, topology, new[] { C(CubeFace.Front, 0, 0), C(CubeFace.Front, 1, 0), C(CubeFace.Front, 0, 0) }, SurfaceDirection.Right));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArrowPath(0, topology, new[] { C(CubeFace.Front, 3, 0) }, SurfaceDirection.Right));
            Assert.Throws<ArgumentException>(() => new SurfaceSegment(topology, C(CubeFace.Front, 0, 0), C(CubeFace.Front, 0, 0)));
        }

        [Test]
        public void OneCellPathHasExplicitHeadingAndNoSegments()
        {
            var path = new ArrowPath(0, new CubeTopology(1), new[] { C(CubeFace.Bottom, 0, 0) }, SurfaceDirection.Up);
            Assert.That(path.Head, Is.EqualTo(path.Tail));
            Assert.That(path.Segments, Is.Empty);
        }

        private static SurfaceCoordinate C(CubeFace face, int x, int y) => new SurfaceCoordinate(face, x, y);
    }
}
