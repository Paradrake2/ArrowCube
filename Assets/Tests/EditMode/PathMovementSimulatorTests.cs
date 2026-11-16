using System;
using System.Collections.Generic;
using System.Linq;
using ArrowCube.Movement;
using ArrowCube.Topology;
using NUnit.Framework;

namespace ArrowCube.Tests
{
    public class PathMovementSimulatorTests
    {
        private readonly PathMovementSimulator simulator = new PathMovementSimulator();

        [Test]
        public void StraightSnakeExitsOnlyAfterItsEntireBodyHasDeparted()
        {
            var puzzle = ManualPuzzles.Create(0);
            var grid = puzzle.CreateGrid();
            var path = puzzle.Paths[0];
            var plan = simulator.Simulate(grid, path.Id, puzzle.Exits,puzzle.Rules);
            Assert.That(plan.Succeeded, Is.True);
            Assert.That(plan.StepCount, Is.EqualTo(6)); // Three surface moves, then three to drain the body.
            Assert.That(plan.GetPosition(1, 0).Cell, Is.EqualTo(C(CubeFace.Front,3,2)));
            Assert.That(plan.GetPosition(3, 0).IsOnSurface, Is.True);
            Assert.That(plan.GetPosition(4, 0).ExitDistance, Is.EqualTo(1));
            Assert.That(plan.GetPosition(5, 2).IsOnSurface, Is.True);
            for (int i = 0; i < path.Cells.Count; i++)
            {
                Assert.That(plan.GetPosition(plan.StepCount, i).IsOnSurface, Is.False);
                Assert.That(plan.GetPosition(plan.StepCount, i).ExitDistance, Is.EqualTo(path.Cells.Count - i));
            }
            Assert.That(grid.Paths.Count, Is.EqualTo(1), "Simulation must not mutate occupancy.");
            Assert.That(grid.TryCommit(plan), Is.True);
            Assert.That(grid.Paths, Is.Empty);
            Assert.That(grid.OccupiedCellCount, Is.Zero);
            Assert.That(grid.OccupiedSegmentCount, Is.Zero);
        }

        [Test]
        public void BentCrossFaceBodyFollowsEveryPreviousSegmentRatherThanTranslatingRigidly()
        {
            var puzzle = ManualPuzzles.Create(1);
            var grid = puzzle.CreateGrid();
            var plan = simulator.Simulate(grid, 10, puzzle.Exits,puzzle.Rules);
            Assert.That(plan.Succeeded, Is.True);
            for (int step = 1; step <= plan.StepCount; step++)
            for (int i = 1; i < plan.Path.Cells.Count; i++)
                Assert.That(plan.GetPosition(step, i), Is.EqualTo(plan.GetPosition(step - 1, i - 1)));
            Assert.That(plan.Rail.Where(p => p.IsOnSurface).Select(p => p.Cell.Face).Distinct().Count(), Is.EqualTo(3));
            Assert.That(plan.GetPosition(5, 0).Cell, Is.EqualTo(C(CubeFace.Back,0,2)));
            Assert.That(plan.Path.FaceTransitionCount, Is.EqualTo(1));
        }

        [TestCase(CubeFace.Front)]
        [TestCase(CubeFace.Right)]
        [TestCase(CubeFace.Back)]
        [TestCase(CubeFace.Left)]
        [TestCase(CubeFace.Top)]
        [TestCase(CubeFace.Bottom)]
        public void EveryFaceAndHeadingCanTraverseMultipleEdgesBeforeAnExplicitExit(CubeFace face)
        {
            var t = new CubeTopology(4);
            for (int d = 0; d < 4; d++)
            {
                var heading = (SurfaceDirection)d;
                var head = C(face,1,1);
                var neck = t.Step(head, heading.Opposite()).Coordinate;
                var gateCell = head;
                var gateHeading = heading;
                for (int i = 0; i < 8; i++)
                {
                    var step = t.Step(gateCell, gateHeading);
                    gateCell = step.Coordinate; gateHeading = step.Direction;
                }
                var path = new ArrowPath(1,t,new[] { head,neck },heading);
                var grid = new SurfaceGrid(t,new[] { path });
                var exits = new ExitMap(t,new[] { new ExitGate(gateCell,gateHeading) });
                var plan = simulator.Simulate(grid,1,exits,ExitRules.GateOnly);
                Assert.That(plan.Succeeded, Is.True, $"{face} {heading}");
                Assert.That(plan.StepCount, Is.EqualTo(10));
                Assert.That(plan.GetPosition(8,0), Is.EqualTo(new PathPosition(gateCell)));
                Assert.That(grid.TryCommit(plan), Is.True);
            }
        }

