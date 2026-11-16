using System.Collections.Generic;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using ArrowCube.Topology;
using NUnit.Framework;
using UnityEngine;

namespace ArrowCube.Tests
{
    public class SurfaceStrokeTests
    {
        [TestCase(CubeFace.Front)] [TestCase(CubeFace.Right)] [TestCase(CubeFace.Back)]
        [TestCase(CubeFace.Left)] [TestCase(CubeFace.Top)] [TestCase(CubeFace.Bottom)]
        public void SquareCornerHasConstantPerpendicularWidthAndStaysOnItsFace(CubeFace face)
        {
            var t=new CubeTopology(6); var geometry=new CubeSurfaceGeometry(t,4);
            var cells=new[] { new SurfaceCoordinate(face,1,1),new SurfaceCoordinate(face,2,1),new SurfaceCoordinate(face,2,2) };
            var points=new List<Vector3>(); foreach(var cell in cells) points.Add(geometry.GetLocalPosition(cell,.02f));
            var n=CubeSurfaceGeometry.ToVector(t.GetOrientation(face).Normal);
            var normals=new[] { n,n }; float half=.1f;
            var offset=SurfaceStrokeGeometry.Offset(points,normals,1,half);
            Assert.That(Mathf.Abs(Vector3.Dot(offset,Vector3.Cross(n,(points[1]-points[0]).normalized))), Is.EqualTo(half).Within(.00001f));
            Assert.That(Mathf.Abs(Vector3.Dot(offset,Vector3.Cross(n,(points[2]-points[1]).normalized))), Is.EqualTo(half).Within(.00001f));
            Assert.That(Vector3.Dot(offset,n), Is.EqualTo(0).Within(.00001f));
            Assert.That(offset.magnitude, Is.EqualTo(half*Mathf.Sqrt(2)).Within(.00001f));
        }

        [Test]
        public void EveryCubeSeamSharesAFlatFixedWidthCrossSection()
        {
            var t=new CubeTopology(6); var geometry=new CubeSurfaceGeometry(t,4);
            foreach(var cell in t.EnumerateCells())
            for(int d=0;d<4;d++)
            {
                var next=t.Step(cell,(SurfaceDirection)d); if(!next.CrossedEdge) continue;
                var path=new ArrowPath(0,t,new[] { next.Coordinate,cell },next.Direction);
                var rail=new[] { new PathPosition(cell),new PathPosition(next.Coordinate) };
                var points=new List<Vector3>(); var normals=new List<Vector3>();
                new PathAnimationGeometry(geometry).WritePolyline(rail,2,0,points,.02f,normals,path.HeadDirection);
                Assert.That(normals.Count, Is.EqualTo(2));
                var offset=SurfaceStrokeGeometry.Offset(points,normals,1,.1f);
                Assert.That(offset.magnitude, Is.EqualTo(.1f).Within(.00001f));
                Assert.That(Vector3.Dot(offset,normals[0]), Is.EqualTo(0).Within(.00001f));
                Assert.That(Vector3.Dot(offset,normals[1]), Is.EqualTo(0).Within(.00001f));
            }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void AnimatedGeneratedRibbonsStayFiniteAndTheirMeshesDoNotDependOnCameraAngle(int mode)
        {
            var rules=mode==0 ? ExitRules.EdgeOnly : mode==1 ? ExitRules.GateOnly : ExitRules.Hybrid;
            var puzzle=new PuzzleGenerator().Generate(new PuzzleRecipe(7,DifficultyProfiles.All[0],rules));
            var geometry=new CubeSurfaceGeometry(puzzle.Topology,4); var animation=new PathAnimationGeometry(geometry);
            var grid=puzzle.CreateGrid(); var simulator=new PathMovementSimulator();
            var bodyObject=new GameObject("Ribbon geometry test"); var headObject=new GameObject("Head geometry test");
            var cameraObject=new GameObject("Camera geometry test");
            var material=new Material(Shader.Find("Unlit/Color"));
            try
            {
                var renderer=bodyObject.AddComponent<SurfacePathRenderer>(); renderer.Initialize(material);
                var head=headObject.AddComponent<TriangleArrowHead>(); head.Initialize(material);
                var camera=cameraObject.AddComponent<Camera>();
                var points=new List<Vector3>(); var normals=new List<Vector3>();
                foreach(int id in puzzle.VerifiedSolution)
                {
                    var plan=simulator.Simulate(grid,id,puzzle.Exits,puzzle.Rules);
                    for(float progress=0;progress<=plan.StepCount;progress+=.25f)
                    {
                        animation.WritePolyline(plan.Rail,plan.Path.Cells.Count,progress,points,.02f,normals,plan.Path.HeadDirection);
                        float distance=plan.Path.Cells.Count-1+progress;
                        Vector3 forward=distance>0 ? (animation.Evaluate(plan.Rail,distance,.02f)-animation.Evaluate(plan.Rail,distance-.0001f,.02f)).normalized
                            : geometry.GetLocalDirection(plan.Path.Head.Face,plan.Path.HeadDirection);
                        var fallback=distance>0 ? animation.GetSegmentNormal(plan.Rail,distance-.0001f,plan.Path.HeadDirection)
                            : CubeSurfaceGeometry.ToVector(puzzle.Topology.GetOrientation(plan.Path.Head.Face).Normal);
                        head.DrawAndTrimBody(points,normals,forward,fallback,geometry.CellSize,.2f);
                        renderer.Draw(points,normals,.2f);
                        var original=bodyObject.GetComponent<MeshFilter>().sharedMesh.vertices;
                        foreach(var vertex in original)
                            Assert.That(float.IsNaN(vertex.x)||float.IsInfinity(vertex.x)||float.IsNaN(vertex.y)||float.IsInfinity(vertex.y)||float.IsNaN(vertex.z)||float.IsInfinity(vertex.z), Is.False);
                        camera.transform.position=new Vector3(10,.2f,-8); camera.transform.LookAt(Vector3.zero);
                        renderer.Draw(points,normals,.2f);
                        Assert.That(bodyObject.GetComponent<MeshFilter>().sharedMesh.vertices, Is.EqualTo(original));
                        for(int i=0;i<points.Count;i++)
                            Assert.That(Vector3.Distance(original[i*2],points[i]), Is.LessThanOrEqualTo(.2f+.00001f),"Joins must never flare into a long spike.");
                    }
                    Assert.That(grid.TryCommit(plan), Is.True);
                }
            }
            finally { Object.DestroyImmediate(bodyObject); Object.DestroyImmediate(headObject); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(material); }
        }
    }
}
