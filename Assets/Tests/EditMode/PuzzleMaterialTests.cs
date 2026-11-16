using ArrowCube.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArrowCube.Tests
{
    public class PuzzleMaterialTests
    {
        [Test]
        public void PlayerShaderIsExplicitlyIncludedOpaqueAndCompilesWithoutErrors()
        {
            var shader=Resources.Load<Shader>("ArrowCubeOpaque"); Assert.That(shader, Is.Not.Null);
            Assert.That(shader.name, Is.EqualTo(PuzzleMaterials.ShaderName));
            foreach(var message in ShaderUtil.GetShaderMessages(shader)) Assert.That(message.severity.ToString(), Is.Not.EqualTo("Error"),message.message);
            var material=PuzzleMaterials.Create(Color.black);
            try
            {
                Assert.That(material.GetTag("RenderType",false), Is.EqualTo("Opaque"));
                Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Geometry)); Assert.That(material.color, Is.EqualTo(Color.black));
                material.color=Color.red; Assert.That(material.color, Is.EqualTo(Color.red));
            }
            finally { Object.DestroyImmediate(material); }
        }
    }
}
