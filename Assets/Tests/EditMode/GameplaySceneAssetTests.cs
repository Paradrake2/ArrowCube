using System.IO;
using System.Linq;
using ArrowCube.Game;
using ArrowCube.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ArrowCube.Tests
{
    public class GameplaySceneAssetTests
    {
        [Test]
        public void GameAndSettingsLabAreDistinctEnabledScenesAndOnlyGameContainsTheGameplayController()
        {
            var paths=EditorBuildSettings.scenes.Where(scene=>scene.enabled).Select(scene=>scene.path).ToArray();
            Assert.That(paths, Does.Contain("Assets/Scenes/Game.unity")); Assert.That(paths, Does.Contain("Assets/Scenes/TestScene.unity"));
            Assert.That(AssetDatabase.AssetPathToGUID("Assets/Scenes/Game.unity"), Is.Not.EqualTo(AssetDatabase.AssetPathToGUID("Assets/Scenes/TestScene.unity")));
            var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Game.unity");
            try
            {
                var roots=scene.GetRootGameObjects();
                Assert.That(roots.SelectMany(root=>root.GetComponentsInChildren<GameplayController>(true)).Count(), Is.EqualTo(1));
                Assert.That(roots.SelectMany(root=>root.GetComponentsInChildren<Stage3GenerationDemo>(true)), Is.Empty);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            string lab=File.ReadAllText("Assets/Scenes/TestScene.unity");
            Assert.That(lab, Does.Contain("ArrowCube.Game.LabGameplayBridge"));
            Assert.That(lab, Does.Not.Contain("ArrowCube.Game.GameplayController"));
        }
    }
}
