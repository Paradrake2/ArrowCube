using System.Collections;
using ArrowCube.Controls;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using ArrowCube.Topology;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace ArrowCube.Tests
{
    public class ControlsInputTests
    {
        private readonly InputTestFixture devices=new InputTestFixture();
        private GameObject root;
        private Stage2MovementDemo movement;
        private PuzzleInputManager input;
        private Mouse mouse;
        private Keyboard keyboard;
        private Touchscreen touch;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            devices.Setup(); mouse=InputSystem.AddDevice<Mouse>(); keyboard=InputSystem.AddDevice<Keyboard>(); touch=InputSystem.AddDevice<Touchscreen>();
            root=new GameObject("Controls input test"); movement=root.AddComponent<Stage2MovementDemo>(); movement.LoadPuzzle(0);
            input=root.AddComponent<PuzzleInputManager>(); input.NotifyFocus(true);
            movement.InspectionCamera.rect=new Rect(0,0,1,1); movement.AnimationCellsPerSecond=1000;
            input.Orbit.FocusFace(movement.Puzzle.Topology,CubeFace.Front); Settle();
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown() { Object.Destroy(root); yield return null; devices.TearDown(); }
        private Vector2 Head => movement.InspectionCamera.WorldToScreenPoint(new CubeSurfaceGeometry(movement.Puzzle.Topology,4).GetWorldPosition(movement.Puzzle.Paths[0].Head,root.transform,.03f));
        private Vector2 Empty => new Vector2(Screen.width*.90f,Screen.height*.90f);
        private void Settle() { for(int i=0;i<20;i++) input.Orbit.Tick(.1f); }
        private void MouseState(Vector2 position,bool left=false,bool right=false,float scroll=0,bool queue=false)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState { position=position,buttons=(ushort)((left ? 1 : 0)|(right ? 2 : 0)),scroll=new Vector2(0,scroll) });
            if(!queue) InputSystem.Update();
        }
        [UnityTest]
        public IEnumerator ARealInputSystemMouseClickIncludingSameUpdatePressAndReleaseActivatesOnePath()
        {
            MouseState(Head,left:true,queue:true); MouseState(Head,queue:true); InputSystem.Update();
            Assert.That(input.AcceptedActivations, Is.EqualTo(1)); Assert.That(movement.LastPlan.Path.Id, Is.EqualTo(10));
            float deadline=Time.realtimeSinceStartup+3;
            while(movement.IsAnimating && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(movement.Grid.Paths, Is.Empty);
        }
        [UnityTest]
        public IEnumerator LeftPathDragsDoNotActivateWhileEmptyAndRightDragsOrbit()
        {
            var initial=input.Orbit.TargetRotation; var point=Head;
            MouseState(point,left:true); MouseState(point+new Vector2(70,0),left:true); MouseState(point);
            Assert.That(input.AcceptedActivations, Is.Zero);
            Assert.That(Quaternion.Angle(initial,input.Orbit.TargetRotation), Is.LessThan(.01f));
            MouseState(Empty,left:true); MouseState(Empty-new Vector2(80,40),left:true); MouseState(Empty-new Vector2(80,40));
            Assert.That(Quaternion.Angle(initial,input.Orbit.TargetRotation), Is.GreaterThan(1));
            var leftForward=Quaternion.Inverse(initial)*(input.Orbit.TargetRotation*Vector3.forward);
            Assert.That(leftForward.x, Is.LessThan(0),"Desktop left-drag horizontal rotation is inverted.");
            Assert.That(leftForward.y, Is.LessThan(0),"Desktop left-drag vertical rotation is inverted.");
            Settle(); point=Head; var before=input.Orbit.TargetRotation;
            MouseState(point,right:true); MouseState(point+new Vector2(60,20),right:true); MouseState(point+new Vector2(60,20));
            Assert.That(Quaternion.Angle(before,input.Orbit.TargetRotation), Is.GreaterThan(1));
            var rightForward=Quaternion.Inverse(before)*(input.Orbit.TargetRotation*Vector3.forward);
            Assert.That(rightForward.x, Is.GreaterThan(0),"Desktop right-drag horizontal rotation is inverted.");
            Assert.That(rightForward.y, Is.GreaterThan(0),"Desktop right-drag vertical rotation is inverted.");
            Assert.That(input.AcceptedActivations, Is.Zero); yield return null;
        }
        [UnityTest]
        public IEnumerator UiOriginAndFocusLossNeverActivateOrRotateThePuzzle()
        {
            var point=Head; var rotation=input.Orbit.TargetRotation; float zoom=input.Orbit.TargetZoom;
            input.AdditionalUIBlocker=_=>true;
            MouseState(point,left:true); MouseState(point+new Vector2(50,0),left:true); MouseState(point);
            MouseState(point,scroll:1);
            Assert.That(input.AcceptedActivations, Is.Zero); Assert.That(input.Orbit.TargetZoom, Is.EqualTo(zoom));
            Assert.That(Quaternion.Angle(rotation,input.Orbit.TargetRotation), Is.LessThan(.01f));
            input.AdditionalUIBlocker=null;
            MouseState(point,left:true); input.NotifyFocus(false); MouseState(point); input.NotifyFocus(true);
            Assert.That(input.AcceptedActivations, Is.Zero); yield return null;
        }
        [UnityTest]
        public IEnumerator WheelResetAndFullscreenShortcutUseInputSystemActions()
        {
            float zoom=input.Orbit.TargetZoom; MouseState(Empty,scroll:1);
            Assert.That(input.Orbit.TargetZoom, Is.EqualTo(zoom*Mathf.Exp(-input.Orbit.ZoomSpeed)).Within(.00001f));
            Settle();
            MouseState(Empty,right:true); MouseState(Empty-new Vector2(80,0),right:true); MouseState(Empty-new Vector2(80,0));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.R)); InputSystem.Update();
            Assert.That(input.Orbit.TargetZoom, Is.EqualTo(zoom));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update();
            int fullscreen=0; input.FullscreenToggleRequested+=()=>fullscreen++;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.F11)); InputSystem.Update();
            Assert.That(keyboard.f11Key.isPressed, Is.True);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update();
            Assert.That(fullscreen, Is.EqualTo(1)); yield return null;
        }
        [UnityTest]
        public IEnumerator ZoomSpeedCanBeTunedLiveAndNormalizedWheelInputIsNotDividedTwice()
        {
            float original=input.Orbit.TargetZoom;
            input.Orbit.ZoomSpeed=.2f; MouseState(Empty,scroll:1);
            float slower=input.Orbit.TargetZoom;
            Assert.That(slower, Is.EqualTo(original*Mathf.Exp(-.2f)).Within(.00001f));
            input.Orbit.ResetView(true);
            JsonUtility.FromJsonOverwrite("{\"zoomSpeed\":0.7}",input.Orbit);
            MouseState(Empty,scroll:1);
            float faster=input.Orbit.TargetZoom;
            Assert.That(faster, Is.EqualTo(original*Mathf.Exp(-.7f)).Within(.00001f));
            Assert.That(faster, Is.LessThan(slower));
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            InputSystem.settings.scrollDeltaBehavior=InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange;
            input.Orbit.ResetView(true); MouseState(Empty,scroll:120);
            Assert.That(input.Orbit.TargetZoom, Is.EqualTo(faster).Within(.00001f),"Legacy Windows and normalized wheel ranges must give the same zoom per notch.");
#endif
            yield return null;
        }
        [UnityTest]
        public IEnumerator RealTouchTapActivatesAndTouchDragStartingOnAPathOnlyOrbits()
        {
            var point=Head; var rotation=input.Orbit.TargetRotation;
            devices.BeginTouch(1,point,screen:touch); devices.MoveTouch(1,point+new Vector2(70,20),screen:touch);
            var touchForward=Quaternion.Inverse(rotation)*(input.Orbit.TargetRotation*Vector3.forward);
            Assert.That(touchForward.x, Is.GreaterThan(0),"Touch horizontal rotation now matches desktop by default.");
            Assert.That(touchForward.y, Is.GreaterThan(0),"Touch vertical rotation now matches desktop by default.");
            devices.EndTouch(1,point,screen:touch);
            Assert.That(input.AcceptedActivations, Is.Zero);
            Assert.That(Quaternion.Angle(rotation,input.Orbit.TargetRotation), Is.GreaterThan(1));
            input.Orbit.FocusFace(movement.Puzzle.Topology,CubeFace.Front); Settle();
            point=Head; devices.BeginTouch(2,point,screen:touch); devices.EndTouch(2,point,screen:touch);
            Assert.That(input.AcceptedActivations, Is.EqualTo(1)); yield return null;
        }
        [UnityTest]
        public IEnumerator MouseAndTouchInversionReverseBothAxesIndependently()
        {
            var initial=input.Orbit.TargetRotation; var point=Empty-new Vector2(80,40); var end=point+new Vector2(60,20);
            Assert.That(input.InvertMouseRotation || input.InvertTouchRotation, Is.False);
            MouseState(point,right:true); MouseState(end,right:true); MouseState(end);
            var normalMouse=input.Orbit.TargetRotation;
            input.Orbit.RestoreView(initial,input.Orbit.TargetZoom); input.InvertMouseRotation=true;
            MouseState(point,right:true); MouseState(end,right:true); MouseState(end);
            var invertedMouse=Quaternion.Inverse(initial)*(input.Orbit.TargetRotation*Vector3.forward);
            Assert.That(invertedMouse.x, Is.LessThan(0)); Assert.That(invertedMouse.y, Is.LessThan(0));
            Assert.That(Quaternion.Angle(initial,input.Orbit.TargetRotation), Is.EqualTo(Quaternion.Angle(initial,normalMouse)).Within(.001f));

            input.Orbit.RestoreView(initial,input.Orbit.TargetZoom);
            devices.BeginTouch(1,point,screen:touch); devices.MoveTouch(1,end,screen:touch); devices.EndTouch(1,end,screen:touch);
            Assert.That(Quaternion.Angle(normalMouse,input.Orbit.TargetRotation), Is.LessThan(.01f),"Mouse preference must not change touch rotation.");
            input.Orbit.RestoreView(initial,input.Orbit.TargetZoom); input.InvertTouchRotation=true;
            devices.BeginTouch(2,point,screen:touch); devices.MoveTouch(2,end,screen:touch); devices.EndTouch(2,end,screen:touch);
            var invertedTouch=Quaternion.Inverse(initial)*(input.Orbit.TargetRotation*Vector3.forward);
            Assert.That(invertedTouch.x, Is.LessThan(0)); Assert.That(invertedTouch.y, Is.LessThan(0));
            Assert.That(input.InvertMouseRotation, Is.True); Assert.That(input.AcceptedActivations, Is.Zero);
            yield return null;
        }
        [UnityTest]
        public IEnumerator ChangingDirectionDuringATouchCancelsItsPendingTap()
        {
            var point=Head; var rotation=input.Orbit.TargetRotation;
            devices.BeginTouch(1,point,screen:touch); input.InvertTouchRotation=true;
            devices.MoveTouch(1,point+new Vector2(70,20),screen:touch); devices.EndTouch(1,point,screen:touch);
            Assert.That(input.AcceptedActivations, Is.Zero);
            Assert.That(Quaternion.Angle(rotation,input.Orbit.TargetRotation), Is.LessThan(.01f));
            yield return null;
        }
        [UnityTest]
        public IEnumerator PinchZoomAndTwoToOneTransitionNeverProduceATap()
        {
            var point=Head; float zoom=input.Orbit.TargetZoom;
            devices.BeginTouch(1,point,screen:touch); devices.BeginTouch(2,point+new Vector2(80,0),screen:touch);
            devices.MoveTouch(2,point+new Vector2(150,0),screen:touch);
            Assert.That(input.Orbit.TargetZoom, Is.LessThan(zoom));
            devices.EndTouch(2,point+new Vector2(150,0),screen:touch);
            var before=input.Orbit.TargetRotation;
            devices.MoveTouch(1,point+new Vector2(80,0),screen:touch); devices.EndTouch(1,point,screen:touch);
            Assert.That(input.AcceptedActivations, Is.Zero);
            Assert.That(Quaternion.Angle(before,input.Orbit.TargetRotation), Is.LessThan(.01f)); yield return null;
        }
        [UnityTest]
        public IEnumerator CancelledTouchUiFingerAndThreeFingerGesturesCannotLeakAnActivation()
        {
            var point=Head;
            devices.BeginTouch(1,point,screen:touch); devices.CancelTouch(1,point,screen:touch);
            Assert.That(input.AcceptedActivations, Is.Zero);
            input.AdditionalUIBlocker=p=>p.x>point.x+30;
            float zoom=input.Orbit.TargetZoom;
            devices.BeginTouch(2,point,screen:touch); devices.BeginTouch(3,point+new Vector2(80,0),screen:touch);
            devices.MoveTouch(3,point+new Vector2(140,0),screen:touch);
            devices.EndTouch(3,point+new Vector2(140,0),screen:touch); devices.EndTouch(2,point,screen:touch);
            Assert.That(input.Orbit.TargetZoom, Is.EqualTo(zoom));
            input.AdditionalUIBlocker=null;
            devices.BeginTouch(4,point,screen:touch); devices.BeginTouch(5,point+new Vector2(40,0),screen:touch);
            devices.BeginTouch(6,point+new Vector2(80,0),screen:touch);
            devices.EndTouch(6,point+new Vector2(80,0),screen:touch); devices.EndTouch(5,point+new Vector2(40,0),screen:touch); devices.EndTouch(4,point,screen:touch);
            Assert.That(input.AcceptedActivations, Is.Zero); yield return null;
        }
        [UnityTest]
        public IEnumerator RegeneratingOrDisablingInputDuringAPressCancelsItsPendingTap()
        {
            var point=Head; MouseState(point,left:true); movement.LoadPuzzle(0); MouseState(point);
            Assert.That(input.AcceptedActivations, Is.Zero);
            point=Head; MouseState(point,left:true); input.enabled=false; MouseState(point); input.enabled=true;
            Assert.That(input.AcceptedActivations, Is.Zero); yield return null;
        }
        [UnityTest]
        public IEnumerator DeviceResetDoesNotTurnAnInputActionCancellationIntoAClick()
        {
            var point=Head; MouseState(point,left:true); InputSystem.ResetDevice(mouse); MouseState(point);
            Assert.That(input.AcceptedActivations, Is.Zero); yield return null;
        }
        [UnityTest]
        public IEnumerator BufferedLongPressUsesDeviceTimestampsEvenWhenBothEventsArriveInOneFrame()
        {
            var point=Head; double began=devices.currentTime;
            InputSystem.QueueStateEvent(mouse,new MouseState { position=point,buttons=1 },began);
            InputSystem.QueueStateEvent(mouse,new MouseState { position=point,buttons=0 },began+.9);
            devices.currentTime=began+1; InputSystem.Update();
            Assert.That(input.AcceptedActivations, Is.Zero); yield return null;
        }
        [UnityTest]
        public IEnumerator Stage4BuildsAResponsiveTouchUiAndKeepsTheWorldViewportSeparate()
        {
            Object.Destroy(root); yield return null;
            root=new GameObject("Full stage 4 test");
            // Match scene deserialization: all required components exist before any Awake runs.
            root.SetActive(false);
            var existing=new GameObject("Existing EventSystem",typeof(UnityEngine.EventSystems.EventSystem),typeof(UnityEngine.EventSystems.StandaloneInputModule));
            existing.transform.SetParent(root.transform,false);
            var legacy=existing.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            var demo=root.AddComponent<Stage4ControlsDemo>();
            root.SetActive(true);
            yield return null; Canvas.ForceUpdateCanvases(); demo.UpdateLayout();
            float deadline=Time.realtimeSinceStartup+60;
            while(demo.Generation.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
            demo.UpdateLayout();
            input=demo.Input; movement=demo.Generation.Movement; input.NotifyFocus(true);
            Assert.That(demo.Generation.Generated, Is.Not.Null);
            Assert.That(input.IsBlocked(demo.PanelScreenRect.center), Is.True);
            Assert.That(demo.PanelScreenRect.Overlaps(movement.InspectionCamera.pixelRect), Is.False);
            Assert.That(root.GetComponentInChildren<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(), Is.Not.Null);
            Assert.That(legacy.enabled, Is.False,"The new input backend must not also poll legacy UI input.");
            UnityEngine.UI.Slider sensitivity=null;
            foreach(var slider in root.GetComponentsInChildren<UnityEngine.UI.Slider>()) if(slider.name=="Sensitivity") sensitivity=slider;
            Assert.That(sensitivity, Is.Not.Null);
            Assert.That(sensitivity.value, Is.EqualTo(input.Orbit.Sensitivity));
            sensitivity.value=2.25f;
            Assert.That(input.Orbit.Sensitivity, Is.EqualTo(2.25f));
            UnityEngine.UI.Toggle invertMouse=null,invertTouch=null;
            foreach(var toggle in root.GetComponentsInChildren<UnityEngine.UI.Toggle>(true))
            { if(toggle.name=="Invert mouse rotation") invertMouse=toggle; if(toggle.name=="Invert touch rotation") invertTouch=toggle; }
            Assert.That(invertMouse, Is.Not.Null); Assert.That(invertTouch, Is.Not.Null);
            invertTouch.isOn=true;
            Assert.That(input.InvertTouchRotation, Is.True); Assert.That(input.InvertMouseRotation, Is.False);
            invertMouse.isOn=true; invertTouch.isOn=false;
            Assert.That(input.InvertMouseRotation, Is.True); Assert.That(input.InvertTouchRotation, Is.False);
            var point=demo.PanelScreenRect.center; var rotation=input.Orbit.TargetRotation;
            devices.BeginTouch(1,point,screen:touch); devices.MoveTouch(1,point+new Vector2(40,0),screen:touch); devices.EndTouch(1,point,screen:touch);
            Assert.That(input.AcceptedActivations, Is.Zero);
            Assert.That(Quaternion.Angle(rotation,input.Orbit.TargetRotation), Is.LessThan(.01f));
            yield return null;
        }
        [UnityTest]
        public IEnumerator DependencyInspectionSelectsAnArrowWithoutChangingGameplayAndCanBeDisabled()
        {
            Object.Destroy(root); yield return null;
            root=new GameObject("Dependency inspection"); var demo=root.AddComponent<Stage4ControlsDemo>(); yield return null;
            float deadline=Time.realtimeSinceStartup+60;
            while(demo.Generation.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(demo.Generation.Generated, Is.Not.Null,demo.Generation.StatusMessage);
            input=demo.Input; movement=demo.Generation.Movement; input.NotifyFocus(true); Settle();
            var geometry=new CubeSurfaceGeometry(movement.Puzzle.Topology,4); var picker=new PathPicker(movement);
            int selected=-1; Vector2 point=default;
            foreach(var path in movement.Puzzle.Paths)
            {
                var candidate=(Vector2)movement.InspectionCamera.WorldToScreenPoint(geometry.GetWorldPosition(path.Head,root.transform,.03f));
                if(!input.IsBlocked(candidate) && picker.TryPick(candidate,1,out int id) && id==path.Id) { selected=id; point=candidate; break; }
            }
            Assert.That(selected, Is.GreaterThanOrEqualTo(0));
            int count=movement.Grid.Paths.Count; movement.DebugInspectDependencies=true;
            MouseState(point,left:true,queue:true); MouseState(point,queue:true); InputSystem.Update();
            Assert.That(movement.DebugSelectedPath, Is.EqualTo(selected)); Assert.That(input.AcceptedActivations, Is.Zero);
            Assert.That(movement.Grid.Paths.Count, Is.EqualTo(count)); Assert.That(movement.IsAnimating, Is.False);
            Assert.That(movement.DependencyDescription(), Does.Contain("Faces involved:"));
            movement.DebugInspectDependencies=false;
            MouseState(point,left:true,queue:true); MouseState(point,queue:true); InputSystem.Update();
            Assert.That(input.AcceptedActivations, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CustomSlidersPreserveOtherSettingsWhileMasterAndResetApplyCoherentDefaults()
        {
            Object.Destroy(root); yield return null;
            root=new GameObject("Advanced sliders"); var demo=root.AddComponent<Stage4ControlsDemo>();
            yield return null;
            float deadline=Time.realtimeSinceStartup+60;
            while(demo.Generation.IsGenerating && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(demo.Generation.Generated, Is.Not.Null,demo.Generation.StatusMessage);
            input=demo.Input; movement=demo.Generation.Movement;
            var author=demo.Generation.EditingProfile; int grid=author.gridSize; float coverage=author.targetCoverage;
            double target=author.difficultySettings.TargetScore;
            UnityEngine.UI.Slider turn=null,master=null,customTarget=null;
            foreach(var slider in root.GetComponentsInChildren<UnityEngine.UI.Slider>(true))
            {
                if(slider.name=="Turn probability") turn=slider;
                if(slider.name=="Master difficulty") master=slider;
                if(slider.name=="Target difficulty score") customTarget=slider;
            }
            Assert.That(turn, Is.Not.Null); Assert.That(master, Is.Not.Null); Assert.That(customTarget, Is.Not.Null);
            turn.value=.83f;
            Assert.That(author.turnProbability, Is.EqualTo(.83f).Within(.001));
            Assert.That(author.gridSize, Is.EqualTo(grid)); Assert.That(author.targetCoverage, Is.EqualTo(coverage));
            Assert.That(author.difficultySettings.TargetScore, Is.EqualTo(target));
            foreach(var slider in root.GetComponentsInChildren<UnityEngine.UI.Slider>(true))
                if(slider.name=="Maximum target blockers") slider.value=7;
            Assert.That(author.difficultySettings.MultiBlocker.MaxTargetBlockers, Is.EqualTo(7));
            Assert.That(author.difficultySettings.TargetScore, Is.EqualTo(target)); Assert.That(author.turnProbability, Is.EqualTo(.83f).Within(.001));
            // A vertical swipe on a slider belongs to the scroll view, not the setting.
            foreach(var button in root.GetComponentsInChildren<UnityEngine.UI.Button>())
                if(button.name=="Show / hide advanced settings") button.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            Assert.That(turn.handleRect.rect.height, Is.GreaterThan(0));
            Assert.That(turn.handleRect.rect.height, Is.LessThanOrEqualTo(((RectTransform)turn.transform).rect.height));
            var scrolling=(ScrollAwareSlider)turn; float beforeSwipe=turn.value;
            var swipe=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                { button=UnityEngine.EventSystems.PointerEventData.InputButton.Left,pressPosition=new Vector2(100,100),position=new Vector2(100,100) };
            scrolling.OnPointerDown(swipe); swipe.position=new Vector2(103,180); swipe.delta=new Vector2(3,80);
            scrolling.OnBeginDrag(swipe); scrolling.OnDrag(swipe); scrolling.OnPointerUp(swipe); scrolling.OnEndDrag(swipe);
            Assert.That(turn.value, Is.EqualTo(beforeSwipe));
            Assert.That(author.difficultySettings.TargetScore, Is.EqualTo(target));
            customTarget.value=63;
            Assert.That(author.difficultySettings.TargetScore, Is.EqualTo(63)); Assert.That(author.turnProbability, Is.EqualTo(.83f).Within(.001));
            var track=(RectTransform)turn.transform;
            var point=RectTransformUtility.WorldToScreenPoint(null,track.TransformPoint(new Vector3(track.rect.xMin+track.rect.width*.25f,track.rect.center.y,0)));
            var tap=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                { button=UnityEngine.EventSystems.PointerEventData.InputButton.Left,position=point,pressPosition=point };
            scrolling.OnPointerDown(tap); Assert.That(turn.value, Is.EqualTo(beforeSwipe));
            scrolling.OnPointerUp(tap); Assert.That(turn.value, Is.EqualTo(.25f).Within(.02));
            Assert.That(author.difficultySettings.TargetScore, Is.EqualTo(63));
            master.value=90;
            Assert.That(author.difficultySettings.TargetScore, Is.EqualTo(90));
            Assert.That(author.difficultySettings.DependencyStrength, Is.GreaterThan(.9));
            Assert.That(author.turnProbability, Is.Not.EqualTo(.83f));
            foreach(var button in root.GetComponentsInChildren<UnityEngine.UI.Button>())
                if(button.name=="Reset to preset defaults") button.onClick.Invoke();
            Assert.That(author.difficultySettings.TargetScore, Is.EqualTo(30));
            Assert.That(author.targetCoverage, Is.EqualTo(coverage));
            Assert.That(demo.Generation.EnableCubeEdgeExits, Is.True); Assert.That(demo.Generation.EnableDirectionalGates, Is.True);
            var before=input.Orbit.TargetRotation;
            var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            var help=turn.GetComponent<ControlTooltip>(); help.OnPointerDown(data);
            Assert.That(root.transform.Find("Stage 4 controls UI/Control panel/Parameter help").gameObject.activeSelf, Is.True);
            help.OnPointerUp(data);
            Assert.That(input.AcceptedActivations, Is.Zero); Assert.That(Quaternion.Angle(before,input.Orbit.TargetRotation), Is.LessThan(.01f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator InspectorThicknessWidensEveryArrowLiveAndTheVisibleEdgesAreClickable()
        {
            movement.ArrowThickness=.05f;
            var definition=movement.Puzzle;
            long revision=movement.Grid.Revision;
            var geometry=new CubeSurfaceGeometry(movement.Puzzle.Topology,movement.SurfaceSideLength);
            var path=movement.Puzzle.Paths[0];
            var middle=Vector3.Lerp(geometry.GetWorldPosition(path.Cells[1],root.transform,.03f),
                geometry.GetWorldPosition(path.Tail,root.transform,.03f),.5f);
            var camera=movement.InspectionCamera;
            var edge=(Vector2)camera.WorldToScreenPoint(middle+camera.transform.up*(geometry.CellSize*.4f*.45f));
            var picker=new PathPicker(movement);
            Assert.That(picker.TryPick(edge,0,out _), Is.False,"Point lies outside the old thin visible stroke.");

            // Exercise the same serialized-field update used by the Inspector, without calling the setter.
            JsonUtility.FromJsonOverwrite("{\"arrowThickness\":0.4}",movement);
            yield return null;
            Assert.That(movement.Puzzle, Is.SameAs(definition));
            Assert.That(movement.Grid.Revision, Is.EqualTo(revision),"Changing thickness must not regenerate or mutate the puzzle.");
            Assert.That(picker.TryPick(edge,0,out int selected), Is.True);
            Assert.That(selected, Is.EqualTo(path.Id));

            movement.LoadPuzzle(6); // Four arrows, including cross-face bodies and both exit mechanics.
            yield return null;
            int bodies=0,heads=0;
            foreach(var line in root.GetComponentsInChildren<SurfacePathRenderer>())
            {
                if(line.name=="Body") { bodies++; Assert.That(line.Width, Is.EqualTo(movement.ArrowBodyWidth).Within(.00001f)); }
            }
            foreach(var head in root.GetComponentsInChildren<TriangleArrowHead>())
            {
                heads++;
                var mesh=head.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertexCount, Is.EqualTo(3));
                Assert.That(mesh.triangles.Length, Is.EqualTo(6));
                Assert.That(Vector3.Distance(mesh.vertices[1],mesh.vertices[2]),
                    Is.EqualTo(TriangleArrowHead.Width(4f/8,movement.ArrowBodyWidth)).Within(.00001f));
            }
            Assert.That(bodies, Is.EqualTo(4)); Assert.That(heads, Is.EqualTo(4));
            Assert.That(movement.ArrowThickness, Is.EqualTo(.4f));
        }
        [UnityTest]
        public IEnumerator FilledTriangleInteriorIsClickableAndTheBodyJoinsItsBase()
        {
            var path=movement.Puzzle.Paths[0];
            var arrow=root.transform.Find($"Stage 2 runtime objects/Arrow {path.Id}/Arrowhead").GetComponent<TriangleArrowHead>();
            var mesh=arrow.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(arrow.GetComponent<LineRenderer>(), Is.Null,"The head is filled geometry, not an outlined V.");
            var body=root.transform.Find($"Stage 2 runtime objects/Arrow {path.Id}/Body").GetComponent<SurfacePathRenderer>();
            var baseCenter=(mesh.vertices[1]+mesh.vertices[2])*.5f;
            Assert.That(Vector3.Distance(body.GetPosition(0),baseCenter), Is.LessThan(.00001f));
            var inside=mesh.vertices[0]*.2f+mesh.vertices[1]*.7f+mesh.vertices[2]*.1f;
            var screen=(Vector2)movement.InspectionCamera.WorldToScreenPoint(root.transform.TransformPoint(inside));
            Assert.That(new PathPicker(movement).TryPick(screen,0,out int selected), Is.True);
            Assert.That(selected, Is.EqualTo(path.Id));
            MouseState(screen,left:true); MouseState(screen);
            Assert.That(input.AcceptedActivations, Is.EqualTo(1)); yield return null;
        }
        [UnityTest]
        public IEnumerator PickerRejectsHiddenPathsAndSelectsEitherVisibleFaceOfACrossFacePath()
        {
            var t=new CubeTopology(6);
            var front=new ArrowPath(1,t,new[] { new SurfaceCoordinate(CubeFace.Front,2,2),new SurfaceCoordinate(CubeFace.Front,1,2) },SurfaceDirection.Right);
            var back=new ArrowPath(2,t,new[] { new SurfaceCoordinate(CubeFace.Back,3,2),new SurfaceCoordinate(CubeFace.Back,4,2) },SurfaceDirection.Left);
            movement.LoadDefinition(new PuzzleDefinition("Occlusion",t,new[] { front,back },new ExitMap(t,System.Array.Empty<ExitGate>()),ExitRules.EdgeOnly));
            movement.InspectionCamera.rect=new Rect(0,0,1,1); input.Orbit.FocusFace(t,CubeFace.Front); Settle();
            var geometry=new CubeSurfaceGeometry(t,4); var picker=new PathPicker(movement);
            var point=(Vector2)movement.InspectionCamera.WorldToScreenPoint(geometry.GetWorldPosition(back.Head,root.transform));
            Assert.That(picker.TryPick(point,10,out int selected), Is.True); Assert.That(selected, Is.EqualTo(1));
            var onlyBack=new PuzzleDefinition("Hidden",t,new[] { back },new ExitMap(t,System.Array.Empty<ExitGate>()),ExitRules.EdgeOnly);
            movement.LoadDefinition(onlyBack); movement.InspectionCamera.rect=new Rect(0,0,1,1); input.Orbit.FocusFace(t,CubeFace.Front); Settle();
            Assert.That(picker.TryPick(point,10,out _), Is.False);
            movement.LoadPuzzle(1); movement.InspectionCamera.rect=new Rect(0,0,1,1);
            foreach(var face in new[] { CubeFace.Front,CubeFace.Right })
            {
                input.Orbit.FocusFace(movement.Puzzle.Topology,face); Settle();
                var path=movement.Puzzle.Paths[0]; var cell=path.Head;
                foreach(var candidate in path.Cells) if(candidate.Face==face) { cell=candidate; break; }
                point=movement.InspectionCamera.WorldToScreenPoint(new CubeSurfaceGeometry(movement.Puzzle.Topology,4).GetWorldPosition(cell,root.transform,.03f));
                Assert.That(picker.TryPick(point,10,out selected), Is.True); Assert.That(selected, Is.EqualTo(10));
            }
            yield return null;
        }
    }
}
