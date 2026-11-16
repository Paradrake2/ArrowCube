using System;
using System.Collections;
using System.IO;
using ArrowCube.Game;
using ArrowCube.Controls;
using ArrowCube.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ArrowCube.Tests
{
    public class GenericSurfaceInteractionTests
    {
        private readonly InputTestFixture devices=new InputTestFixture();
        private GameObject root; private GameplayController game; private string folder; private Mouse mouse; private Touchscreen touch;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            devices.Setup(); mouse=InputSystem.AddDevice<Mouse>(); touch=InputSystem.AddDevice<Touchscreen>();
            folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/GenericSurfaceInteraction",Guid.NewGuid().ToString("N")));
            CreateRoot(); yield return null; yield return null;
            var demo=AuthoredSurfaceDemos.CreateL(); game.LoadPuzzle(demo.Puzzle,1,true,demo.Geometry); game.Movement.AnimationCellsPerSecond=30; game.Input.NotifyFocus(true); yield return null;
        }
        private void CreateRoot()
        {
            root=new GameObject("Generic surface interaction"); root.SetActive(false); game=root.AddComponent<GameplayController>();
            game.SavePathOverride=Path.Combine(folder,"player.json"); root.SetActive(true);
        }
        [UnityTearDown]
        public IEnumerator Cleanup() { Object.Destroy(root); yield return null; devices.TearDown(); if(Directory.Exists(folder)) Directory.Delete(folder,true); }
        private Vector2 Point(int pathId)
        {
            var path=game.Session.Grid.Paths[pathId]; var geometry=game.Movement.SurfaceGeometry;
            game.Input.Orbit.FocusCell(geometry,path.HeadCellId); for(int i=0;i<25;i++) game.Input.Orbit.Tick(.1f);
            return game.Movement.InspectionCamera.WorldToScreenPoint(root.transform.TransformPoint(geometry.GetCellCenter(path.HeadCellId,geometry.ReferenceCellSize*.04f)));
        }
        private IEnumerator Idle()
        { float deadline=Time.realtimeSinceStartup+5; while(game.Movement.IsAnimating && Time.realtimeSinceStartup<deadline) yield return null; Assert.That(game.Movement.IsAnimating, Is.False); }
        [UnityTest]
        public IEnumerator RealMouseAndTouchClearTheNonCubePuzzleThroughTheGameplayController()
        {
            var point=Point(20);
            InputSystem.QueueStateEvent(mouse,new MouseState {position=point,buttons=1}); InputSystem.QueueStateEvent(mouse,new MouseState {position=point,buttons=0}); InputSystem.Update();
            Assert.That(game.Input.AcceptedActivations, Is.EqualTo(1)); yield return Idle(); Assert.That(game.Session.Grid.Paths.ContainsKey(20), Is.False);
            point=Point(10); devices.BeginTouch(1,point,screen:touch); devices.EndTouch(1,point,screen:touch);
            Assert.That(game.Input.AcceptedActivations, Is.EqualTo(2)); yield return Idle(); Assert.That(game.Session.Grid.Paths.ContainsKey(10), Is.False);
            point=Point(30); devices.BeginTouch(2,point,screen:touch); devices.EndTouch(2,point,screen:touch); yield return Idle();
            Assert.That(game.Page, Is.EqualTo(GamePage.Victory)); Assert.That(game.Session.Grid.Paths, Is.Empty); Assert.That(game.Session.Mistakes, Is.Zero);
            Assert.That(game.Undo(), Is.True); Assert.That(game.Session.Grid.Paths.ContainsKey(30), Is.True);
        }
        [UnityTest]
        public IEnumerator GenericSaveRestoresAuthoredGeometryAndHintsWithoutCubeCoordinates()
        {
            Assert.That(game.Hint(), Is.True); Assert.That(game.Session.HintUses, Is.EqualTo(1));
            game.ArmHammer(); Assert.That(game.ActivatePath(20), Is.True); game.Save();
            Object.Destroy(root); yield return null; CreateRoot(); yield return null; yield return null;
            Assert.That(game.ContinueSandbox(), Is.True); yield return null;
            Assert.That(game.Movement.SurfaceGeometry, Is.InstanceOf<QuadSurfaceGeometry>()); Assert.That(game.Session.Grid.Paths.ContainsKey(20), Is.False);
            Assert.That(game.Undo(), Is.True); Assert.That(game.Session.Grid.Paths.ContainsKey(20), Is.True);
            var point=Point(20); Assert.That(new PathPicker(game.Movement).TryPick(point,2,out int id), Is.True); Assert.That(id, Is.EqualTo(20));
        }
    }
}
