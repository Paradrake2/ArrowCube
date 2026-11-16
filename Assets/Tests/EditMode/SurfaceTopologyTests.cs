using System;
using System.Collections.Generic;
using System.Linq;
using ArrowCube.Movement;
using ArrowCube.Topology;
using NUnit.Framework;

namespace ArrowCube.Tests
{
    public class SurfaceTopologyTests
    {
        // A hand-wired route with successive local heading changes:
        // 10/Right --concave--> 20/Up --convex--> 30/Left --concave--> 40/Down --> 10/Right.
        // Unused direction pairs are closed self-connections; this is logical test data, not a shape generator.
        private static SurfaceTopologyBuilder Fixture(params int[] order)
        {
            var builder=new SurfaceTopologyBuilder();
            foreach(int id in order.Length==0 ? new[] { 10,20,30,40 } : order) builder.AddCell(id);
            builder.Connect(10,SurfaceDirection.Right,20,SurfaceDirection.Up,true,false);
            builder.Connect(20,SurfaceDirection.Up,30,SurfaceDirection.Left,true,true);
            builder.Connect(30,SurfaceDirection.Left,40,SurfaceDirection.Down,true,false);
            builder.Connect(40,SurfaceDirection.Down,10,SurfaceDirection.Right);
            builder.Connect(10,SurfaceDirection.Up,10,SurfaceDirection.Up);
            builder.Connect(20,SurfaceDirection.Right,20,SurfaceDirection.Right);
            builder.Connect(30,SurfaceDirection.Up,30,SurfaceDirection.Up);
            builder.Connect(40,SurfaceDirection.Right,40,SurfaceDirection.Right);
            return builder;
        }

        [Test]
        public void SuccessiveFoldsTransportHeadingAndEveryConnectionIsReversible()
        {
            var topology=Fixture().Build(); int cell=10; var direction=SurfaceDirection.Right;
            var expectedCells=new[] { 20,30,40,10 };
            var expectedDirections=new[] { SurfaceDirection.Up,SurfaceDirection.Left,SurfaceDirection.Down,SurfaceDirection.Right };
            for(int i=0;i<4;i++)
            {
                var step=topology.Step(cell,direction);
                Assert.That(step.CellId, Is.EqualTo(expectedCells[i])); Assert.That(step.Direction, Is.EqualTo(expectedDirections[i]));
                cell=step.CellId; direction=step.Direction;
            }
            foreach(int id in topology.EnumerateCellIds())
            for(int d=0;d<4;d++)
            {
                var outgoing=(SurfaceDirection)d; var step=topology.Step(id,outgoing);
                var reverse=topology.Step(step.CellId,step.Direction.Opposite());
                Assert.That(reverse.CellId, Is.EqualTo(id)); Assert.That(reverse.Direction, Is.EqualTo(outgoing.Opposite()));
            }
        }

        [Test]
        public void ConcaveFoldContinuesEvenWithEdgeExitsEnabledAndConvexFoldExitsOnlyWhenEnabled()
        {
            var topology=Fixture().Build(); var inside=topology.Step(10,SurfaceDirection.Right);
            Assert.That(inside.IsGeometricEdge, Is.True); Assert.That(inside.AllowsEdgeExit, Is.False);
            Assert.That(ExitRules.EdgeOnly.SelectExit(inside.AllowsEdgeExit,false), Is.Null);
            Assert.That(inside.CellId, Is.EqualTo(20)); Assert.That(inside.Direction, Is.EqualTo(SurfaceDirection.Up));
            var outside=topology.Step(inside.CellId,inside.Direction);
            Assert.That(outside.IsGeometricEdge && outside.AllowsEdgeExit, Is.True);
            Assert.That(ExitRules.EdgeOnly.SelectExit(outside.AllowsEdgeExit,false), Is.EqualTo(ExitType.CubeEdge));
            Assert.That(ExitRules.GateOnly.SelectExit(outside.AllowsEdgeExit,false), Is.Null);
            Assert.That(ExitRules.None.SelectExit(outside.AllowsEdgeExit,false), Is.Null);
            Assert.That(outside.CellId, Is.EqualTo(30)); Assert.That(outside.Direction, Is.EqualTo(SurfaceDirection.Left));
        }

