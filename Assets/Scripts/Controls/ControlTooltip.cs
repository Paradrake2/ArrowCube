using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ArrowCube.Controls
{
    /// <summary>Mouse hover and touch press help without capturing gameplay gestures.</summary>
    public sealed class ControlTooltip : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler
    {
        public string Message;
        public Action<string> Show;
        public void OnPointerEnter(PointerEventData data) => Show?.Invoke(Message);
        public void OnPointerExit(PointerEventData data) => Show?.Invoke(null);
        public void OnPointerDown(PointerEventData data) => Show?.Invoke(Message);
        public void OnPointerUp(PointerEventData data) => Show?.Invoke(null);
        private void OnDisable() => Show?.Invoke(null);
    }
}
