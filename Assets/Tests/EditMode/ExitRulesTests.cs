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
    public class ExitRulesTests
    {
        private readonly PathMovementSimulator simulator = new PathMovementSimulator();

        [Test]
        public void NewRulesAndAnUnspecifiedSimulationDefaultToBothExitMethods()
        {
            var rules = new ExitRules();
            Assert.That(rules.EnableCubeEdgeExits && rules.EnableDirectionalGates, Is.True);
            var p = ManualPuzzles.Create(0);
            var plan = simulator.Simulate(p.CreateGrid(),10,p.Exits);
            Assert.That(plan.Rules.EnableCubeEdgeExits && plan.Rules.EnableDirectionalGates, Is.True);
            Assert.That(plan.ExitType, Is.EqualTo(ExitType.CubeEdge));
            Assert.That(ManualPuzzles.Create(2).Rules, Is.SameAs(ExitRules.GateOnly));
            Assert.That(ManualPuzzles.Create(5).Rules, Is.SameAs(ExitRules.EdgeOnly));
            Assert.That(ManualPuzzles.Create(6).Rules, Is.SameAs(ExitRules.Hybrid));
        }

        [TestCase(true, false, ExitType.CubeEdge)]
        [TestCase(false, true, ExitType.DirectionalGate)]
        [TestCase(true, true, ExitType.DirectionalGate)]
        public void IndependentSwitchesControlTheSamePathWithAnInteriorGate(bool edges, bool gates, ExitType expected)
        {
            var t = new CubeTopology(6);
            var path = new ArrowPath(1,t,new[] { C(CubeFace.Front,1,2),C(CubeFace.Front,0,2) },SurfaceDirection.Right);
            var grid = new SurfaceGrid(t,new[] { path });
            var map = new ExitMap(t,new[] { new ExitGate(C(CubeFace.Front,3,2),SurfaceDirection.Right) });
            var plan = simulator.Simulate(grid,1,map,new ExitRules(edges,gates));
            Assert.That(plan.Succeeded, Is.True);
            Assert.That(plan.ExitType, Is.EqualTo(expected));
            Assert.That(plan.StepCount, Is.EqualTo(expected == ExitType.CubeEdge ? 6 : 4));
            Assert.That(plan.GetPosition(plan.StepCount,1).ExitType, Is.EqualTo(expected));
        }

        [Test]
        public void BothDisabledFindsNoExitEvenWhenACompatibleGateExists()
        {
            var p = ManualPuzzles.Create(0);
            var grid = p.CreateGrid();
            var plan = simulator.Simulate(grid,10,p.Exits,ExitRules.None);
            Assert.That(plan.Outcome, Is.EqualTo(MovementOutcome.NoExit));
            Assert.That(plan.ExitType, Is.Null);
            Assert.That(grid.TryCommit(plan), Is.False);
        }

        [TestCase(CubeFace.Front)]
        [TestCase(CubeFace.Right)]
        [TestCase(CubeFace.Back)]
        [TestCase(CubeFace.Left)]
        [TestCase(CubeFace.Top)]
        [TestCase(CubeFace.Bottom)]
        public void EveryFaceAndHeadingExitsTangentiallyWithoutWrappingItsHead(CubeFace face)
        {
            var t = new CubeTopology(4);
            var map = new ExitMap(t,Array.Empty<ExitGate>());
            for (int d = 0; d < 4; d++)
            {
                var direction = (SurfaceDirection)d;
                var head = C(face,1,1);
                var neck = t.Step(head,direction.Opposite()).Coordinate;
                var path = new ArrowPath(1,t,new[] { head,neck },direction);
                var grid = new SurfaceGrid(t,new[] { path });
                var plan = simulator.Simulate(grid,1,map,ExitRules.EdgeOnly);
                Assert.That(plan.Succeeded, Is.True);
                Assert.That(plan.ExitType, Is.EqualTo(ExitType.CubeEdge));
                Assert.That(plan.Rail.All(p => p.Cell.Face == face), Is.True);
                int surfaceMoves = direction == SurfaceDirection.Right || direction == SurfaceDirection.Up ? 2 : 1;
                Assert.That(plan.StepCount, Is.EqualTo(surfaceMoves + path.Cells.Count));
                var firstOutside = plan.GetPosition(surfaceMoves+1,0);
                Assert.That(firstOutside.ExitDistance, Is.EqualTo(1));
                Assert.That(firstOutside.ExitDirection, Is.EqualTo(direction));
                var geometry = new CubeSurfaceGeometry(t,4);
                var animation = new PathAnimationGeometry(geometry);
                var a = animation.Evaluate(plan.Rail,path.Cells.Count-1+surfaceMoves);
                var b = animation.Evaluate(plan.Rail,path.Cells.Count+surfaceMoves);
                Assert.That(Vector3.Distance(b-a,geometry.GetLocalDirection(face,direction)), Is.LessThan(0.00001f));
                for (int step = 1; step <= plan.StepCount; step++)
                for (int i = 1; i < path.Cells.Count; i++)
                    Assert.That(plan.GetPosition(step,i), Is.EqualTo(plan.GetPosition(step-1,i-1)));
                Assert.That(plan.GetPosition(plan.StepCount-1,1).IsOnSurface, Is.True);
                Assert.That(plan.GetPosition(plan.StepCount,1).IsOnSurface, Is.False);
                Assert.That(grid.TryCommit(plan), Is.True);
            }
        }

        [Test]
        public void BoundaryGateDoesNotOverrideAnOrdinaryEdgeExitInHybridMode()
        {
            var p = ManualPuzzles.Create(0); // Gate is at Front(5,2), pointing Right.
            var edge = simulator.Simulate(p.CreateGrid(),10,p.Exits,ExitRules.Hybrid);
            var gate = simulator.Simulate(p.CreateGrid(),10,p.Exits,ExitRules.GateOnly);
            Assert.That(edge.ExitType, Is.EqualTo(ExitType.CubeEdge));
            Assert.That(gate.ExitType, Is.EqualTo(ExitType.DirectionalGate));
            Assert.That(edge.StepCount, Is.EqualTo(gate.StepCount));
            var geometry = new PathAnimationGeometry(new CubeSurfaceGeometry(p.Topology,6));
            Assert.That(geometry.Evaluate(edge.Rail,6)-geometry.Evaluate(edge.Rail,5), Is.EqualTo(Vector3.right));
            Assert.That(geometry.Evaluate(gate.Rail,6)-geometry.Evaluate(gate.Rail,5), Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void WrongDirectionGateIsIgnoredWhileAnEnabledEdgeStillWorks()
        {
            var p = ManualPuzzles.Create(0);
            var map = new ExitMap(p.Topology,new[] { new ExitGate(C(CubeFace.Front,3,2),SurfaceDirection.Up) });
            Assert.That(simulator.Simulate(p.CreateGrid(),10,map,ExitRules.Hybrid).ExitType, Is.EqualTo(ExitType.CubeEdge));
            Assert.That(simulator.Simulate(p.CreateGrid(),10,map,ExitRules.GateOnly).Outcome, Is.EqualTo(MovementOutcome.NoExit));
        }

        [Test]
        public void ABlockerAcrossTheNextSeamIsNotOnTheEdgeDepartureTrajectory()
        {
            var t = new CubeTopology(6);
            var head = C(CubeFace.Right,2,5);
            var wrapped = t.Step(head,SurfaceDirection.Up);
            var mover = new ArrowPath(1,t,new[] { head,C(CubeFace.Right,2,4) },SurfaceDirection.Up);
            var blocker = new ArrowPath(2,t,new[] { wrapped.Coordinate },SurfaceDirection.Up);
            var grid = new SurfaceGrid(t,new[] { mover,blocker });
            var exits = new ExitMap(t,Array.Empty<ExitGate>());
            var edge = simulator.Simulate(grid,1,exits,ExitRules.EdgeOnly);
            Assert.That(edge.Succeeded, Is.True);
            Assert.That(edge.StepCount, Is.EqualTo(2));
            var wrap = simulator.Simulate(grid,1,exits,ExitRules.GateOnly);
            Assert.That(wrap.Outcome, Is.EqualTo(MovementOutcome.BlockedByPath));
            Assert.That(wrap.BlockingPathId, Is.EqualTo(2));
            Assert.That(grid.TryCommit(wrap), Is.False);
            Assert.That(grid.TryCommit(edge), Is.True);
            Assert.That(grid.Paths.ContainsKey(2), Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void LegacyGatePuzzlesKeepTheirExactGateBehaviorWhenEdgesAreDisabled(int fixture)
        {
            var p = ManualPuzzles.Create(fixture);
            var grid = p.CreateGrid();
            foreach (int id in fixture < 2 ? new[] { 10 } : new[] { 20,10 })
            {
                var plan = simulator.Simulate(grid,id,p.Exits,p.Rules);
                Assert.That(plan.ExitType, Is.EqualTo(ExitType.DirectionalGate));
                Assert.That(plan.Rail.Where(pos => !pos.IsOnSurface).All(pos => pos.ExitType == ExitType.DirectionalGate), Is.True);
                Assert.That(grid.TryCommit(plan), Is.True);
            }
            Assert.That(grid.Paths, Is.Empty);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void DiagramMatchesDependenciesWithBlueRemovableAtEveryStage(int blueRemovalStage)
        {
            var p = ManualPuzzles.Create(5);
            var grid = p.CreateGrid();
            Assert.That(p.Exits.Gates, Is.Empty);
            Assert.That(p.Paths.Single(path => path.Id==20).FaceTransitionCount, Is.EqualTo(1));
            Assert.That(p.Paths.Single(path => path.Id==40).FaceTransitionCount, Is.EqualTo(1));
            Assert.That(Predict(10).Succeeded, Is.True, "Red can exit immediately.");
            Assert.That(Predict(40).Succeeded, Is.True, "Blue is independent.");
            AssertBlocked(20,10);
            AssertBlocked(30,20);
            var dependencyOrder = new[] { 10,20,30 };
            for (int i = 0; i <= dependencyOrder.Length; i++)
            {
                if (grid.Paths.ContainsKey(40)) Assert.That(Predict(40).Succeeded, Is.True);
                if (i == blueRemovalStage) Remove(40);
                if (i == dependencyOrder.Length) break;
                Remove(dependencyOrder[i]);
                if (i == 0)
                {
                    Assert.That(Predict(20).Succeeded, Is.True);
                    AssertBlocked(30,20);
                }
                if (i == 1) Assert.That(Predict(30).Succeeded, Is.True);
            }
            Assert.That(grid.Paths, Is.Empty);
            Assert.That(grid.OccupiedCellCount, Is.Zero);
            Assert.That(grid.OccupiedSegmentCount, Is.Zero);

            MovementPlan Predict(int id) => simulator.Simulate(grid,id,p.Exits,p.Rules);
            void AssertBlocked(int id,int blocker)
            {
                long revision = grid.Revision;
                var plan = Predict(id);
                Assert.That(plan.Outcome, Is.EqualTo(MovementOutcome.BlockedByPath));
                Assert.That(plan.BlockingPathId, Is.EqualTo(blocker));
                Assert.That(plan.ExitType, Is.Null);
                Assert.That(grid.TryCommit(plan), Is.False);
                Assert.That(grid.Revision, Is.EqualTo(revision));
            }
            void Remove(int id)
            {
                var plan = Predict(id);
                Assert.That(plan.ExitType, Is.EqualTo(ExitType.CubeEdge));
                Assert.That(grid.TryCommit(plan), Is.True);
            }
        }

        [Test]
        public void OneHybridLevelUsesBothExitTypesWithoutConflicts()
        {
            var p = ManualPuzzles.Create(6);
            var grid = p.CreateGrid();
            foreach (int id in new[] { 40,10,20,30 })
            {
                var plan = simulator.Simulate(grid,id,p.Exits,p.Rules);
                Assert.That(plan.ExitType, Is.EqualTo(id == 40 ? ExitType.DirectionalGate : ExitType.CubeEdge));
                Assert.That(grid.TryCommit(plan), Is.True);
            }
            Assert.That(grid.Paths, Is.Empty);
        }

        [Test]
        public void FirstValidExitCanPrecedeABlockerButAGateCannotBypassItsOwnOccupant()
        {
            var t = new CubeTopology(6);
            var mover = new ArrowPath(1,t,new[] { C(CubeFace.Front,1,2),C(CubeFace.Front,0,2) },SurfaceDirection.Right);
            var blocker = new ArrowPath(2,t,new[] { C(CubeFace.Front,4,2) },SurfaceDirection.Up);
            var grid = new SurfaceGrid(t,new[] { mover,blocker });
            var beforeBlocker = new ExitMap(t,new[] { new ExitGate(C(CubeFace.Front,2,2),SurfaceDirection.Right) });
            var hybrid = simulator.Simulate(grid,1,beforeBlocker,ExitRules.Hybrid);
            Assert.That(hybrid.ExitType, Is.EqualTo(ExitType.DirectionalGate));
            var edge = simulator.Simulate(grid,1,beforeBlocker,ExitRules.EdgeOnly);
            Assert.That(edge.Outcome, Is.EqualTo(MovementOutcome.BlockedByPath));
            Assert.That(edge.StepCount, Is.EqualTo(2));
            Assert.That(edge.ExitType, Is.Null);
            var occupiedGate = new ExitMap(t,new[] { new ExitGate(blocker.Head,SurfaceDirection.Right) });
            var blocked = simulator.Simulate(grid,1,occupiedGate,ExitRules.Hybrid);
            Assert.That(blocked.Outcome, Is.EqualTo(MovementOutcome.BlockedByPath));
            Assert.That(blocked.BlockingPathId, Is.EqualTo(2));
            Assert.That(grid.TryCommit(blocked), Is.False);
            Assert.That(grid.OccupiedCellCount, Is.EqualTo(3));
        }

        [TestCase(5)]
        [TestCase(6)]
        public void CrossFaceBodyBendsSurviveFractionalMotionForBothExitTypes(int fixture)
        {
            var p = ManualPuzzles.Create(fixture);
            var grid = p.CreateGrid();
            var animation = new PathAnimationGeometry(new CubeSurfaceGeometry(p.Topology,4));
            var points = new List<Vector3>();
            foreach (int id in new[] { 10,20,30,40 })
            {
                var plan = simulator.Simulate(grid,id,p.Exits,p.Rules);
                Assert.That(plan.Succeeded, Is.True);
                for (float progress = 0; progress <= plan.StepCount; progress += 0.25f)
                {
                    animation.WritePolyline(plan.Rail,plan.Path.Cells.Count,progress,points);
                    for (int knot = Mathf.CeilToInt(progress); knot < plan.Path.Cells.Count-1+progress; knot++)
                        Assert.That(points.Any(point => Vector3.Distance(point,animation.Evaluate(plan.Rail,knot)) < 0.00001f), Is.True);
                    foreach (var point in points)
                        Assert.That(Mathf.Max(Mathf.Abs(point.x),Mathf.Abs(point.y),Mathf.Abs(point.z)), Is.GreaterThanOrEqualTo(2-0.00001f));
                }
                Assert.That(grid.TryCommit(plan), Is.True);
            }
        }

        private static SurfaceCoordinate C(CubeFace face,int x,int y) => new SurfaceCoordinate(face,x,y);
    }
}
