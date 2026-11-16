using System;
using System.Collections.Generic;
using ArrowCube.Movement;
using ArrowCube.Topology;
using NUnit.Framework;

namespace ArrowCube.Tests
{
    public class SurfaceGridTests
    {
        [Test]
        public void CellAndSegmentQueriesAgreeAcrossSeamsAndRemovalClearsBoth()
        {
            var puzzle = ManualPuzzles.Create(1);
            var grid = puzzle.CreateGrid();
            var path = puzzle.Paths[0];
            Assert.That(grid.OccupiedCellCount, Is.EqualTo(path.Cells.Count));
            Assert.That(grid.OccupiedSegmentCount, Is.EqualTo(path.Segments.Count));
            foreach (var cell in path.Cells)
            { Assert.That(grid.TryGetCellOwner(cell,out int id), Is.True); Assert.That(id, Is.EqualTo(path.Id)); }
            foreach (var edge in path.Segments)
            {
                Assert.That(grid.TryGetSegmentOwner(new SurfaceSegment(puzzle.Topology,edge.B,edge.A),out int id), Is.True);
                Assert.That(id, Is.EqualTo(path.Id));
            }
            Assert.That(grid.RemovePath(10), Is.True);
            Assert.That(grid.OccupiedCellCount, Is.Zero);
            Assert.That(grid.OccupiedSegmentCount, Is.Zero);
            long revision = grid.Revision;
            Assert.That(grid.RemovePath(10), Is.False);
            Assert.That(grid.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void InvalidAddIsAtomicAndDuplicateIdsAreRejected()
        {
            var p = ManualPuzzles.Create(0);
            var grid = p.CreateGrid();
            long revision = grid.Revision;
            var overlapping = new ArrowPath(20,p.Topology,new[] { new SurfaceCoordinate(CubeFace.Front,2,2) },SurfaceDirection.Up);
            Assert.Throws<ArgumentException>(() => grid.AddPath(overlapping));
            Assert.Throws<ArgumentException>(() => grid.AddPath(p.Paths[0]));
            Assert.That(grid.Paths.Count, Is.EqualTo(1));
            Assert.That(grid.OccupiedCellCount, Is.EqualTo(3));
            Assert.That(grid.OccupiedSegmentCount, Is.EqualTo(2));
            Assert.That(grid.Revision, Is.EqualTo(revision));
            Assert.Throws<NotSupportedException>(() => ((IDictionary<int,ArrowPath>)grid.Paths).Clear());
        }

        [Test]
        public void StaleForeignAndAlreadyCommittedPlansCannotRemoveAPath()
        {
            var p = ManualPuzzles.Create(0);
            var a = p.CreateGrid(); var b = p.CreateGrid();
            var simulator = new PathMovementSimulator();
            var plan = simulator.Simulate(a,10,p.Exits,p.Rules);
            Assert.That(b.TryCommit(plan), Is.False);
            Assert.That(a.TryCommit(null), Is.False);
            a.AddPath(new ArrowPath(2,p.Topology,new[] { new SurfaceCoordinate(CubeFace.Top,0,0) },SurfaceDirection.Up));
            Assert.That(a.TryCommit(plan), Is.False);
            Assert.That(a.Paths.ContainsKey(10), Is.True);
            plan = simulator.Simulate(a,10,p.Exits,p.Rules);
            Assert.That(a.TryCommit(plan), Is.True);
            Assert.That(a.TryCommit(plan), Is.False);
            Assert.That(a.Paths.ContainsKey(2), Is.True);
        }

        [Test]
        public void PathsMustBeConnectedOnTheOccupancyGridsOwnTopology()
        {
            var t = new CubeTopology(3);
            var path = new ArrowPath(1,t,new[] { new SurfaceCoordinate(CubeFace.Right,0,1),new SurfaceCoordinate(CubeFace.Front,2,1) },SurfaceDirection.Right);
            var other = new SurfaceGrid(new CubeTopology(6),Array.Empty<ArrowPath>());
            Assert.Throws<ArgumentException>(() => other.AddPath(path));
            Assert.That(other.Paths, Is.Empty);
            Assert.That(other.Revision, Is.Zero);
        }
    }
}
