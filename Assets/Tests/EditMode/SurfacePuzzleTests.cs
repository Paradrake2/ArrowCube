using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Topology;
using NUnit.Framework;

namespace ArrowCube.Tests
{
    public class SurfacePuzzleTests
    {
        private readonly PathMovementSimulator simulator=new PathMovementSimulator();

        [Test]
        public void NonCubePuzzleHasBlockingChainAndVerifiedGateRemovalSolution()
        {
            var puzzle=ManualSurfacePuzzle.Create(); var grid=puzzle.CreateGrid(); long revision=grid.Revision;
            Assert.That(puzzle.Surface, Is.Not.InstanceOf<CubeTopology>()); Assert.That(puzzle.Surface.CellCount, Is.EqualTo(16));
            var a=simulator.Simulate(grid,10,puzzle.Exits,puzzle.Rules); var b=simulator.Simulate(grid,20,puzzle.Exits,puzzle.Rules);
            Assert.That(a.Outcome, Is.EqualTo(MovementOutcome.BlockedByPath)); Assert.That(a.BlockingPathId, Is.EqualTo(20)); Assert.That(a.BlockedCellId, Is.EqualTo(115));
            Assert.That(b.Outcome, Is.EqualTo(MovementOutcome.BlockedByPath)); Assert.That(b.BlockingPathId, Is.EqualTo(30)); Assert.That(b.BlockedCellId, Is.EqualTo(317));
            Assert.That(grid.TryCommit(a) || grid.TryCommit(b), Is.False); Assert.That(grid.Revision, Is.EqualTo(revision));
            var validation=new PuzzleValidator().Validate(puzzle,ManualSurfacePuzzle.RemovalOrder);
            Assert.That(validation.Succeeded, Is.True,validation.Error); Assert.That(validation.Solution, Is.EqualTo(new[] {30,20,10}));
            Assert.That(validation.Statistics.DependencyCount, Is.EqualTo(2)); Assert.That(validation.Statistics.DependencyDepth, Is.EqualTo(3));
            Assert.That(validation.Statistics.ImmediatelyRemovablePaths, Is.EqualTo(1));
            Assert.That(new PuzzleValidator().Validate(puzzle,new[] {10,20,30}).Succeeded, Is.False);
            Assert.That(new PuzzleValidator().Validate(puzzle).Solution, Is.EqualTo(ManualSurfacePuzzle.RemovalOrder));
            foreach(int id in ManualSurfacePuzzle.RemovalOrder)
            {
                var plan=simulator.Simulate(grid,id,puzzle.Exits,puzzle.Rules);
                Assert.That(plan.Succeeded, Is.True); Assert.That(plan.ExitType, Is.EqualTo(ExitType.DirectionalGate));
                for(int i=0;i<plan.Path.CellIds.Count;i++) Assert.That(plan.GetCellPosition(plan.StepCount,i).IsOnSurface, Is.False);
                Assert.That(grid.TryCommit(plan), Is.True); Assert.That(grid.TryCommit(plan), Is.False);
            }
            Assert.That(grid.Paths, Is.Empty); Assert.That(grid.OccupiedCellCount+grid.OccupiedSegmentCount, Is.Zero);
        }

