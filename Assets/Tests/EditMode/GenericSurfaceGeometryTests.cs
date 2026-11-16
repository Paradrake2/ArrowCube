using System;
using System.Collections.Generic;
using System.Linq;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using ArrowCube.Topology;
using NUnit.Framework;
using UnityEngine;

namespace ArrowCube.Tests
{
    public class GenericSurfaceGeometryTests
    {
        [TestCase(false)] [TestCase(true)]
        public void AuthoredSolidsHaveValidSharedBoundariesNormalsAndVerifiedPuzzles(bool folded)
        {
            var demo=folded ? AuthoredSurfaceDemos.CreateFolded() : AuthoredSurfaceDemos.CreateL();
            var geometry=demo.Geometry;
            Assert.That(geometry.Surface.CellCount, Is.EqualTo(folded ? 12 : 14));
            int concave=0,oblique=0;
            foreach(int id in geometry.Surface.EnumerateCellIds())
            {
                var quad=geometry.GetCell(id); Assert.That(quad.Normal.magnitude, Is.EqualTo(1).Within(.00001f));
                for(int i=0;i<4;i++) Assert.That(Vector3.Dot(quad.Corner(i)-quad.Center,quad.Normal), Is.EqualTo(0).Within(.00001f));
                for(int d=0;d<4;d++)
                {
                    var direction=(SurfaceDirection)d; var link=geometry.Surface.GetBoundary(id,direction);
                    var neighbor=geometry.GetCell(link.NeighborCellId.Value);
                    Assert.That(Vector3.Distance(quad.BoundaryCenter(direction),neighbor.BoundaryCenter(link.NewDirection.Opposite())), Is.LessThan(.00001f));
                    var raised=geometry.GetLiftedBoundary(id,direction,.025f);
                    Assert.That(Vector3.Dot(raised-quad.Center,quad.Normal), Is.EqualTo(.025f).Within(.00001f));
                    Assert.That(Vector3.Dot(raised-neighbor.Center,neighbor.Normal), Is.EqualTo(.025f).Within(.00001f));
                    float dot=Vector3.Dot(quad.Normal,neighbor.Normal);
                    if(dot<.999f && Vector3.Dot(neighbor.Normal,quad.Direction(direction))<0) { concave++; Assert.That(link.AllowsEdgeExit, Is.False); }
                    if(Mathf.Abs(dot)>.05f && dot<.95f) oblique++;
                }
            }
            Assert.That(folded ? oblique : concave, Is.GreaterThan(0));
            var validation=new PuzzleValidator().Validate(demo.Puzzle,new[] {20,10,30});
            Assert.That(validation.Succeeded, Is.True,validation.Error); Assert.That(validation.Statistics.DependencyCount, Is.GreaterThanOrEqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void RailSamplesStayOnEachOffsetPlaneAndAreContinuousAcrossEveryFold(bool folded)
        {
            var demo=folded ? AuthoredSurfaceDemos.CreateFolded() : AuthoredSurfaceDemos.CreateL();
            var path=demo.Puzzle.Paths[0];
            var plan=new PathMovementSimulator().Simulate(new SurfaceGrid(demo.Puzzle.Surface,new[] {path}),10,demo.Puzzle.Exits,demo.Puzzle.Rules);
            var rail=SurfaceRail.FromPlan(plan); var animation=new PathAnimationGeometry(demo.Geometry); const float lift=.02f;
            for(int i=0;i<rail.Count-1;i++)
            {
                if(!rail[i].IsOnSurface || !rail[i+1].IsOnSurface) continue;
                float split=animation.BoundaryFraction(rail,i);
                var expected=demo.Geometry.GetLiftedBoundary(rail[i].CellId,rail.Directions[i],lift);
                Assert.That(Vector3.Distance(animation.Evaluate(rail,i+split,lift),expected), Is.LessThan(.00001f));
                Assert.That(Vector3.Distance(animation.Evaluate(rail,i+split-.00001f,lift),animation.Evaluate(rail,i+split+.00001f,lift)), Is.LessThan(.001f));
                for(int sample=0;sample<=20;sample++)
                {
                    float fraction=sample/20f; var q=demo.Geometry.GetCell(fraction<split ? rail[i].CellId : rail[i+1].CellId);
                    Assert.That(Vector3.Dot(animation.Evaluate(rail,i+fraction,lift)-q.Center,q.Normal), Is.EqualTo(lift).Within(.00002f));
                }
            }
            var points=new List<Vector3>(); var normals=new List<Vector3>();
            for(float progress=0;progress<=plan.StepCount;progress+=.125f)
            {
                animation.WritePolyline(rail,path.CellIds.Count,progress,points,lift,normals);
                Assert.That(points.Count-1, Is.EqualTo(normals.Count));
                Assert.That(Vector3.Distance(points[0],animation.Evaluate(rail,path.CellIds.Count-1+progress,lift)), Is.LessThan(.00001f));
                Assert.That(Vector3.Distance(points[points.Count-1],animation.Evaluate(rail,progress,lift)), Is.LessThan(.00001f));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void GateAndEligibleEdgeDeparturesUseTheDepartureCellsLocalFrame(bool folded)
        {
            var demo=folded ? AuthoredSurfaceDemos.CreateFolded() : AuthoredSurfaceDemos.CreateL(); var path=demo.Puzzle.Paths[0];
            foreach(var rules in new[] {ExitRules.GateOnly,ExitRules.Hybrid})
            {
                var plan=new PathMovementSimulator().Simulate(new SurfaceGrid(demo.Puzzle.Surface,new[] {path}),path.Id,demo.Puzzle.Exits,rules);
                var rail=SurfaceRail.FromPlan(plan); var animation=new PathAnimationGeometry(demo.Geometry);
                int first=0; while(rail[first].IsOnSurface) first++;
                var departure=rail[first]; var vector=animation.Evaluate(rail,first)-animation.Evaluate(rail,first-1);
                var expected=departure.ExitType==ExitType.DirectionalGate ? demo.Geometry.GetCellNormal(departure.CellId)
                    : demo.Geometry.GetDirection(departure.CellId,departure.ExitDirection);
                Assert.That(Vector3.Dot(vector.normalized,expected), Is.GreaterThan(.99999f));
            }
        }

        [Test]
        public void RibbonJoinsAtFoldsStayInBothPlanesAndKeepTheirProjectedWidth()
        {
            foreach(var demo in new[] {AuthoredSurfaceDemos.CreateL(),AuthoredSurfaceDemos.CreateFolded()})
            {
                var path=demo.Puzzle.Paths[0];
                var plan=new PathMovementSimulator().Simulate(new SurfaceGrid(demo.Puzzle.Surface,new[] {path}),10,demo.Puzzle.Exits,demo.Puzzle.Rules);
                var rail=SurfaceRail.FromPlan(plan); var animation=new PathAnimationGeometry(demo.Geometry);
                var points=new List<Vector3>(); var normals=new List<Vector3>();
                for(float progress=0;progress<plan.StepCount;progress+=.125f)
                {
                    animation.WritePolyline(rail,path.CellIds.Count,progress,points,.02f,normals);
                    for(int i=1;i<points.Count-1;i++)
                    {
                        if(Vector3.Dot(normals[i-1],normals[i])>.999f) continue;
                        var offset=SurfaceStrokeGeometry.Offset(points,normals,i,.1f);
                        Assert.That(Vector3.Dot(offset,normals[i-1]), Is.EqualTo(0).Within(.00001f));
                        Assert.That(Vector3.Dot(offset,normals[i]), Is.EqualTo(0).Within(.00001f));
                        var side=Vector3.Cross(normals[i-1],(points[i]-points[i-1]).normalized).normalized;
                        Assert.That(Mathf.Abs(Vector3.Dot(offset,side)), Is.EqualTo(.1f).Within(.0001f));
                    }
                }
            }
        }

        [Test]
        public void NonRightAngleConcaveFoldUsesTheIntersectionOfBothOffsetPlanes()
        {
            var demo=AuthoredSurfaceDemos.CreateL(); var cells=new Dictionary<int,SurfaceQuad>();
            Vector3 Shear(Vector3 p) => new Vector3(p.x+.35f*p.y,p.y,p.z);
            foreach(int id in demo.Puzzle.Surface.EnumerateCellIds())
            {
                var q=demo.Geometry.GetCell(id);
                cells.Add(id,new SurfaceQuad(Shear(q.Corner(0)),Shear(q.Corner(1)),Shear(q.Corner(2)),Shear(q.Corner(3)),q.Color));
            }
            var geometry=new QuadSurfaceGeometry(demo.Puzzle.Surface,cells); var a=geometry.GetCell(103); var b=geometry.GetCell(104);
            Assert.That(Vector3.Dot(a.Normal,b.Normal), Is.LessThan(-.1f));
            Assert.That(geometry.Surface.GetBoundary(103,SurfaceDirection.Right).AllowsEdgeExit, Is.False);
            const float lift=.03f; var seam=geometry.GetLiftedBoundary(103,SurfaceDirection.Right,lift);
            Assert.That(Vector3.Dot(seam-a.Center,a.Normal), Is.EqualTo(lift).Within(.00001f));
            Assert.That(Vector3.Dot(seam-b.Center,b.Normal), Is.EqualTo(lift).Within(.00001f));
            var naive=geometry.GetBoundaryPosition(103,SurfaceDirection.Right)+(a.Normal+b.Normal)*lift;
            Assert.That(Mathf.Abs(Vector3.Dot(naive-a.Center,a.Normal)-lift), Is.GreaterThan(.001f),"The cube's unscaled normal sum is insufficient at this inside fold.");
        }

        [Test]
        public void ActualSurfaceOcclusionLeavesTheLNotchEmpty()
        {
            var demo=AuthoredSurfaceDemos.CreateL();
            Assert.That(demo.Geometry.Raycast(new Ray(new Vector3(1,1,10),Vector3.back),out _), Is.False,"The empty notch must not be treated as a solid bounding box.");
            Assert.That(demo.Geometry.Raycast(new Ray(new Vector3(1,-1,10),Vector3.back),out float distance), Is.True);
            Assert.That(distance, Is.EqualTo(8.5f).Within(.00001f));
        }

        [Test]
        public void QuadValidationRejectsNonPlanarityAndGeometryTopologyMismatch()
        {
            Assert.Throws<ArgumentException>(()=>new SurfaceQuad(Vector3.zero,Vector3.right,new Vector3(1,1,.2f),Vector3.up,Color.white));
            Assert.Throws<ArgumentException>(()=>new SurfaceQuad(Vector3.zero,Vector3.zero,Vector3.one,Vector3.up,Color.white));
            var demo=AuthoredSurfaceDemos.CreateL(); var quads=demo.Geometry.Surface.EnumerateCellIds().ToDictionary(id=>id,id=>demo.Geometry.GetCell(id));
            var q=quads[100]; quads[100]=new SurfaceQuad(q.Corner(0)+Vector3.one,q.Corner(1)+Vector3.one,q.Corner(2)+Vector3.one,q.Corner(3)+Vector3.one,q.Color);
            Assert.Throws<ArgumentException>(()=>new QuadSurfaceGeometry(demo.Puzzle.Surface,quads));
        }

        [Test]
        public void UnequalCellSizesAndSkewPlanarQuadsHaveLocalDimensionsAndOppositeDirections()
        {
            var quad=new SurfaceQuad(new Vector3(0,0,0),new Vector3(3,.4f,0),new Vector3(2.5f,2,0),new Vector3(.2f,1.4f,0),Color.white);
            Assert.That(Vector3.Dot(quad.Direction(SurfaceDirection.Right),quad.Direction(SurfaceDirection.Left)), Is.EqualTo(-1).Within(.00001f));
            Assert.That(Vector3.Dot(quad.Direction(SurfaceDirection.Up),quad.Direction(SurfaceDirection.Down)), Is.EqualTo(-1).Within(.00001f));
            Assert.That(quad.Size, Is.GreaterThan(0));
            var demo=AuthoredSurfaceDemos.CreateL(); Assert.That(demo.Geometry.GetCell(100).Size, Is.Not.EqualTo((demo.Geometry.GetCell(100).Corner(3)-demo.Geometry.GetCell(100).Corner(0)).magnitude));
        }
    }
}
