using System.Collections;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using ArrowCube.Topology;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArrowCube.Tests
{
    public class MovementDemoTests
    {
        private GameObject root;
        private Stage2MovementDemo demo;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root = new GameObject("Movement demo test");
            demo = root.AddComponent<Stage2MovementDemo>();
            yield return null; // Runs Start and constructs the manual puzzle/visuals.
            demo.LoadPuzzle(2); // Existing gate-only regression fixture.
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FailedAttemptRestoresVisualsAndBlockerRemovalThenClearsThePuzzle()
        {
            var grid = demo.Grid;
            long originalRevision = grid.Revision;
            var originalPath = grid.Paths[10];
            Assert.That(demo.TryActivate(10), Is.True);
            Assert.That(demo.LastPlan.Outcome, Is.EqualTo(MovementOutcome.BlockedByPath));
            Assert.That(demo.TryActivate(20), Is.False, "Only one animation transaction can run.");
            yield return WaitForIdle();
            Assert.That(grid.Revision, Is.EqualTo(originalRevision));
            Assert.That(grid.Paths[10], Is.SameAs(originalPath));
            var body = root.transform.Find("Stage 2 runtime objects/Arrow 10/Body").GetComponent<SurfacePathRenderer>();
            Assert.That(body.SharedMaterial.color, Is.EqualTo(Color.black));
            var originalPoints = new System.Collections.Generic.List<Vector3>();
            new CubeSurfaceGeometry(grid.Topology,4).WritePolyline(originalPath,originalPoints,(4f/6)*0.04f);
            var forward=new CubeSurfaceGeometry(grid.Topology,4).GetLocalDirection(originalPath.Head.Face,originalPath.HeadDirection);
            originalPoints[0]-=forward*TriangleArrowHead.Length(4f/6,demo.ArrowBodyWidth);
            Assert.That(body.PositionCount, Is.EqualTo(originalPoints.Count));
            for (int i = 0; i < body.PositionCount; i++)
                Assert.That(Vector3.Distance(body.GetPosition(i),originalPoints[i]), Is.LessThan(0.00001f));
            Assert.That(demo.TryActivate(20), Is.True);
            Assert.That(grid.Paths.ContainsKey(20), Is.True, "Occupancy stays reserved until the animation finishes.");
            yield return WaitForIdle();
            Assert.That(grid.Paths.Count, Is.EqualTo(1));
            Assert.That(demo.TryActivate(10), Is.True);
            yield return WaitForIdle();
            Assert.That(grid.Paths, Is.Empty);
            Assert.That(grid.OccupiedCellCount, Is.Zero);
            Assert.That(grid.OccupiedSegmentCount, Is.Zero);
            yield return null;
            Assert.That(root.transform.Find("Stage 2 runtime objects/Arrow 10"), Is.Null);
        }

        [UnityTest]
        public IEnumerator BlockedArrowsTravelToTheVisibleObstructionPauseAndRestoreAcrossFaces()
        {
            foreach(int fixture in new[] { -1,2,3,6 })
            {
                if(fixture<0)
                {
                    var t=new CubeTopology(8);
                    var moving=new ArrowPath(10,t,new[] { new SurfaceCoordinate(CubeFace.Front,1,3),new SurfaceCoordinate(CubeFace.Front,0,3) },SurfaceDirection.Right);
                    var blocker=new ArrowPath(20,t,new[] { new SurfaceCoordinate(CubeFace.Front,6,4),new SurfaceCoordinate(CubeFace.Front,6,3),new SurfaceCoordinate(CubeFace.Front,6,2) },SurfaceDirection.Up);
                    demo.LoadDefinition(new PuzzleDefinition("Distant visible blocker",t,new[] { moving,blocker },new ExitMap(t,System.Array.Empty<ExitGate>()),ExitRules.EdgeOnly));
                }
                else demo.LoadPuzzle(fixture);
                yield return null;
                demo.AnimationCellsPerSecond=30;
                int id=fixture==6 ? 20 : 10;
                var body=root.transform.Find($"Stage 2 runtime objects/Arrow {id}/Body").GetComponent<SurfacePathRenderer>();
                var head=root.transform.Find($"Stage 2 runtime objects/Arrow {id}/Arrowhead").GetComponent<MeshFilter>().sharedMesh;
                var originalHead=head.vertices; var originalBody=body.GetComponent<MeshFilter>().sharedMesh.vertices;
                long revision=demo.Grid.Revision; int paths=demo.Grid.Paths.Count,callbacks=0;
                System.Action<MovementPlan,bool> completed=(plan,committed)=> { callbacks++; Assert.That(committed, Is.False); };
                demo.AttemptCompleted+=completed;
                Assert.That(demo.TryActivate(id), Is.True);
                Assert.That(demo.LastPlan.Outcome, Is.EqualTo(MovementOutcome.BlockedByPath));
                Assert.That(demo.TryActivate(id), Is.False);
                bool reached=false; float deadline=Time.realtimeSinceStartup+6;
                while(demo.IsAnimating && Time.realtimeSinceStartup<deadline)
                {
                    Assert.That(demo.Grid.Revision, Is.EqualTo(revision)); Assert.That(demo.Grid.Paths.Count, Is.EqualTo(paths));
                    foreach(var vertex in head.vertices)
                        Assert.That(Mathf.Max(Mathf.Abs(vertex.x),Mathf.Abs(vertex.y),Mathf.Abs(vertex.z)), Is.GreaterThanOrEqualTo(2-.0001f),"Travel must stay outside the cube, including rotated seams.");
                    var marker=root.transform.Find("Stage 2 runtime objects/Block contact");
                    if(!reached && marker!=null && marker.gameObject.activeSelf)
                    {
                        reached=true;
                        var geometry=new CubeSurfaceGeometry(demo.Puzzle.Topology,4);
                        var blocked=geometry.GetLocalPosition(demo.LastPlan.BlockedCell.Value,geometry.CellSize*.04f);
                        Assert.That(Vector3.Distance(head.vertices[0],blocked), Is.LessThan(geometry.CellSize*.5f),"The head must reach the distant blocker, not just nudge at its starting position.");
                        if(fixture<0)
                        {
                            var expected=blocked-Vector3.right*(demo.ArrowBodyWidth*.5f+geometry.CellSize*.02f);
                            Assert.That(Vector3.Distance(head.vertices[0],expected), Is.LessThan(.0001f),"Stop at the visible stroke edge with a small gap.");
                            demo.AnimationPaused=true; var paused=head.vertices;
                            yield return new WaitForSecondsRealtime(.1f);
                            Assert.That(head.vertices, Is.EqualTo(paused)); Assert.That(callbacks, Is.Zero);
                            demo.AnimationPaused=false;
                        }
                    }
                    yield return null;
                }
                demo.AttemptCompleted-=completed;
                Assert.That(demo.IsAnimating, Is.False); Assert.That(reached, Is.True); Assert.That(callbacks, Is.EqualTo(1));
                Assert.That(head.vertices, Is.EqualTo(originalHead));
                Assert.That(body.GetComponent<MeshFilter>().sharedMesh.vertices, Is.EqualTo(originalBody));
                Assert.That(root.transform.Find("Stage 2 runtime objects/Block contact").gameObject.activeSelf, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator RestartDuringBlockedTravelCancelsTheReturnAndCompletionCallback()
        {
            demo.AnimationCellsPerSecond=1; int completed=0;
            demo.AttemptCompleted+=(plan,committed)=>completed++;
            Assert.That(demo.TryActivate(10), Is.True); yield return null;
            demo.RestartPuzzle(); yield return new WaitForSecondsRealtime(.3f);
            Assert.That(demo.IsAnimating, Is.False); Assert.That(completed, Is.Zero);
            Assert.That(demo.Grid.Paths.Count, Is.EqualTo(2)); Assert.That(demo.LastPlan, Is.Null);
            Assert.That(root.transform.Find("Stage 2 runtime objects/Block contact"), Is.Null);
        }

        [UnityTest]
        public IEnumerator RestartCancelsTheAnimationAndRestoresTheExactPuzzle()
        {
            var oldGrid = demo.Grid;
            Assert.That(demo.TryActivate(20), Is.True);
            yield return null;
            demo.LoadPuzzle(2);
            var restarted = demo.Grid;
            Assert.That(restarted, Is.Not.SameAs(oldGrid));
            Assert.That(demo.IsAnimating, Is.False);
            Assert.That(demo.LastPlan, Is.Null);
            foreach (var pair in oldGrid.Paths)
                Assert.That(restarted.Paths[pair.Key].Cells, Is.EqualTo(pair.Value.Cells));
            yield return new WaitForSeconds(0.8f);
            Assert.That(restarted.Paths.Count, Is.EqualTo(2));
            Assert.That(oldGrid.Paths.Count, Is.EqualTo(2), "Cancelled plan must not commit to either grid.");
        }

        [UnityTest]
        public IEnumerator EveryManualPuzzleBuildsItsVisualsAndNoGateAttemptRemainsPlayable()
        {
            for (int i = 0; i < ManualPuzzles.Count; i++)
            {
                demo.LoadPuzzle(i);
                yield return null;
                Assert.That(demo.Grid.Paths.Count, Is.GreaterThan(0));
                Assert.That(root.GetComponentsInChildren<Camera>().Length, Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<SurfacePathRenderer>().Length, Is.GreaterThan(0));
            }
            demo.LoadPuzzle(4);
            Assert.That(demo.TryActivate(10), Is.True);
            yield return WaitForIdle();
            Assert.That(demo.LastPlan.Outcome, Is.EqualTo(MovementOutcome.NoExit));
            Assert.That(demo.Grid.Paths.Count, Is.EqualTo(1));
            Assert.That(demo.TryActivate(10), Is.True, "Failed paths remain available for another attempt.");
            yield return WaitForIdle();
        }

        private IEnumerator WaitForIdle()
        {
            float deadline = Time.realtimeSinceStartup + 8;
            while (demo.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(demo.IsAnimating, Is.False, "Animation timed out.");
        }

        [UnityTest]
        public IEnumerator HybridDemoAnimatesGateAndEdgeRemovalsInTheSameLevel()
        {
            demo.LoadPuzzle(6);
            yield return null; // Let Unity destroy the previous fixture's inactive hierarchy.
            Assert.That(demo.Rules.EnableCubeEdgeExits && demo.Rules.EnableDirectionalGates, Is.True);
            var greenBody = root.transform.Find("Stage 2 runtime objects/Arrow 20/Body").GetComponent<SurfacePathRenderer>();
            var greenColor = greenBody.SharedMaterial.color;
            Assert.That(demo.TryActivate(20), Is.True);
            Assert.That(demo.LastPlan.Outcome, Is.EqualTo(MovementOutcome.BlockedByPath));
            yield return WaitForIdle();
            Assert.That(greenBody.SharedMaterial.color, Is.EqualTo(greenColor));
            foreach (int id in new[] { 40,10,20,30 })
            {
                Assert.That(demo.TryActivate(id), Is.True);
                Assert.That(demo.LastPlan.ExitType, Is.EqualTo(id == 40 ? ExitType.DirectionalGate : ExitType.CubeEdge));
                Assert.That(demo.Grid.Paths.ContainsKey(id), Is.True);
                yield return WaitForIdle();
                Assert.That(demo.Grid.Paths.ContainsKey(id), Is.False);
            }
            Assert.That(demo.Grid.OccupiedCellCount, Is.Zero);
            Assert.That(demo.Grid.OccupiedSegmentCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator IndependentExitSwitchesCancelOldMovesAndSurviveRestart()
        {
            demo.LoadPuzzle(6);
            Assert.That(demo.TryActivate(40), Is.True);
            yield return null;
            var oldGrid = demo.Grid;
            demo.ConfigureExits(true,false);
            Assert.That(demo.IsAnimating, Is.False);
            Assert.That(demo.Grid, Is.Not.SameAs(oldGrid));
            Assert.That(demo.Rules.EnableCubeEdgeExits, Is.True);
            Assert.That(demo.Rules.EnableDirectionalGates, Is.False);
            Assert.That(demo.TryActivate(40), Is.True);
            Assert.That(demo.LastPlan.ExitType, Is.EqualTo(ExitType.CubeEdge));
            yield return WaitForIdle();
            Assert.That(oldGrid.Paths.ContainsKey(40), Is.True, "Rule changes cancel the old animation transaction.");
            demo.RestartPuzzle();
            Assert.That(demo.Grid.Paths.Count, Is.EqualTo(4));
            Assert.That(demo.Rules.EnableCubeEdgeExits, Is.True);
            Assert.That(demo.Rules.EnableDirectionalGates, Is.False);
            demo.ConfigureExits(false,true);
            Assert.That(demo.TryActivate(40), Is.True);
            Assert.That(demo.LastPlan.ExitType, Is.EqualTo(ExitType.DirectionalGate));
            yield return WaitForIdle();
            demo.RestartPuzzle();
            Assert.That(demo.Rules.EnableCubeEdgeExits, Is.False);
            Assert.That(demo.Rules.EnableDirectionalGates, Is.True);
        }
    }
}
