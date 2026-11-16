using ArrowCube.Controls;
using ArrowCube.Topology;
using NUnit.Framework;
using UnityEngine;

namespace ArrowCube.Tests
{
    public class ControlsGeometryTests
    {
        [Test]
        public void CleanPressAndReleaseRequiresTheSamePathAndShortDuration()
        {
            var gesture=new PointerGesture();
            gesture.Begin(Vector2.zero,1,8,7,false,false);
            Assert.That(gesture.End(new Vector2(2,1),1.2,false,7), Is.EqualTo(7));
            gesture.Begin(Vector2.zero,1,8,7,false,false);
            Assert.That(gesture.End(Vector2.zero,1.2,false,8), Is.EqualTo(-1));
            gesture.Begin(Vector2.zero,1,8,7,false,false);
            Assert.That(gesture.End(Vector2.zero,2,false,7), Is.EqualTo(-1));
        }
        [Test]
        public void DraggingOutAndBackOrMovingOnlyOnReleaseCannotBecomeATap()
        {
            var gesture=new PointerGesture();
            gesture.Begin(Vector2.zero,0,8,7,true,false);
            Assert.That(gesture.Move(new Vector2(20,0)).x, Is.EqualTo(20));
            gesture.Move(Vector2.zero);
            Assert.That(gesture.End(Vector2.zero,.2,false,7), Is.EqualTo(-1));
            gesture.Begin(Vector2.zero,0,8,7,false,false);
            Assert.That(gesture.End(new Vector2(20,0),.2,false,7), Is.EqualTo(-1));
        }
        [Test]
        public void UiOriginCancellationAndSuppressionCannotLeakIntoAnotherClick()
        {
            var gesture=new PointerGesture();
            gesture.Begin(Vector2.zero,0,8,7,true,true);
            Assert.That(gesture.Move(new Vector2(20,0)), Is.EqualTo(Vector2.zero));
            Assert.That(gesture.End(Vector2.zero,.2,false,7), Is.EqualTo(-1));
            gesture.Begin(Vector2.zero,0,8,7,true,false); gesture.SuppressTap();
            Assert.That(gesture.End(Vector2.zero,.2,false,7), Is.EqualTo(-1));
            gesture.Begin(Vector2.zero,0,8,7,true,false); gesture.Cancel();
            Assert.That(gesture.End(Vector2.zero,.2,false,7), Is.EqualTo(-1));
        }
        [TestCase(1280,720)] [TestCase(720,1280)] [TestCase(640,360)] [TestCase(360,640)]
        [TestCase(2560,1440)] [TestCase(320,240)]
        public void ResponsiveViewportAndPanelNeverOverlapOrEscapeTheSafeArea(int width,int height)
        {
            var safe=new Rect(12,20,width-24,height-40);
            var layout=PuzzleScreenLayout.Calculate(safe);
            Assert.That(layout.Panel.width, Is.GreaterThan(0)); Assert.That(layout.View.height, Is.GreaterThan(0));
            Assert.That(layout.Panel.Overlaps(layout.View), Is.False);
            Assert.That(safe.Contains(layout.Panel.min), Is.True); Assert.That(safe.Contains(layout.View.min), Is.True);
            Assert.That(layout.Panel.xMax, Is.LessThanOrEqualTo(safe.xMax)); Assert.That(layout.Panel.yMax, Is.LessThanOrEqualTo(safe.yMax));
            Assert.That(layout.View.xMax, Is.LessThanOrEqualTo(safe.xMax)); Assert.That(layout.View.yMax, Is.LessThanOrEqualTo(safe.yMax));
        }
        [Test]
        public void HighDpiPortraitKeepsControlsReadableWithoutCoveringThePuzzle()
        {
            var safe=new Rect(0,80,1080,1760);
            var layout=PuzzleScreenLayout.Calculate(safe,2.5f);
            Assert.That(layout.Panel.height, Is.GreaterThan(400));
            Assert.That(layout.Panel.Overlaps(layout.View), Is.False);
            Assert.That(layout.View.height, Is.GreaterThan(1000));
        }
        [TestCase(CubeFace.Front)] [TestCase(CubeFace.Right)] [TestCase(CubeFace.Back)]
        [TestCase(CubeFace.Left)] [TestCase(CubeFace.Top)] [TestCase(CubeFace.Bottom)]
        public void OrbitCanInspectAllSixFaces(CubeFace face)
        {
            var root=new GameObject("Orbit test"); var cameraObject=new GameObject("Camera");
            try
            {
                var camera=cameraObject.AddComponent<Camera>(); camera.orthographic=true;
                var orbit=root.AddComponent<OrbitCameraController>(); orbit.Configure(camera,root.transform,4);
                var topology=new CubeTopology(6); orbit.FocusFace(topology,face);
                for(int i=0;i<20;i++) orbit.Tick(.1f);
                var normal=topology.GetOrientation(face).Normal;
                var outward=new Vector3(normal.X,normal.Y,normal.Z);
                Assert.That(Vector3.Dot((camera.transform.position-root.transform.position).normalized,outward), Is.GreaterThan(.999f));
                Assert.That(Vector3.Dot(camera.transform.forward,-outward), Is.GreaterThan(.999f));
            }
            finally { Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(root); }
        }
        [Test]
        public void VerticalDragPassesBothPolesAndZoomResetRemainsBounded()
        {
            var root=new GameObject("Orbit test"); var cameraObject=new GameObject("Camera");
            try
            {
                var camera=cameraObject.AddComponent<Camera>(); camera.orthographic=true;
                var orbit=root.AddComponent<OrbitCameraController>(); orbit.Configure(camera,root.transform,4);
                var initial=orbit.TargetRotation; float originalZoom=orbit.TargetZoom;
                orbit.FocusFace(new CubeTopology(6),CubeFace.Front); for(int i=0;i<20;i++) orbit.Tick(.1f);
                orbit.BeginDrag(); orbit.Drag(new Vector2(0,250),500,.1f); for(int i=0;i<20;i++) orbit.Tick(.1f);
                Assert.That(camera.transform.position.y, Is.GreaterThan(12));
                orbit.Drag(new Vector2(0,500),500,.1f); for(int i=0;i<20;i++) orbit.Tick(.1f);
                Assert.That(camera.transform.position.y, Is.LessThan(-12));
                orbit.EndDrag(); orbit.ScaleZoom(.00001f); Assert.That(orbit.TargetZoom, Is.GreaterThanOrEqualTo(1));
                orbit.ScaleZoom(100000); Assert.That(orbit.TargetZoom, Is.LessThanOrEqualTo(12));
                orbit.InertiaEnabled=true; orbit.BeginDrag(); orbit.Drag(new Vector2(40,20),500,.02f); orbit.EndDrag();
                var before=orbit.TargetRotation; orbit.Tick(.05f);
                Assert.That(Quaternion.Angle(before,orbit.TargetRotation), Is.GreaterThan(.1f));
                orbit.ResetView(true);
                Assert.That(Quaternion.Angle(initial,orbit.TargetRotation), Is.LessThan(.001f));
                Assert.That(orbit.TargetZoom, Is.EqualTo(originalZoom));
                orbit.Tick(.1f); Assert.That(Quaternion.Angle(initial,orbit.TargetRotation), Is.LessThan(.001f));
            }
            finally { Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(root); }
        }
    }
}
