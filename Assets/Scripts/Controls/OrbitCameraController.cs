using System;
using ArrowCube.Topology;
using ArrowCube.Presentation;
using UnityEngine;

namespace ArrowCube.Controls
{
    /// <summary>Free quaternion orbit. No Euler pitch clamp, pole singularity, or inaccessible underside.</summary>
    public sealed class OrbitCameraController : MonoBehaviour
    {
        [SerializeField,Range(.1f,3)] private float sensitivity=1;
        [SerializeField,Range(.05f,2f)]
        [Tooltip("Mouse-wheel zoom strength per notch. Higher values zoom faster. Changes apply immediately in Play mode.")]
        private float zoomSpeed=.35f;
        [SerializeField,Min(.1f)] private float smoothing=16;
        [SerializeField] private bool inertia;
        private Camera view;
        private Transform target;
        private Quaternion currentRotation,targetRotation,defaultRotation;
        private float currentZoom,targetZoom,defaultZoom,minZoom,maxZoom,distance;
        private Vector2 velocity;
        private bool dragging;
        public Camera View => view;
        public float Sensitivity { get => sensitivity; set => sensitivity=Mathf.Clamp(value,.1f,3); }
        public float ZoomSpeed
        {
            get => zoomSpeed;
            set
            {
                if(float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
                zoomSpeed=Mathf.Clamp(value,.05f,2f);
            }
        }
        public bool InertiaEnabled { get => inertia; set { inertia=value; if(!value) velocity=Vector2.zero; } }
        public float Zoom => currentZoom;
        public float TargetZoom => targetZoom;
        public Quaternion TargetRotation => targetRotation;
        public Quaternion Rotation => currentRotation;
        public void RestoreView(Quaternion rotation,float zoom)
        {
            currentRotation=targetRotation=rotation.normalized; currentZoom=targetZoom=Mathf.Clamp(zoom,minZoom,maxZoom);
            velocity=Vector2.zero; dragging=false; ApplyPose();
        }

        public void Configure(Camera camera,Transform center,float cubeSideLength)
        {
            view=camera!=null ? camera : throw new ArgumentNullException(nameof(camera));
            target=center!=null ? center : throw new ArgumentNullException(nameof(center));
            float scale=Mathf.Max(Mathf.Abs(center.lossyScale.x),Mathf.Abs(center.lossyScale.y),Mathf.Abs(center.lossyScale.z));
            float side=Mathf.Max(.1f,cubeSideLength*scale);
            var offset=center.TransformDirection(new Vector3(7,6,9).normalized);
            defaultRotation=Quaternion.LookRotation(-offset,center.up);
            distance=side*3.25f;
            defaultZoom=view.orthographic ? side*1.1f : distance;
            minZoom=view.orthographic ? side*.25f : side*.95f;
            maxZoom=view.orthographic ? side*3 : side*12;
            ResetView(true);
        }

        public void ResetView(bool immediate=false)
        {
            targetRotation=defaultRotation; targetZoom=defaultZoom; velocity=Vector2.zero; dragging=false;
            if(immediate) { currentRotation=targetRotation; currentZoom=targetZoom; ApplyPose(); }
        }
        public void StopMotion()
        { targetRotation=currentRotation; targetZoom=currentZoom; velocity=Vector2.zero; dragging=false; }

        public void BeginDrag() { StopMotion(); dragging=true; }
        public void Drag(Vector2 pixelDelta,float viewportShortSide,float elapsed)
        {
            if(view==null) return;
            Vector2 degrees=pixelDelta*(180*sensitivity/Mathf.Max(100,viewportShortSide));
            Rotate(degrees);
            velocity=Vector2.ClampMagnitude(degrees/Mathf.Max(.008f,elapsed),540);
        }
        public void EndDrag() { dragging=false; if(!inertia) velocity=Vector2.zero; }
        public void ZoomByScroll(float notches) => ScaleZoom(Mathf.Exp(-Mathf.Clamp(notches,-20,20)*zoomSpeed));
        public void ScaleZoom(float ratio)
        {
            if(view==null || float.IsNaN(ratio) || float.IsInfinity(ratio) || ratio<=0) return;
            targetZoom=Mathf.Clamp(targetZoom*ratio,minZoom,maxZoom);
        }
        public void FocusFace(CubeTopology topology,CubeFace face)
        {
            if(view==null || target==null) return;
            var frame=topology.GetOrientation(face);
            targetRotation=Quaternion.LookRotation(-target.TransformDirection(CubeSurfaceGeometry.ToVector(frame.Normal)),
                target.TransformDirection(CubeSurfaceGeometry.ToVector(frame.Up)));
            velocity=Vector2.zero; dragging=false;
        }
        private void Rotate(Vector2 degrees) => targetRotation=(targetRotation*Quaternion.Euler(degrees.y,-degrees.x,0)).normalized;
        private void LateUpdate() => Tick(Time.unscaledDeltaTime);
        public void Tick(float unscaledDeltaTime)
        {
            if(view==null || target==null || unscaledDeltaTime<=0) return;
            float dt=Mathf.Min(unscaledDeltaTime,.1f);
            if(inertia && !dragging && velocity.sqrMagnitude>.01f)
            { Rotate(velocity*dt); velocity*=Mathf.Exp(-6*dt); }
            float blend=1-Mathf.Exp(-Mathf.Max(.1f,smoothing)*dt);
            currentRotation=Quaternion.Slerp(currentRotation,targetRotation,blend);
            currentZoom=Mathf.Lerp(currentZoom,targetZoom,blend); ApplyPose();
        }
        private void ApplyPose()
        {
            if(view==null || target==null) return;
            view.transform.SetPositionAndRotation(target.position-currentRotation*Vector3.forward*(view.orthographic ? distance : currentZoom),currentRotation);
            if(view.orthographic) view.orthographicSize=currentZoom;
        }
    }
}
