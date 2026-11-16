using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArrowCube.Controls
{
    /// <summary>Defer taps until release; vertical drags scroll without editing the setting.</summary>
    public sealed class ScrollAwareSlider : Slider,IBeginDragHandler,IEndDragHandler
    {
        private bool pending,dragStarted,forwarding;
        private ScrollRect scroll;
        public override void OnPointerDown(PointerEventData data)
        {
            if(data.button!=PointerEventData.InputButton.Left || !IsActive() || !IsInteractable()) return;
            pending=true; dragStarted=forwarding=false; scroll=GetComponentInParent<ScrollRect>(); Select();
        }
        public override void OnInitializePotentialDrag(PointerEventData data) { data.useDragThreshold=true; }
        public void OnBeginDrag(PointerEventData data)
        {
            if(!pending) return;
            dragStarted=true; var delta=data.position-data.pressPosition;
            forwarding=scroll!=null && Mathf.Abs(delta.y)>Mathf.Abs(delta.x);
            if(forwarding) scroll.OnBeginDrag(data); else base.OnPointerDown(data);
        }
        public override void OnDrag(PointerEventData data)
        {
            if(!pending) return;
            if(forwarding) scroll.OnDrag(data); else if(dragStarted) base.OnDrag(data);
        }
        public void OnEndDrag(PointerEventData data)
        { if(forwarding && scroll!=null) scroll.OnEndDrag(data); forwarding=false; }
        public override void OnPointerUp(PointerEventData data)
        {
            if(data.button!=PointerEventData.InputButton.Left) return;
            if(pending && !dragStarted) base.OnPointerDown(data);
            base.OnPointerUp(data); pending=false;
        }
    }
}