        [TestCase(true,false,3,0)] [TestCase(false,true,0,3)] [TestCase(true,true,2,1)]
        public void SameSimulatorHandlesConvexConcaveAndIndependentExitModes(bool edges,bool gates,int expectedEdges,int expectedGates)
        {
            var puzzle=ManualSurfacePuzzle.Create(new ExitRules(edges,gates)); var grid=puzzle.CreateGrid(); int edgeCount=0,gateCount=0;
            foreach(int id in ManualSurfacePuzzle.RemovalOrder)
            {
                var plan=simulator.Simulate(grid,id,puzzle.Exits,puzzle.Rules); Assert.That(plan.Succeeded, Is.True);
                if(plan.ExitType==ExitType.CubeEdge) edgeCount++; else gateCount++;
                if(id==10)
                {
                    var first=plan.GetCellPosition(1,0);
                    Assert.That(first.IsOnSurface, Is.True,"The concave boundary at cell 101 cannot trigger an ordinary exit.");
                    Assert.That(first.CellId, Is.EqualTo(115));
                    if(edges) { Assert.That(plan.GetCellPosition(2,0).IsOnSurface, Is.False); Assert.That(plan.GetCellPosition(2,0).ExitDirection, Is.EqualTo(SurfaceDirection.Up)); }
                    else Assert.That(plan.GetCellPosition(2,0).CellId, Is.EqualTo(129),"Convex folds must be traversable when edge exits are disabled.");
                }
                Assert.That(grid.TryCommit(plan), Is.True);
            }
            Assert.That(edgeCount, Is.EqualTo(expectedEdges)); Assert.That(gateCount, Is.EqualTo(expectedGates));
        }

        [Test]
        public void EntireBentBodyFollowsTransportedHeadRouteAndReturnsFullTrajectoryVisits()
        {
            var puzzle=ManualSurfacePuzzle.Create(); var path=puzzle.Paths[0];
            var alone=new SurfaceGrid(puzzle.Surface,new[] {path}); var plan=simulator.Simulate(alone,10,puzzle.Exits,puzzle.Rules);
            Assert.That(plan.StepCount, Is.EqualTo(4));
            Assert.That(plan.GetCellPosition(1,0).CellId, Is.EqualTo(115)); Assert.That(plan.GetCellPosition(2,0).CellId, Is.EqualTo(129));
            for(int step=1;step<=plan.StepCount;step++)
                Assert.That(plan.GetCellPosition(step,1), Is.EqualTo(plan.GetCellPosition(step-1,0)));
            var trajectory=RemovalTrajectory.Calculate(puzzle.Surface,path,puzzle.Exits,puzzle.Rules);
            Assert.That(trajectory.SweptCellIds, Is.EquivalentTo(new[] {143,101,115,129}));
            Assert.That(trajectory.Contains(115), Is.True); Assert.That(trajectory.SweptCellSegments.Count, Is.EqualTo(3));
            Assert.That(trajectory.CellVisits.First(v=>v.CellId==115).FirstStep, Is.EqualTo(1));
            Assert.That(trajectory.GetCellBodyPosition(2,0).CellId, Is.EqualTo(129));
            Assert.Throws<NotSupportedException>(()=>((IList<CellPathPosition>)plan.CellRail).Clear());
            Assert.Throws<ArgumentOutOfRangeException>(()=>plan.GetCellPosition(-1,0));
            Assert.Throws<ArgumentOutOfRangeException>(()=>plan.GetCellPosition(0,2));
        }

        [Test]
        public void SelfCollisionAndSimultaneouslyVacatedTailRetainExistingRules()
        {
            var puzzle=ManualSurfacePuzzle.Create();
            var legal=new ArrowPath(7,puzzle.Surface,new[] {101,143,244,230,216,115},SurfaceDirection.Right);
            var blocked=new ArrowPath(8,puzzle.Surface,new[] {101,143,244,230,216,115,129},SurfaceDirection.Right);
            var legalPlan=simulator.Simulate(new SurfaceGrid(puzzle.Surface,new[] {legal}),7,puzzle.Exits,puzzle.Rules);
            Assert.That(legalPlan.Succeeded, Is.True); Assert.That(InitialExitClearance.IsClear(legalPlan), Is.False);
            var grid=new SurfaceGrid(puzzle.Surface,new[] {blocked});
            var self=simulator.Simulate(grid,8,puzzle.Exits,puzzle.Rules);
            Assert.That(self.Outcome, Is.EqualTo(MovementOutcome.SelfCollision)); Assert.That(self.StepCount, Is.Zero);
            Assert.That(self.BlockingPathId, Is.EqualTo(8)); Assert.That(self.BlockedCellId, Is.EqualTo(115)); Assert.That(grid.TryCommit(self), Is.False);
        }