        [Test]
        public void PuzzleOwnedGatesUseCellAndHeadingIndependentlyOfBoundaryGeometry()
        {
            var topology=Fixture().Build();
            var gates=new HashSet<(int,SurfaceDirection)> { (10,SurfaceDirection.Right),(10,SurfaceDirection.Up),(20,SurfaceDirection.Up) };
            var concave=topology.Step(10,SurfaceDirection.Right);
            Assert.That(ExitRules.Hybrid.SelectExit(concave.AllowsEdgeExit,gates.Contains((10,SurfaceDirection.Right))), Is.EqualTo(ExitType.DirectionalGate));
            Assert.That(ExitRules.EdgeOnly.SelectExit(concave.AllowsEdgeExit,true), Is.Null,"A disabled gate must not turn an inside fold into an ordinary exit.");
            var flat=topology.Step(10,SurfaceDirection.Up);
            Assert.That(flat.IsGeometricEdge || flat.AllowsEdgeExit, Is.False);
            Assert.That(ExitRules.GateOnly.SelectExit(flat.AllowsEdgeExit,gates.Contains((10,SurfaceDirection.Up))), Is.EqualTo(ExitType.DirectionalGate));
            Assert.That(ExitRules.GateOnly.SelectExit(flat.AllowsEdgeExit,gates.Contains((10,SurfaceDirection.Down))), Is.Null,"Gate heading matters.");
            var convex=topology.Step(20,SurfaceDirection.Up);
            Assert.That(ExitRules.Hybrid.SelectExit(convex.AllowsEdgeExit,true), Is.EqualTo(ExitType.CubeEdge),"Preserve edge priority when both exits match.");
            Assert.That(ExitRules.GateOnly.SelectExit(convex.AllowsEdgeExit,true), Is.EqualTo(ExitType.DirectionalGate));
        }

        [Test]
        public void PublishedConnectionsAndPermissionsCannotBeMutatedThroughBuilderOrCollections()
        {
            var builder=Fixture(); var original=builder.Build();
            builder.Connect(10,SurfaceDirection.Right,20,SurfaceDirection.Up,true,true);
            var changed=builder.Build();
            Assert.That(original.GetBoundary(10,SurfaceDirection.Right).AllowsEdgeExit, Is.False);
            Assert.That(changed.GetBoundary(10,SurfaceDirection.Right).AllowsEdgeExit, Is.True);
            var boundaries=(IList<CellBoundary>)original.GetCell(10).Boundaries;
            Assert.Throws<NotSupportedException>(()=>boundaries[0]=new CellBoundary(10,SurfaceDirection.Right));
            Assert.Throws<NotSupportedException>(()=>boundaries.Clear());
            Assert.Throws<NotSupportedException>(()=>((IList<int>)original.EnumerateCellIds()).Add(99));
            Assert.That(original.Step(10,SurfaceDirection.Right).CellId, Is.EqualTo(20));
        }

        [Test]
        public void InsertionOrderDoesNotChangeEnumerationConnectionsOrDirectionIndexing()
        {
            var a=Fixture().Build(); var b=Fixture(40,10,30,20).Build();
            Assert.That(b.EnumerateCellIds(), Is.EqualTo(new[] { 10,20,30,40 }));
            Assert.That((int)SurfaceDirection.Right, Is.Zero); Assert.That((int)SurfaceDirection.Up, Is.EqualTo(1));
            Assert.That((int)SurfaceDirection.Left, Is.EqualTo(2)); Assert.That((int)SurfaceDirection.Down, Is.EqualTo(3));
            foreach(int id in a.EnumerateCellIds())
            for(int d=0;d<4;d++)
            {
                var direction=(SurfaceDirection)d;
                Assert.That(a.Step(id,direction), Is.EqualTo(b.Step(id,direction)));
                Assert.That(a.GetCell(id).GetBoundary(direction), Is.SameAs(a.GetBoundary(id,direction)));
            }
        }