        [TestCase(2, 4, CubeFace.Back, 0, 2)]
        [TestCase(3, 2, CubeFace.Top, 4, 2)]
        public void FullTrajectoryFindsDistantBlockerAcrossASeamWithoutChangingState(int fixture, int safeSteps, CubeFace face, int x, int y)
        {
            var puzzle = ManualPuzzles.Create(fixture);
            var grid = puzzle.CreateGrid();
            long revision = grid.Revision;
            var cellsBefore = Snapshot(grid);
            var plan = simulator.Simulate(grid,10,puzzle.Exits,puzzle.Rules);
            Assert.That(plan.Outcome, Is.EqualTo(MovementOutcome.BlockedByPath));
            Assert.That(plan.StepCount, Is.EqualTo(safeSteps));
            Assert.That(plan.BlockingPathId, Is.EqualTo(20));
            Assert.That(plan.BlockedCell, Is.EqualTo(C(face,x,y)));
            Assert.That(grid.TryCommit(plan), Is.False);
            Assert.That(Snapshot(grid), Is.EqualTo(cellsBefore));
            Assert.That(grid.Revision, Is.EqualTo(revision));
            Assert.That(grid.Paths[10], Is.SameAs(puzzle.Paths[0]));
            var repeat = simulator.Simulate(grid,10,puzzle.Exits,puzzle.Rules);
            Assert.That(repeat.Rail, Is.EqualTo(plan.Rail));
            Assert.That(repeat.BlockedCell, Is.EqualTo(plan.BlockedCell));
        }

        [Test]
        public void ImmediateCollisionDoesNotAllowAnyDiscreteStep()
        {
            var t = new CubeTopology(6);
            var path = Path(t,1,SurfaceDirection.Right,C(CubeFace.Front,2,2),C(CubeFace.Front,1,2));
            var blocker = Path(t,2,SurfaceDirection.Up,C(CubeFace.Front,3,2));
            var grid = new SurfaceGrid(t,new[] { path, blocker });
            var plan = simulator.Simulate(grid,1,new ExitMap(t,new[] { G(CubeFace.Front,5,2,SurfaceDirection.Right) }));
            Assert.That(plan.Outcome, Is.EqualTo(MovementOutcome.BlockedByPath));
            Assert.That(plan.StepCount, Is.Zero);
            Assert.That(plan.AttemptedHead.Cell, Is.EqualTo(blocker.Head));
        }

        [TestCase(2)]
        [TestCase(3)]
        public void RemovingTheBlockerMakesTheOriginalSnakeRemovable(int fixture)
        {
            var puzzle = ManualPuzzles.Create(fixture);
            var grid = puzzle.CreateGrid();
            var blocked = simulator.Simulate(grid,10,puzzle.Exits,puzzle.Rules);
            Assert.That(blocked.Succeeded, Is.False);
            var blockerPlan = simulator.Simulate(grid,20,puzzle.Exits,puzzle.Rules);
            Assert.That(blockerPlan.Succeeded, Is.True);
            Assert.That(grid.TryCommit(blockerPlan), Is.True);
            var clear = simulator.Simulate(grid,10,puzzle.Exits,puzzle.Rules);
            Assert.That(clear.Succeeded, Is.True);
            Assert.That(grid.TryCommit(clear), Is.True);
            Assert.That(grid.OccupiedCellCount, Is.Zero);
        }

