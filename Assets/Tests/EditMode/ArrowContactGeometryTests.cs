using ArrowCube.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace ArrowCube.Tests
{
    public class ArrowContactGeometryTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void CoplanarTipFindsTheFirstVisibleTriangleEdgeInEitherWinding(bool reverse)
        {
            var mesh=Triangle(reverse);
            try { Assert.That(ArrowContactGeometry.FirstContact(Vector3.left,Vector3.right,mesh), Is.EqualTo(.5f).Within(.00001f)); }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void TipDoesNotHitADifferentFacePlaneOrTriangleOutsideItsTravel()
        {
            var mesh=Triangle(false);
            try
            {
                Assert.That(ArrowContactGeometry.FirstContact(Vector3.left+Vector3.forward,Vector3.right+Vector3.forward,mesh), Is.EqualTo(float.PositiveInfinity));
                Assert.That(ArrowContactGeometry.FirstContact(new Vector3(-1,3,0),new Vector3(1,3,0),mesh), Is.EqualTo(float.PositiveInfinity));
                Assert.That(ArrowContactGeometry.FirstContact(Vector3.left,Vector3.left*.5f,mesh), Is.EqualTo(float.PositiveInfinity));
                Assert.That(ArrowContactGeometry.FirstContact(Vector3.left,Vector3.left,mesh), Is.EqualTo(float.PositiveInfinity));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void NonCoplanarTravelHitsOnlyInsideTheTriangle()
        {
            var mesh=Triangle(false);
            try
            {
                Assert.That(ArrowContactGeometry.FirstContact(new Vector3(.1f,0,-1),new Vector3(.1f,0,1),mesh), Is.EqualTo(.5f).Within(.00001f));
                Assert.That(ArrowContactGeometry.FirstContact(new Vector3(-1,0,-1),new Vector3(-1,0,1),mesh), Is.EqualTo(float.PositiveInfinity));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        private static Mesh Triangle(bool reverse) => new Mesh
        {
            vertices=new[] { new Vector3(0,-1,0),new Vector3(1,-1,0),new Vector3(0,1,0) },
            triangles=reverse ? new[] { 2,1,0 } : new[] { 0,1,2 }
        };
    }
}
