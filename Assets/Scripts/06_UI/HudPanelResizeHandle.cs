using UnityEngine;
using UnityEngine.EventSystems;

namespace SpaceGame.UI
{
    /// <summary>
    /// Zieh-Griff unten rechts zum Skalieren eines DraggableHudPanel.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class HudPanelResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private DraggableHudPanel _panel;
        private Vector2 _startSize;
        private Vector2 _startPointerLocal;

        public void Initialize(DraggableHudPanel panel) => _panel = panel;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_panel == null) return;

            _panel.BeginResize();
            _startSize = _panel.CurrentSize;

            var parent = _panel.ParentRect;
            if (parent == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, eventData.position, _panel.UiCamera, out _startPointerLocal);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_panel == null) return;

            var parent = _panel.ParentRect;
            if (parent == null) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, eventData.position, _panel.UiCamera, out var localPoint))
                return;

            var delta = localPoint - _startPointerLocal;
            _panel.ResizeFromBottomRight(_startSize + new Vector2(delta.x, -delta.y));
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _panel?.EndResize();
        }
    }
}