        [Test]
        public void MissingOrWrongHeadingGateProducesNoExitAndDoesNotMutateTheBoard()
        {
            var puzzle=ManualSurfacePuzzle.Create(); var path=puzzle.Paths[2]; var grid=new SurfaceGrid(puzzle.Surface,new[] {path});
            var wrong=new ExitMap(puzzle.Surface,new[] {new CellGate(418,SurfaceDirection.Right)});
            Assert.That(simulator.Simulate(grid,30,wrong,ExitRules.GateOnly).Outcome, Is.EqualTo(MovementOutcome.NoExit));
            var plan=simulator.Simulate(grid,30,puzzle.Exits,ExitRules.None);
            Assert.That(plan.Outcome, Is.EqualTo(MovementOutcome.NoExit)); Assert.That(plan.StepCount, Is.EqualTo(4));
            Assert.That(grid.Paths.Count, Is.EqualTo(1)); Assert.That(grid.Revision, Is.EqualTo(1));
        }

        [Test]
        public void OccupancyIsAtomicUsesBoundaryIdentityAndRejectsForeignTopologyData()
        {
            var puzzle=ManualSurfacePuzzle.Create(); var grid=puzzle.CreateGrid(); long revision=grid.Revision;
            foreach(var path in puzzle.Paths)
            {
                foreach(int cell in path.CellIds) { Assert.That(grid.TryGetCellOwner(cell,out int owner), Is.True); Assert.That(owner, Is.EqualTo(path.Id)); }
                foreach(var segment in path.CellSegments)
                {
                    var reverse=new CellSegment(puzzle.Surface,segment.B,segment.DirectionFromB);
                    Assert.That(reverse, Is.EqualTo(segment)); Assert.That(reverse.GetHashCode(), Is.EqualTo(segment.GetHashCode()));
                    Assert.That(grid.TryGetSegmentOwner(reverse,out int owner), Is.True); Assert.That(owner, Is.EqualTo(path.Id));
                }
            }
            Assert.Throws<ArgumentException>(()=>grid.AddPath(new ArrowPath(99,puzzle.Surface,new[] {115},SurfaceDirection.Right)));
            var other=ManualSurfacePuzzle.Create();
            Assert.Throws<ArgumentException>(()=>grid.AddPath(other.Paths[0]));
            Assert.Throws<ArgumentException>(()=>simulator.Simulate(grid,10,other.Exits,puzzle.Rules));
            Assert.Throws<ArgumentException>(()=>grid.TryGetSegmentOwner(other.Paths[0].CellSegments[0],out _));
            Assert.That(grid.Revision, Is.EqualTo(revision)); Assert.That(grid.OccupiedCellCount, Is.EqualTo(6));
            var plan=simulator.Simulate(grid,30,puzzle.Exits,puzzle.Rules);
            Assert.That(other.CreateGrid().TryCommit(plan), Is.False);
            grid.RemovePath(10); Assert.That(grid.TryCommit(plan), Is.False,"Stale plans must be rejected.");
        }

