using System;
using System.IO;
using System.Linq;
using ArrowCube.Game;
using ArrowCube.Gameplay;
using ArrowCube.Generation;
using ArrowCube.Movement;
using ArrowCube.Topology;
using NUnit.Framework;
using UnityEngine;

namespace ArrowCube.Tests
{
    public class SurfacePuzzleSaveTests
    {
        [Test]
        public void LegacyCubeJsonStillRestoresTheSameIdsGatesAndRemoval()
        {
            const string legacy="{\"Name\":\"Legacy cube\",\"GridSize\":3,\"EdgeExits\":true,\"DirectionalGates\":false,\"Paths\":[{\"Id\":7,\"Heading\":0,\"Cells\":[1,0]}],\"Gates\":[]}";
            var saved=JsonUtility.FromJson<PuzzleSave>(legacy); var puzzle=saved.Restore();
            Assert.That(puzzle.Surface, Is.InstanceOf<CubeTopology>()); Assert.That(puzzle.Topology.GridSize, Is.EqualTo(3));
            Assert.That(puzzle.Paths[0].CellIds, Is.EqualTo(new[] {1,0})); Assert.That(puzzle.Paths[0].Head, Is.EqualTo(new SurfaceCoordinate(CubeFace.Front,1,0)));
            var recaptured=PuzzleSave.Capture(puzzle); Assert.That(recaptured.SurfaceFormat, Is.Zero);
            Assert.That(recaptured.Paths[0].Cells, Is.EqualTo(saved.Paths[0].Cells)); Assert.That(recaptured.GridSize, Is.EqualTo(3));
            var grid=puzzle.CreateGrid(); var plan=new PathMovementSimulator().Simulate(grid,7,puzzle.Exits,puzzle.Rules);
            Assert.That(plan.StepCount, Is.EqualTo(3)); Assert.That(grid.TryCommit(plan), Is.True);
        }

        [Test]
        public void GenericTopologyAndExactPuzzleSurviveJsonWithSessionUndoAndReload()
        {
            var puzzle=ManualSurfacePuzzle.Create(); var options=new GameplayOptions {UnlimitedAttempts=false,StartingHearts=3};
            var session=new GameplaySession(puzzle,options);
            var blocked=session.BeginAttempt(10); Assert.That(session.CompleteAttempt(blocked), Is.True);
            Assert.That(session.Hearts, Is.EqualTo(2)); Assert.That(session.Mistakes, Is.EqualTo(1)); Assert.That(session.Hint(), Is.EqualTo(30));
            var remove=session.BeginAttempt(30); Assert.That(session.CompleteAttempt(remove), Is.True);
            var active=new ActiveLevelSave {Sandbox=true,Puzzle=PuzzleSave.Capture(puzzle),Session=session.Capture()};
            var restored=JsonUtility.FromJson<ActiveLevelSave>(JsonUtility.ToJson(active)).Restore();
            Assert.That(restored.Puzzle.Surface, Is.InstanceOf<SurfaceTopology>()); Assert.That(restored.Grid.Paths.Keys, Is.EquivalentTo(new[] {10,20}));
            Assert.That(restored.Hearts, Is.EqualTo(2)); Assert.That(restored.Mistakes, Is.EqualTo(1));
            foreach(int id in puzzle.Surface.EnumerateCellIds())
            for(int d=0;d<4;d++) Assert.That(restored.Puzzle.Surface.Step(id,(SurfaceDirection)d), Is.EqualTo(puzzle.Surface.Step(id,(SurfaceDirection)d)));
            Assert.That(restored.Puzzle.Exits.CellGates, Is.EqualTo(puzzle.Exits.CellGates));
            Assert.That(restored.Undo(), Is.True); Assert.That(restored.Grid.Paths.Keys, Is.EquivalentTo(new[] {10,20,30}));
            foreach(int id in ManualSurfacePuzzle.RemovalOrder) Assert.That(restored.CompleteAttempt(restored.BeginAttempt(id)), Is.True);
            Assert.That(restored.State, Is.EqualTo(PlayState.Victory)); Assert.That(restored.Grid.OccupiedCellCount+restored.Grid.OccupiedSegmentCount, Is.Zero);
        }

