using System;
using System.Collections.Generic;
using System.Linq;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using ArrowCube.Topology;
using NUnit.Framework;
using UnityEngine;

namespace ArrowCube.Tests
{
    public class PathAnimationGeometryTests
    {
        [Test]
        public void FractionalMotionPreservesEveryBendAndEveryCubeSeam()
        {
            var puzzle = ManualPuzzles.Create(1);
            var plan = new PathMovementSimulator().Simulate(puzzle.CreateGrid(),10,puzzle.Exits,puzzle.Rules);
            var geometry = new CubeSurfaceGeometry(puzzle.Topology,4);
            var animation = new PathAnimationGeometry(geometry);
            var points = new List<Vector3>();
            for (float progress = 0; progress <= plan.StepCount; progress += 0.125f)
            {
                animation.WritePolyline(plan.Rail,plan.Path.Cells.Count,progress,points);
                Assert.That(points[0], Is.EqualTo(animation.Evaluate(plan.Rail,plan.Path.Cells.Count-1+progress)));
                Assert.That(points[points.Count-1], Is.EqualTo(animation.Evaluate(plan.Rail,progress)));
                // Any segment still touching the cube remains on its exterior, never cutting through a corner.
                for (int i = 1; i < points.Count; i++)
                {
                    var midpoint = (points[i-1]+points[i])*0.5f;
                    Assert.That(MaxAbs(midpoint), Is.GreaterThanOrEqualTo(2-0.00001f));
                }
                for (int knot = Mathf.CeilToInt(progress); knot < plan.Path.Cells.Count-1+progress; knot++)
                {
                    var expected = animation.Evaluate(plan.Rail,knot);
                    Assert.That(points.Any(p => Vector3.Distance(p,expected) < 0.00001f), Is.True,
                        $"Body skipped route bend {knot} at progress {progress}.");
                }
            }
        }

        [Test]
        public void ASeamUsesItsExactSharedMidpointAndKeepsMotionContinuous()
        {
            var t = new CubeTopology(6);
            var from = new SurfaceCoordinate(CubeFace.Right,2,5);
            var next = t.Step(from,SurfaceDirection.Up);
            var rail = new[] { new PathPosition(from),new PathPosition(next.Coordinate) };
            var geometry = new CubeSurfaceGeometry(t,4);
            var animation = new PathAnimationGeometry(geometry);
            var expected = CubeSurfaceGeometry.ToVector(t.GetDoubledEdgeMidpoint(from,SurfaceDirection.Up))*(geometry.CellSize*0.5f);
            Assert.That(Vector3.Distance(animation.Evaluate(rail,0.5f),expected), Is.LessThan(0.00001f));
            Assert.That(Vector3.Distance(animation.Evaluate(rail,0.4999f),animation.Evaluate(rail,0.5001f)), Is.LessThan(0.001f));
            Assert.That(Vector3.Dot(animation.Evaluate(rail,0.25f),Vector3.right), Is.EqualTo(2).Within(0.00001f));
            Assert.That(Vector3.Dot(animation.Evaluate(rail,0.75f),Vector3.up), Is.EqualTo(2).Within(0.00001f));
        }

        [Test]
        public void DepartureFollowsTheNormalAndDoesNotJumpOrShrinkTheBody()
        {
            var t = new CubeTopology(6);
            var gate = new SurfaceCoordinate(CubeFace.Top,2,2);
            var rail = new[] { new PathPosition(gate),new PathPosition(gate,1),new PathPosition(gate,2) };
            var geometry = new CubeSurfaceGeometry(t,6);
            var animation = new PathAnimationGeometry(geometry);
            Assert.That(animation.Evaluate(rail,0.5f)-animation.Evaluate(rail,0), Is.EqualTo(Vector3.up*0.5f));
            var points = new List<Vector3>();
            animation.WritePolyline(rail,2,1,points);
            Assert.That(Vector3.Distance(points[0],points[points.Count-1]), Is.EqualTo(1).Within(0.00001f));
        }

        [Test]
        public void FractionalSlideReversalReturnsTheExactOriginalPolyline()
        {
            var p = ManualPuzzles.Create(2);
            var plan = new PathMovementSimulator().Simulate(p.CreateGrid(),10,p.Exits,p.Rules);
            var geometry = new CubeSurfaceGeometry(p.Topology,4);
            var animation = new PathAnimationGeometry(geometry);
            var points = new List<Vector3>();
            var original = new List<Vector3>();
            geometry.WritePolyline(plan.Path,original,0.02f);
            animation.WritePolyline(plan.Rail,plan.Path.Cells.Count,0.15f,points,0.02f);
            Assert.That(points[0], Is.Not.EqualTo(original[0]));
            animation.WritePolyline(plan.Rail,plan.Path.Cells.Count,0,points,0.02f);
            Assert.That(points.Count, Is.EqualTo(original.Count));
            for (int i = 0; i < points.Count; i++) Assert.That(Vector3.Distance(points[i],original[i]), Is.LessThan(0.00001f));
        }

        [Test]
        public void InvalidProgressIsRejectedAndOneCellPathsHaveOneBodySample()
        {
            var t = new CubeTopology(3);
            var animation = new PathAnimationGeometry(new CubeSurfaceGeometry(t,4));
            var rail = new[] { new PathPosition(new SurfaceCoordinate(CubeFace.Front,0,0)) };
            var points = new List<Vector3>();
            animation.WritePolyline(rail,1,0,points);
            Assert.That(points.Count, Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => animation.Evaluate(rail,float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => animation.WritePolyline(rail,1,0.1f,points));
        }

        private static float MaxAbs(Vector3 p) => Mathf.Max(Mathf.Abs(p.x),Mathf.Abs(p.y),Mathf.Abs(p.z));
    }
}
