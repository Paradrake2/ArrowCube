using System;
using System.Collections;
using System.IO;
using ArrowCube.Game;
using ArrowCube.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace ArrowCube.Tests
{
    public class SurfaceGeometrySceneTests
    {
        private string folder;
        [UnitySetUp]
        public IEnumerator Setup()
        { folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Temp/SurfaceSceneTests",Guid.NewGuid().ToString("N"))); SceneManager.sceneLoaded+=Loaded; yield return null; }
        private void Loaded(Scene scene,LoadSceneMode mode)
        { foreach(var root in scene.GetRootGameObjects()) { var game=root.GetComponent<GameplayController>(); if(game!=null) game.SavePathOverride=Path.Combine(folder,"demo.json"); } }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded-=Loaded;
            for(int i=SceneManager.sceneCount-1;i>=0;i--) { var scene=SceneManager.GetSceneAt(i); if(scene.name=="SurfaceL" || scene.name=="SurfacePrism") yield return SceneManager.UnloadSceneAsync(scene); }
            if(Directory.Exists(folder)) Directory.Delete(folder,true);
        }
        [UnityTest]
        public IEnumerator BothDeliveredScenesStartPlayableAndTheSelectorLoadsEitherShape()
        {
            foreach(string name in new[] {"SurfaceL","SurfacePrism"})
            {
                yield return SceneManager.LoadSceneAsync(name,LoadSceneMode.Additive); yield return null; yield return null;
                var demo=Object.FindFirstObjectByType<SurfaceGeometryDemo>(); var game=demo.GetComponent<GameplayController>();
                Assert.That(game.Page, Is.EqualTo(GamePage.Game)); Assert.That(game.Input.enabled, Is.True);
                Assert.That(game.Movement.SurfaceGeometry, Is.InstanceOf<QuadSurfaceGeometry>());
                Assert.That(demo.FoldedPrism, Is.EqualTo(name=="SurfacePrism"));
                Assert.That(game.Session.Grid.Paths.Count, Is.EqualTo(3)); Assert.That(game.Movement.GetComponentInChildren<SurfaceMeshRenderer>(), Is.Not.Null);
                Button switchShape=null;
                foreach(var button in demo.GetComponentsInChildren<Button>(true)) if(button.name==(name=="SurfaceL" ? "Oblique prism" : "L-shaped solid")) switchShape=button;
                Assert.That(switchShape, Is.Not.Null); switchShape.onClick.Invoke(); yield return null; yield return null;
                Assert.That(demo.FoldedPrism, Is.EqualTo(name=="SurfaceL"));
                Assert.That(game.Hint(), Is.True,"The normal hint/focus behavior must work in the delivered scene.");
                game.Movement.AnimationCellsPerSecond=100;
                foreach(int id in new[] {20,10,30})
                {
                    Assert.That(game.ActivatePath(id), Is.True); float deadline=Time.realtimeSinceStartup+4;
                    while(game.Movement.IsAnimating && Time.realtimeSinceStartup<deadline) yield return null;
                    Assert.That(game.Movement.IsAnimating, Is.False);
                }
                Assert.That(game.Page, Is.EqualTo(GamePage.Victory)); yield return SceneManager.UnloadSceneAsync(name);
            }
        }
    }
}
