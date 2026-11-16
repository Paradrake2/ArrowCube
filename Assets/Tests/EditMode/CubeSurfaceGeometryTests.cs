using System;
using System.Collections.Generic;
using ArrowCube.Presentation;
using ArrowCube.Topology;
using NUnit.Framework;
using UnityEngine;

namespace ArrowCube.Tests
{
    public class CubeSurfaceGeometryTests
    {
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(6)]
        [TestCase(20)]
        public void EverySeamHasTwoSurfaceSegmentsAndNoGapOrInteriorChord(int size)
        {
            var topology = new CubeTopology(size);
            var geometry = new CubeSurfaceGeometry(topology, 4);
            var points = new List<Vector3>();
            foreach (var cell in topology.EnumerateCells())
            for (int d = 0; d < 4; d++)
            {
                var step = topology.Step(cell, (SurfaceDirection)d);
                if (!step.CrossedEdge) continue;
                var path = new ArrowPath(0, topology, new[] { step.Coordinate, cell }, step.Direction);
                geometry.WritePolyline(path, points);
                Assert.That(points.Count, Is.EqualTo(3));
                Assert.That(Vector3.Distance(points[0], points[1]), Is.EqualTo(geometry.CellSize * 0.5f).Within(0.00001f));
                Assert.That(Vector3.Distance(points[1], points[2]), Is.EqualTo(geometry.CellSize * 0.5f).Within(0.00001f));
                var aNormal = CubeSurfaceGeometry.ToVector(topology.GetOrientation(cell.Face).Normal);
                var bNormal = CubeSurfaceGeometry.ToVector(topology.GetOrientation(step.Coordinate.Face).Normal);
                Assert.That(Vector3.Dot(points[1], aNormal), Is.EqualTo(2).Within(0.00001f));
                Assert.That(Vector3.Dot(points[1], bNormal), Is.EqualTo(2).Within(0.00001f));
                geometry.WritePolyline(path, points, 0.02f);
                Assert.That(Vector3.Dot((points[0] + points[1]) * 0.5f, bNormal), Is.GreaterThanOrEqualTo(2));
                Assert.That(Vector3.Dot((points[1] + points[2]) * 0.5f, aNormal), Is.GreaterThanOrEqualTo(2));
            }
        }

        [Test]
        public void MultiFacePolylineIncludesEveryBendAndSeam()
        {
            var path = ArrowPathTests.ThreeFacePath();
            var geometry = new CubeSurfaceGeometry(new CubeTopology(3), 6);
            var points = new List<Vector3> { Vector3.one * 999 };
            geometry.WritePolyline(path, points);
            Assert.That(points.Count, Is.EqualTo(path.Cells.Count + path.FaceTransitionCount));
            Assert.That(points[0], Is.EqualTo(geometry.GetLocalPosition(path.Head)));
            Assert.That(points[points.Count - 1], Is.EqualTo(geometry.GetLocalPosition(path.Tail)));
        }

        [Test]
        public void WorldPositionRespectsTranslationRotationAndScale()
        {
            var cube = new GameObject("Geometry test");
            try
            {
                cube.transform.position = new Vector3(3, -2, 7);
                cube.transform.rotation = Quaternion.Euler(30, 65, -10);
                cube.transform.localScale = new Vector3(2, 3, 4);
                var geometry = new CubeSurfaceGeometry(new CubeTopology(6), 4);
                var cell = new SurfaceCoordinate(CubeFace.Bottom, 2, 3);
                var local = geometry.GetLocalPosition(cell, 0.02f);
                Assert.That(Vector3.Distance(geometry.GetWorldPosition(cell, cube.transform, 0.02f),
                    cube.transform.localToWorldMatrix.MultiplyPoint3x4(local)), Is.LessThan(0.00001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(cube); }
        }

        [Test]
        public void InvalidGeometryArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new CubeSurfaceGeometry(null, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CubeSurfaceGeometry(new CubeTopology(6), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CubeSurfaceGeometry(new CubeTopology(6), float.NaN));
            var geometry = new CubeSurfaceGeometry(new CubeTopology(3), 4);
            Assert.Throws<ArgumentOutOfRangeException>(() => geometry.WritePolyline(ArrowPathTests.ThreeFacePath(), new List<Vector3>(), -1));
        }
    }
}