        [Test]
        public void GenericPuzzleWorksWithChecksummedPlayerSaveAndBackup()
        {
            string folder=Path.Combine(Application.dataPath,"../Temp/SurfaceSaveTests",Guid.NewGuid().ToString("N"));
            try
            {
                var puzzle=ManualSurfacePuzzle.Create(); var session=new GameplaySession(puzzle);
                var save=new PlayerSave {Active=new ActiveLevelSave {Sandbox=true,Puzzle=PuzzleSave.Capture(puzzle),Session=session.Capture()} };
                var store=new LocalSaveStore(Path.Combine(folder,"player.json")); store.Save(save); store.Save(save);
                var loaded=store.Load(out var warning); Assert.That(loaded, Is.Not.Null,warning); Assert.That(loaded.Active.Restore().Hint(), Is.EqualTo(30));
                File.WriteAllText(store.Path,"broken"); loaded=store.Load(out warning);
                Assert.That(loaded, Is.Not.Null,warning); Assert.That(warning, Does.Contain("backup"));
                Assert.That(new PuzzleValidator().Validate(loaded.Active.Puzzle.Restore(),ManualSurfacePuzzle.RemovalOrder).Succeeded, Is.True);
            }
            finally { if(Directory.Exists(folder)) Directory.Delete(folder,true); }
        }

        [Test]
        public void ParallelBodyConnectionsSurviveGenericSerialization()
        {
            var builder=new SurfaceTopologyBuilder().AddCell(1).AddCell(2);
            builder.Connect(1,SurfaceDirection.Right,2,SurfaceDirection.Right).Connect(1,SurfaceDirection.Left,2,SurfaceDirection.Left);
            builder.Connect(1,SurfaceDirection.Up,1,SurfaceDirection.Up).Connect(2,SurfaceDirection.Up,2,SurfaceDirection.Up);
            var surface=builder.Build();
            var path=new ArrowPath(1,surface,new[] {1,2},SurfaceDirection.Right,new[] {SurfaceDirection.Left});
            var puzzle=new PuzzleDefinition("Parallel boundaries",surface,new[] {path},new ExitMap(surface,new[] {new CellGate(1,SurfaceDirection.Right)}),ExitRules.GateOnly);
            var restored=JsonUtility.FromJson<PuzzleSave>(JsonUtility.ToJson(PuzzleSave.Capture(puzzle))).Restore();
            Assert.That(restored.Paths[0].BodyDirections, Is.EqualTo(new[] {SurfaceDirection.Left}));
            Assert.That(restored.Paths[0].CellSegments[0], Is.EqualTo(new CellSegment(restored.Surface,1,SurfaceDirection.Left)));
            Assert.That(new PuzzleValidator().Validate(restored).Succeeded, Is.True);
        }

        [Test]
        public void CorruptSurfaceSnapshotsDoNotBecomeDifferentValidPuzzles()
        {
            var saved=PuzzleSave.Capture(ManualSurfacePuzzle.Create()); saved.SurfaceFormat=999;
            Assert.Throws<ArgumentException>(()=>saved.Restore());
            saved=PuzzleSave.Capture(ManualSurfacePuzzle.Create()); saved.SurfaceCells[0].Boundaries[0].Neighbor=999;
            Assert.Throws<ArgumentException>(()=>saved.Restore());
            saved=PuzzleSave.Capture(ManualSurfacePuzzle.Create()); saved.SurfaceCells[0].Boundaries[0].Direction=9;
            Assert.Throws<ArgumentException>(()=>saved.Restore());
            saved=PuzzleSave.Capture(ManualSurfacePuzzle.Create()); saved.SurfaceCells[0].Boundaries.RemoveAt(0);
            Assert.Throws<ArgumentException>(()=>saved.Restore());
            saved=PuzzleSave.Capture(ManualSurfacePuzzle.Create()); saved.Gates[0].Cell=999;
            Assert.Throws<ArgumentException>(()=>saved.Restore());
        }
    }
}