        [Test]
        public void EnteringTheSimultaneouslyVacatedTailIsNotASelfCollision()
        {
            var t = new CubeTopology(6);
            var path = Path(t,1,SurfaceDirection.Right,C(CubeFace.Front,2,2),C(CubeFace.Front,1,2),
                C(CubeFace.Front,1,1),C(CubeFace.Front,2,1),C(CubeFace.Front,3,1),C(CubeFace.Front,3,2));
            var grid = new SurfaceGrid(t,new[] { path });
            var plan = simulator.Simulate(grid,1,new ExitMap(t,new[] { G(CubeFace.Front,4,2,SurfaceDirection.Right) }));
            Assert.That(plan.Succeeded, Is.True);
            Assert.That(plan.GetPosition(1,0).Cell, Is.EqualTo(path.Tail));
        }

        [Test]
        public void EnteringANonVacatedBodyCellIsAnActualSelfCollision()
        {
            var t = new CubeTopology(6);
            var path = Path(t,1,SurfaceDirection.Right,C(CubeFace.Front,2,2),C(CubeFace.Front,1,2),
                C(CubeFace.Front,1,1),C(CubeFace.Front,2,1),C(CubeFace.Front,3,1),C(CubeFace.Front,3,2),C(CubeFace.Front,4,2));
            var grid = new SurfaceGrid(t,new[] { path });
            var plan = simulator.Simulate(grid,1,new ExitMap(t,new[] { G(CubeFace.Front,5,2,SurfaceDirection.Right) }));
            Assert.That(plan.Outcome, Is.EqualTo(MovementOutcome.SelfCollision));
            Assert.That(plan.StepCount, Is.Zero);
            Assert.That(grid.TryCommit(plan), Is.False);
        }

        [Test]
        public void ClosedCircuitTerminatesAndDoesNotMistakeAnyCubeEdgeForAnExit()
        {
            var puzzle = ManualPuzzles.Create(4);
            var grid = puzzle.CreateGrid();
            var plan = simulator.Simulate(grid,10,puzzle.Exits,puzzle.Rules);
            Assert.That(plan.Outcome, Is.EqualTo(MovementOutcome.NoExit));
            Assert.That(plan.StepCount, Is.EqualTo(4 * puzzle.Topology.GridSize));
            Assert.That(plan.Rail.All(p => p.IsOnSurface), Is.True);
            Assert.That(grid.TryCommit(plan), Is.False);
            Assert.That(grid.OccupiedCellCount, Is.EqualTo(puzzle.Paths[0].Cells.Count));
        }

        [Test]
        public void AWrongHeadingDoesNotOpenAGate()
        {
            var puzzle = ManualPuzzles.Create(0);
            var plan = simulator.Simulate(puzzle.CreateGrid(),10,
                new ExitMap(puzzle.Topology,new[] { G(CubeFace.Front,5,2,SurfaceDirection.Up) }),ExitRules.GateOnly);
            Assert.That(plan.Outcome, Is.EqualTo(MovementOutcome.NoExit));
        }

        [Test]
        public void GateAtInitialHeadStillRequiresTheWholeTailToDrain()
        {
            var puzzle = ManualPuzzles.Create(0);
            var path = puzzle.Paths[0];
            var plan = simulator.Simulate(puzzle.CreateGrid(),10,
                new ExitMap(puzzle.Topology,new[] { new ExitGate(path.Head,path.HeadDirection) }));
            Assert.That(plan.StepCount, Is.EqualTo(path.Cells.Count));
            Assert.That(plan.GetPosition(1,0).IsOnSurface, Is.False);
            Assert.That(plan.GetPosition(1,1).Cell, Is.EqualTo(path.Head));
        }

