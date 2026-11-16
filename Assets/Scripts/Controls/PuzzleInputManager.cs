using System;
using System.Collections.Generic;
using ArrowCube.Movement;
using ArrowCube.Presentation;
using ArrowCube.Topology;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Touch=UnityEngine.InputSystem.EnhancedTouch.Touch;
using InputTouchPhase=UnityEngine.InputSystem.TouchPhase;

namespace ArrowCube.Controls
{
    [RequireComponent(typeof(Stage2MovementDemo),typeof(OrbitCameraController))]
    public sealed class PuzzleInputManager : MonoBehaviour
    {
        [SerializeField,Tooltip("Reverse both axes of mouse drag rotation.")]
        private bool invertMouseRotation;
        [SerializeField,Tooltip("Reverse both axes of one-finger drag rotation. Independent of mouse controls and pinch zoom.")]
        private bool invertTouchRotation;
        private Stage2MovementDemo movement;
        private OrbitCameraController orbit;
        private PathPicker picker;
        private readonly PointerGesture left=new PointerGesture(),right=new PointerGesture(),singleTouch=new PointerGesture();
        private readonly Dictionary<Finger,Contact> contacts=new Dictionary<Finger,Contact>();
        private readonly List<RaycastResult> uiHits=new List<RaycastResult>();
        private PointerEventData uiPointer;
        private EventSystem pointerEventSystem;
        private bool hasFocus=true,mouseNeedsRelease,multiTouch,touchBlocked;
        private float pinchDistance,lastTouchTime=float.NegativeInfinity;
        private InputAction[] actions;
        private sealed class Contact { public Vector2 Position; public bool Blocked; }
        public Func<Vector2,bool> AdditionalUIBlocker { get; set; }
        public Func<int,bool> PathActivationHandler { get; set; }
        public event Action FullscreenToggleRequested;
        public OrbitCameraController Orbit => orbit;
        public int AcceptedActivations { get; private set; }
        public bool InvertMouseRotation { get => invertMouseRotation; set { if(invertMouseRotation==value) return; CancelActiveGestures(); invertMouseRotation=value; } }
        public bool InvertTouchRotation { get => invertTouchRotation; set { if(invertTouchRotation==value) return; CancelActiveGestures(); invertTouchRotation=value; } }

