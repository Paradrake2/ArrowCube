using System.Collections;
using System.Collections.Generic;
using ArrowCube.Controls;
using ArrowCube.Presentation;
using ArrowCube.Topology;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace ArrowCube.Tests
{
    public class PuzzleRenderingTests
    {
        [UnityTest]
        public IEnumerator CubeFacesHaveDistinctOpaqueColorsOutwardWindingAndNoShadowReception()
        {
            var root=new GameObject("Colored cube regression"); var movement=root.AddComponent<Stage2MovementDemo>();
            yield return null;
            try
            {
                var surface=root.GetComponentInChildren<ColoredCubeSurface>(); Assert.That(surface, Is.Not.Null);
                var renderer=surface.GetComponent<MeshRenderer>(); var mesh=surface.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(6)); Assert.That(mesh.subMeshCount, Is.EqualTo(6));
                Assert.That(renderer.receiveShadows, Is.False); Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                for(int face=0;face<6;face++)
                {
                    var material=renderer.sharedMaterials[face]; var color=material.color;
                    Assert.That(material.shader.name, Is.EqualTo(PuzzleMaterials.ShaderName)); Assert.That(color.a, Is.EqualTo(1));
                    Assert.That(Mathf.Min(color.r,color.g,color.b), Is.GreaterThan(.5f),"Keep dark arrows readable on every face.");
                    for(int other=face+1;other<6;other++) Assert.That(color, Is.Not.EqualTo(renderer.sharedMaterials[other].color));
                    var expected=CubeSurfaceGeometry.ToVector(movement.Puzzle.Topology.GetOrientation((CubeFace)face).Normal);
                    var indices=mesh.GetTriangles(face); var vertices=mesh.vertices;
                    var normal=Vector3.Cross(vertices[indices[1]]-vertices[indices[0]],vertices[indices[2]]-vertices[indices[0]]).normalized;
                    Assert.That(Vector3.Dot(normal,expected), Is.GreaterThan(.999f));
                    foreach(int vertex in indices) Assert.That(Vector3.Dot(vertices[vertex],expected), Is.EqualTo(2).Within(.0001f));
                }
            }
            finally { Object.Destroy(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator OrbitDoesNotCreateOrDeleteArrowsAndEveryPuzzleRendererUsesOpaqueMaterials()
        {
            var root=new GameObject("Opaque rendering regression"); var movement=root.AddComponent<Stage2MovementDemo>();
            yield return null; movement.LoadPuzzle(6); var input=root.AddComponent<PuzzleInputManager>(); yield return null;
            try
            {
                long revision=movement.Grid.Revision; int count=movement.Grid.Paths.Count;
                var original=new Dictionary<Mesh,Vector3[]>();
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>()) original.Add(filter.sharedMesh,filter.sharedMesh.vertices);
                for(int face=0;face<6;face++)
                {
                    input.Orbit.FocusFace(movement.Puzzle.Topology,(CubeFace)face);
                    for(int i=0;i<20;i++) input.Orbit.Tick(.1f);
                    yield return null;
                    Assert.That(movement.Grid.Revision, Is.EqualTo(revision)); Assert.That(movement.Grid.Paths.Count, Is.EqualTo(count));
                    foreach(var pair in original) Assert.That(pair.Key.vertices, Is.EqualTo(pair.Value));
                    foreach(var renderer in root.GetComponentsInChildren<Renderer>())
                    {
                        Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo(PuzzleMaterials.ShaderName));
                        Assert.That(renderer.sharedMaterial.renderQueue, Is.EqualTo((int)RenderQueue.Geometry));
                    }
                }
            }
            finally { Object.Destroy(root); }
            yield return null;
        }
    }
}