        [Test]
        public void AdjacentPathsAndPathsBeyondTheExitDoNotCauseFalsePositives()
        {
            var p = ManualPuzzles.Create(0);
            var grid = p.CreateGrid();
            grid.AddPath(Path(p.Topology,2,SurfaceDirection.Right,C(CubeFace.Front,3,3),C(CubeFace.Front,2,3)));
            grid.AddPath(Path(p.Topology,3,SurfaceDirection.Right,C(CubeFace.Right,0,2)));
            var plan = simulator.Simulate(grid,10,p.Exits,p.Rules);
            Assert.That(plan.Succeeded, Is.True);
            Assert.That(grid.TryCommit(plan), Is.True);
            Assert.That(grid.Paths.Count, Is.EqualTo(2));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ManualPuzzleSolutionsClearAllCellsAndSegments(int fixture)
        {
            var puzzle = ManualPuzzles.Create(fixture);
            var grid = puzzle.CreateGrid();
            var solution = fixture < 2 ? new[] { 10 } : new[] { 20,10 };
            foreach (int id in solution)
            {
                var plan = simulator.Simulate(grid,id,puzzle.Exits,puzzle.Rules);
                Assert.That(plan.Succeeded, Is.True);
                // Every body cell is either its own original occupancy or a validated empty cell.
                for (int s = 0; s <= plan.StepCount; s++)
                for (int i = 0; i < plan.Path.Cells.Count; i++)
                {
                    var position = plan.GetPosition(s,i);
                    if (position.IsOnSurface && grid.TryGetCellOwner(position.Cell,out int owner))
                        Assert.That(owner, Is.EqualTo(id));
                }
                Assert.That(grid.TryCommit(plan), Is.True);
            }
            Assert.That(grid.Paths, Is.Empty);
            Assert.That(grid.OccupiedCellCount, Is.Zero);
            Assert.That(grid.OccupiedSegmentCount, Is.Zero);
            var restart = puzzle.CreateGrid();
            Assert.That(restart.Paths.Keys, Is.EquivalentTo(puzzle.Paths.Select(p => p.Id)));
            foreach (var path in puzzle.Paths) Assert.That(restart.Paths[path.Id].Cells, Is.EqualTo(path.Cells));
        }

        [Test]
        public void InvalidInputsAndFrameIndicesFailExplicitly()
        {
            var p = ManualPuzzles.Create(0);
            var grid = p.CreateGrid();
            Assert.Throws<ArgumentNullException>(() => simulator.Simulate(null,10,p.Exits));
            Assert.Throws<ArgumentNullException>(() => simulator.Simulate(grid,10,null));
            Assert.Throws<ArgumentException>(() => simulator.Simulate(grid,999,p.Exits));
            Assert.Throws<ArgumentException>(() => simulator.Simulate(grid,10,new ExitMap(new CubeTopology(3),Array.Empty<ExitGate>())));
            Assert.Throws<ArgumentException>(() => new ExitMap(p.Topology,new[] { p.Exits.Gates[0],p.Exits.Gates[0] }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ExitMap(p.Topology,new[] { G(CubeFace.Top,6,0,SurfaceDirection.Right) }));
            var plan = simulator.Simulate(grid,10,p.Exits,p.Rules);
            Assert.Throws<ArgumentOutOfRangeException>(() => plan.GetPosition(-1,0));
            Assert.Throws<ArgumentOutOfRangeException>(() => plan.GetPosition(plan.StepCount+1,0));
            Assert.Throws<ArgumentOutOfRangeException>(() => plan.GetPosition(0,plan.Path.Cells.Count));
        }

        private static SurfaceCoordinate C(CubeFace f,int x,int y) => new SurfaceCoordinate(f,x,y);
        private static ExitGate G(CubeFace f,int x,int y,SurfaceDirection d) => new ExitGate(C(f,x,y),d);
        private static ArrowPath Path(CubeTopology t,int id,SurfaceDirection d,params SurfaceCoordinate[] c) => new ArrowPath(id,t,c,d);
        private static int[] Snapshot(SurfaceGrid grid) => grid.Topology.EnumerateCells()
            .Select(c => grid.TryGetCellOwner(c,out int owner) ? owner : -1).ToArray();
    }
}