        [Test]
        public void ParallelConnectionsHaveSeparateUndirectedSegmentKeys()
        {
            var builder=new SurfaceTopologyBuilder().AddCell(1).AddCell(2);
            builder.Connect(1,SurfaceDirection.Right,2,SurfaceDirection.Right).Connect(1,SurfaceDirection.Left,2,SurfaceDirection.Left);
            builder.Connect(1,SurfaceDirection.Up,1,SurfaceDirection.Up).Connect(2,SurfaceDirection.Up,2,SurfaceDirection.Up);
            var surface=builder.Build();
            var first=new CellSegment(surface,1,SurfaceDirection.Right); var second=new CellSegment(surface,1,SurfaceDirection.Left);
            Assert.That(first, Is.Not.EqualTo(second)); Assert.That(first, Is.EqualTo(new CellSegment(surface,2,SurfaceDirection.Left)));
            Assert.Throws<ArgumentException>(()=>new ArrowPath(1,surface,new[] {1,2},SurfaceDirection.Right),"Ambiguous cell-only body authoring must fail explicitly.");
            var explicitPath=new ArrowPath(2,surface,new[] {1,2},SurfaceDirection.Right,new[] {SurfaceDirection.Left});
            Assert.That(explicitPath.CellSegments[0], Is.EqualTo(second));
            Assert.That(new SurfaceGrid(surface,new[] {explicitPath}).TryGetSegmentOwner(second,out int owner), Is.True); Assert.That(owner, Is.EqualTo(2));
            var path=new ArrowPath(1,surface,new[] {1},SurfaceDirection.Right);
            var plan=simulator.Simulate(new SurfaceGrid(surface,new[] {path}),1,new ExitMap(surface,Array.Empty<CellGate>()),ExitRules.None);
            Assert.That(plan.TraversedSegments.Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void PathsAndGatesAreDefensivelyCopiedAndInvalidAuthoringIsRejected()
        {
            var puzzle=ManualSurfacePuzzle.Create(); var ids=new List<int> {101,143};
            var path=new ArrowPath(7,puzzle.Surface,ids,SurfaceDirection.Right); ids.Clear();
            Assert.That(path.CellIds, Is.EqualTo(new[] {101,143}));
            Assert.Throws<NotSupportedException>(()=>((IList<int>)path.CellIds).Clear());
            Assert.Throws<NotSupportedException>(()=>((IList<CellSegment>)path.CellSegments).Clear());
            var gates=new List<CellGate> {new CellGate(129,SurfaceDirection.Left)}; var map=new ExitMap(puzzle.Surface,gates); gates.Clear();
            Assert.That(map.CellGates.Count, Is.EqualTo(1)); Assert.Throws<NotSupportedException>(()=>((IList<CellGate>)map.CellGates).Clear());
            Assert.Throws<ArgumentException>(()=>new ArrowPath(0,puzzle.Surface,new[] {101,101},SurfaceDirection.Right));
            Assert.Throws<ArgumentException>(()=>new ArrowPath(0,puzzle.Surface,new[] {101,331},SurfaceDirection.Right));
            Assert.Throws<ArgumentException>(()=>new ArrowPath(0,puzzle.Surface,new[] {101,143},SurfaceDirection.Up));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new ArrowPath(0,puzzle.Surface,new[] {999},SurfaceDirection.Right));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new ExitMap(puzzle.Surface,new[] {new CellGate(999,SurfaceDirection.Up)}));
            Assert.Throws<ArgumentException>(()=>new ExitMap(puzzle.Surface,new[] {new CellGate(101,SurfaceDirection.Up),new CellGate(101,SurfaceDirection.Up)}));
            Assert.Throws<NotSupportedException>(()=>{ var unused=path.Cells; });
        }

        [Test]
        public void TrajectoryCacheIncludesSurfaceIdentityAndHonorsCancellation()
        {
            var first=ManualSurfacePuzzle.Create(); var second=ManualSurfacePuzzle.Create(); var cache=new RemovalTrajectoryCache();
            var a=cache.Get(first.Surface,first.Paths[0],first.Exits,first.Rules);
            Assert.That(cache.Get(first.Surface,first.Paths[0],first.Exits,first.Rules), Is.SameAs(a));
            Assert.That(cache.Get(second.Surface,second.Paths[0],second.Exits,second.Rules), Is.Not.SameAs(a));
            Assert.That(cache.Hits, Is.EqualTo(1)); Assert.That(cache.Misses, Is.EqualTo(2));
            Assert.Throws<ArgumentException>(()=>cache.Get(second.Surface,first.Paths[0],first.Exits,first.Rules));
            using(var cancellation=new CancellationTokenSource())
            { cancellation.Cancel(); Assert.Throws<OperationCanceledException>(()=>cache.Get(first.Surface,first.Paths[0],first.Exits,first.Rules,cancellation.Token)); }
        }
    }
}