        private void Awake()
        {
            movement=GetComponent<Stage2MovementDemo>(); orbit=GetComponent<OrbitCameraController>(); picker=new PathPicker(movement);
            var position=new InputAction("Pointer position",InputActionType.PassThrough,"<Mouse>/position");
            var primary=new InputAction("Activate or drag",InputActionType.Button,"<Mouse>/leftButton");
            var secondary=new InputAction("Orbit",InputActionType.Button,"<Mouse>/rightButton");
            var wheel=new InputAction("Zoom",InputActionType.PassThrough,"<Mouse>/scroll");
            var reset=new InputAction("Reset camera",InputActionType.Button,"<Keyboard>/r");
            var fullscreen=new InputAction("Fullscreen",InputActionType.Button,"<Keyboard>/f11");
            position.performed+=ctx=>MoveMouse(ctx.ReadValue<Vector2>());
            primary.performed+=ctx=>BeginMouse(false,ctx.time); primary.canceled+=ctx=>EndMouse(false,ctx.time);
            secondary.performed+=ctx=>BeginMouse(true,ctx.time); secondary.canceled+=ctx=>EndMouse(true,ctx.time);
            wheel.performed+=ctx=>Scroll(ctx.ReadValue<Vector2>().y);
            reset.performed+=ctx=> { if(hasFocus && !TypingInUI()) { CancelActiveGestures(); orbit.ResetView(); } };
            fullscreen.performed+=ctx=>
            {
                if(!hasFocus || TypingInUI()) return;
                if(FullscreenToggleRequested!=null) FullscreenToggleRequested.Invoke();
                else Screen.fullScreenMode=Screen.fullScreen ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
            };
            actions=new[] { position,primary,secondary,wheel,reset,fullscreen };
        }
        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            Touch.onFingerDown+=FingerDown; Touch.onFingerMove+=FingerMove; Touch.onFingerUp+=FingerUp;
            movement.PuzzleLoaded+=PuzzleLoaded; movement.FaceFocusRequested+=FocusFace;
            InputSystem.onDeviceChange+=DeviceChanged;
            foreach(var finger in Touch.activeFingers)
                contacts[finger]=new Contact { Position=finger.screenPosition,Blocked=true };
            touchBlocked=contacts.Count>0;
            mouseNeedsRelease=Mouse.current!=null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed);
            if(movement.Puzzle!=null) PuzzleLoaded(movement.Puzzle);
            foreach(var action in actions) action.Enable();
        }
        private void OnDisable()
        {
            movement.PuzzleLoaded-=PuzzleLoaded; movement.FaceFocusRequested-=FocusFace;
            InputSystem.onDeviceChange-=DeviceChanged;
            Touch.onFingerDown-=FingerDown; Touch.onFingerMove-=FingerMove; Touch.onFingerUp-=FingerUp;
            CancelActiveGestures(); contacts.Clear();
            foreach(var action in actions) action.Disable();
            EnhancedTouchSupport.Disable();
        }
        private void OnDestroy() { foreach(var action in actions) action.Dispose(); }
        private void PuzzleLoaded(PuzzleDefinition _)
        {
            CancelActiveGestures();
            orbit.Configure(movement.InspectionCamera,movement.transform,movement.SurfaceSideLength);
        }
        private void FocusFace(CubeFace face)
        { CancelActiveGestures(); orbit.FocusFace(movement.Puzzle.Topology,face); }
        public void CancelActiveGestures()
        {
            left.Cancel(); right.Cancel(); singleTouch.Cancel(); if(orbit!=null) orbit.StopMotion();
            if(contacts.Count>0) { touchBlocked=true; foreach(var contact in contacts.Values) contact.Blocked=true; }
            mouseNeedsRelease=Mouse.current!=null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed);
        }
        public void NotifyFocus(bool focused) { hasFocus=focused; if(!focused) CancelActiveGestures(); }
        private void OnApplicationFocus(bool focused) => NotifyFocus(focused);
        private void OnApplicationPause(bool paused) { if(paused) CancelActiveGestures(); }
        private void Update() => ProcessFrame();
        public void ProcessFrame()
        {
            var mouse=Mouse.current;
            if(mouse==null) return;
            if(mouseNeedsRelease && !mouse.leftButton.isPressed && !mouse.rightButton.isPressed) mouseNeedsRelease=false;
            MoveMouse(mouse.position.ReadValue());
        }
        private bool MouseAllowed => hasFocus && movement.Puzzle!=null && orbit.View!=null && contacts.Count==0
            && Time.unscaledTime-lastTouchTime>=.25f && !mouseNeedsRelease;
        private void BeginMouse(bool secondary,double time)
        {
            if(!MouseAllowed || Mouse.current==null) return;
            var point=Mouse.current.position.ReadValue(); bool blocked=IsBlocked(point);
            if(secondary)
            {
                left.Cancel(); if(!blocked) orbit.StopMotion();
                right.Begin(point,time,8*DpiScale,-1,true,blocked);
            }
            else if(!right.Active && !Mouse.current.rightButton.isPressed)
            {
                int candidate=blocked ? -1 : Pick(point,8*DpiScale);
                if(!blocked) orbit.StopMotion();
                left.Begin(point,time,8*DpiScale,candidate,candidate<0,blocked);
            }
        }
        private void MoveMouse(Vector2 point)
        {
            if(!MouseAllowed) return;
            if(right.Active) Move(right,point,!invertMouseRotation); else if(left.Active) Move(left,point,!invertMouseRotation);
        }
        private void EndMouse(bool secondary,double time)
        {
            if(Mouse.current==null) return;
            // Input System cancels actions on reset/focus loss before resetting their device state.
            // A still-pressed button is a cancellation, never a physical release/click.
            if(secondary ? Mouse.current.rightButton.isPressed : Mouse.current.leftButton.isPressed)
            { (secondary ? right : left).Cancel(); orbit.StopMotion(); mouseNeedsRelease=true; return; }
            if(!MouseAllowed) { (secondary ? right : left).Cancel(); ProcessFrame(); return; }
            var point=Mouse.current.position.ReadValue();
            var gesture=secondary ? right : left;
            if(!gesture.Active) return;
            Move(gesture,point,!invertMouseRotation);
            int activate=gesture.End(point,time,secondary || IsBlocked(point),secondary ? -1 : Pick(point,8*DpiScale));
            orbit.EndDrag(); Activate(activate);
        }
        private void DeviceChanged(InputDevice device,InputDeviceChange change)
        {
            if(!(device is Mouse) && !(device is Touchscreen)) return;
            if(change!=InputDeviceChange.Removed && change!=InputDeviceChange.Disconnected && change!=InputDeviceChange.Disabled
                && change!=InputDeviceChange.SoftReset && change!=InputDeviceChange.HardReset) return;
            CancelActiveGestures();
            if(device is Touchscreen) { contacts.Clear(); multiTouch=false; touchBlocked=false; }
        }
        private void Scroll(float amount)
        {
            if(!MouseAllowed || Mouse.current==null || IsBlocked(Mouse.current.position.ReadValue()) || Mathf.Abs(amount)<.01f) return;
            // Input System 1.20 normalizes the wheel to one unit per notch by default.
            // Only the optional legacy Windows range still needs conversion from 120 units.
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if(InputSystem.settings.scrollDeltaBehavior==InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange) amount/=120;
#endif
            left.SuppressTap(); right.SuppressTap(); orbit.ZoomByScroll(amount);
        }

        private void FingerDown(Finger finger)
        {
            var touch=finger.currentTouch; lastTouchTime=Time.unscaledTime;
            bool blocked=!hasFocus || movement.Puzzle==null || orbit.View==null || IsBlocked(touch.screenPosition);
            contacts[finger]=new Contact { Position=touch.screenPosition,Blocked=blocked };
            left.Cancel(); right.Cancel(); orbit.StopMotion();
            if(contacts.Count==1)
            {
                multiTouch=false; touchBlocked=blocked;
                singleTouch.Begin(touch.screenPosition,touch.time,12*DpiScale,blocked ? -1 : Pick(touch.screenPosition,14*DpiScale),true,blocked);
            }
            else
            {
                singleTouch.Cancel(); multiTouch=true;
                if(blocked || contacts.Count>2) touchBlocked=true;
                foreach(var contact in contacts.Values) if(contact.Blocked) touchBlocked=true;
                pinchDistance=ContactDistance();
            }
        }
        private void FingerMove(Finger finger)
        {
            if(!contacts.TryGetValue(finger,out var contact)) return;
            lastTouchTime=Time.unscaledTime; contact.Position=finger.currentTouch.screenPosition;
            if(!hasFocus || touchBlocked) return;
            if(contacts.Count==2 && multiTouch)
            {
                float next=ContactDistance();
                if(pinchDistance>1 && next>1) orbit.ScaleZoom(Mathf.Clamp(pinchDistance/next,.5f,2));
                pinchDistance=next;
            }
            else if(contacts.Count==1 && !multiTouch) Move(singleTouch,contact.Position,!invertTouchRotation);
        }
        private void FingerUp(Finger finger)
        {
            if(!contacts.ContainsKey(finger)) return;
            var touch=finger.currentTouch; lastTouchTime=Time.unscaledTime;
            if(contacts.Count==1 && !multiTouch && !touchBlocked && hasFocus)
            {
                Move(singleTouch,touch.screenPosition,!invertTouchRotation);
                int activate=singleTouch.End(touch.screenPosition,touch.time,
                    touch.phase==InputTouchPhase.Canceled || IsBlocked(touch.screenPosition),Pick(touch.screenPosition,14*DpiScale));
                orbit.EndDrag(); Activate(activate);
            }
            contacts.Remove(finger);
            if(contacts.Count==0) { singleTouch.Cancel(); multiTouch=false; touchBlocked=false; orbit.EndDrag(); }
            // After a pinch, the remaining finger is suppressed until all contacts have lifted.
        }
        private float ContactDistance()
        {
            Vector2 a=default; bool first=true;
            foreach(var contact in contacts.Values) { if(first) { a=contact.Position; first=false; } else return Vector2.Distance(a,contact.Position); }
            return 0;
        }
        private void Move(PointerGesture gesture,Vector2 position,bool invertDrag)
        {
            bool wasDragging=gesture.Dragging;
            var delta=gesture.Move(position);
            if(delta.sqrMagnitude<=0) return;
            if(!wasDragging) orbit.BeginDrag();
            orbit.Drag(invertDrag ? -delta : delta,Mathf.Min(orbit.View.pixelWidth,orbit.View.pixelHeight),Time.unscaledDeltaTime);
        }
        private void Activate(int id)
        {
            if(id<0) return;
            if(movement.DebugInspectDependencies) { movement.InspectDependency(id); return; }
            if(PathActivationHandler!=null ? PathActivationHandler(id) : movement.TryActivate(id)) AcceptedActivations++;
        }
        private int Pick(Vector2 position,float tolerance) => picker.TryPick(position,tolerance,out int id) ? id : -1;
        private float DpiScale => Screen.dpi>0 ? Mathf.Clamp(Screen.dpi/96,1,3) : 1;
        public bool IsBlocked(Vector2 point)
        {
            if(orbit.View==null || !orbit.View.pixelRect.Contains(point) || AdditionalUIBlocker!=null && AdditionalUIBlocker(point)) return true;
            var system=EventSystem.current;
            if(system==null) return false;
            if(pointerEventSystem!=system) { pointerEventSystem=system; uiPointer=new PointerEventData(system); }
            uiPointer.Reset(); uiPointer.position=point; uiHits.Clear(); system.RaycastAll(uiPointer,uiHits);
            foreach(var hit in uiHits)
                if(hit.module is GraphicRaycaster || hit.module!=null && hit.module.GetType().Name=="PanelRaycaster") return true;
            return false;
        }
        private static bool TypingInUI()
        {
            var selected=EventSystem.current==null ? null : EventSystem.current.currentSelectedGameObject;
            return selected!=null && (selected.GetComponent<InputField>()!=null || selected.GetComponent("TMP_InputField")!=null);
        }
    }
}