        [Test]
        public void EmptyIncompleteDanglingAndNonReversibleTopologiesAreRejected()
        {
            Assert.Throws<InvalidOperationException>(()=>new SurfaceTopologyBuilder().Build());
            Assert.Throws<InvalidOperationException>(()=>new SurfaceTopologyBuilder().AddCell(10).Build());
            var dangling=Fixture().SetBoundary(10,SurfaceDirection.Right,new CellBoundary(99,SurfaceDirection.Up));
            Assert.That(Assert.Throws<InvalidOperationException>(()=>dangling.Build()).Message, Does.Contain("missing destination"));
            var wrongCell=Fixture().SetBoundary(10,SurfaceDirection.Right,new CellBoundary(30,SurfaceDirection.Up));
            Assert.That(Assert.Throws<InvalidOperationException>(()=>wrongCell.Build()).Message, Does.Contain("reversible"));
            var wrongHeading=Fixture().SetBoundary(20,SurfaceDirection.Down,new CellBoundary(10,SurfaceDirection.Up));
            Assert.That(Assert.Throws<InvalidOperationException>(()=>wrongHeading.Build()).Message, Does.Contain("correct heading"));
        }

        [Test]
        public void InvalidIdsDirectionsAndHeadingsFailExplicitly()
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>new SurfaceTopologyBuilder().AddCell(-1));
            Assert.Throws<ArgumentException>(()=>new SurfaceTopologyBuilder().AddCell(1).AddCell(1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new CellBoundary(-1,SurfaceDirection.Up));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new CellBoundary(1,(SurfaceDirection)4));
            var builder=Fixture(); var topology=builder.Build();
            Assert.Throws<ArgumentOutOfRangeException>(()=>builder.Connect(10,(SurfaceDirection)4,20,SurfaceDirection.Up));
            Assert.Throws<ArgumentOutOfRangeException>(()=>builder.Connect(10,SurfaceDirection.Right,20,(SurfaceDirection)4));
            Assert.Throws<ArgumentOutOfRangeException>(()=>builder.Connect(10,SurfaceDirection.Right,99,SurfaceDirection.Up));
            Assert.Throws<ArgumentNullException>(()=>builder.SetBoundary(10,SurfaceDirection.Right,null));
            Assert.Throws<ArgumentOutOfRangeException>(()=>topology.GetBoundary(10,(SurfaceDirection)4));
            Assert.Throws<ArgumentOutOfRangeException>(()=>topology.Step(-1,SurfaceDirection.Right));
            Assert.Throws<ArgumentNullException>(()=>SurfaceTraversal.Step(null,10,SurfaceDirection.Right));
            Assert.That(topology.ContainsCell(10), Is.True); Assert.That(topology.ContainsCell(-1), Is.False);
        }

        [Test]
        public void OpenBoundariesAreExplicitAndDifferentFromMissingConfiguration()
        {
            var builder=new SurfaceTopologyBuilder().AddCell(7);
            Assert.That(builder.GetBoundary(7,SurfaceDirection.Right), Is.Null);
            var open=CellBoundary.Open(true,true);
            Assert.That(open.Kind, Is.EqualTo(BoundaryConnectionKind.Open)); Assert.That(open.NeighborCellId, Is.Null);
            Assert.That(new CellBoundary(7,SurfaceDirection.Right).Kind, Is.EqualTo(BoundaryConnectionKind.Connected));
            builder.SetBoundary(7,SurfaceDirection.Right,open);
            Assert.That(Assert.Throws<NotSupportedException>(()=>builder.Build()).Message, Does.Contain("intentionally open"));
        }

        [Test]
        public void GeometricFoldAndEdgeExitEligibilityAreIndependentProperties()
        {
            var boundary=new CellBoundary(0,SurfaceDirection.Right,false,true);
            Assert.That(boundary.IsGeometricEdge, Is.False); Assert.That(boundary.AllowsEdgeExit, Is.True);
            Assert.That(ExitRules.EdgeOnly.SelectExit(boundary.AllowsEdgeExit,false), Is.EqualTo(ExitType.CubeEdge));
        }
    }
}
