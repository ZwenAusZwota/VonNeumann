// Assets/Scripts/UI/DraggableHudPanel.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpaceGame.UI
{
    /// <summary>
    /// Verschiebbares HUD-Panel mit X-Schließen & dauerhafter JSON-Persistenz
    /// (Position und Sichtbarkeit) pro Panel-ID.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DraggableHudPanel : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Identifikation & Canvas")]
        [Tooltip("Eindeutige ID – wird als Schlüssel für Speicherung genutzt. Leer lassen = GameObject-Name.")]
        public string panelId = "";
        [Tooltip("Root-Canvas, der das Panel enthält. Wenn leer, wird automatisch gesucht.")]
        public Canvas rootCanvas;

        [Header("Interaktion")]
        [Tooltip("Button in der rechten oberen Ecke zum Schließen.")]
        public Button closeButton;
        [Tooltip("Optional: Panelbewegung an Canvas-Grenzen festklemmen.")]
        public bool clampToCanvas = true;
        [Tooltip("Sichtbarkeit mit speichern/laden? (Initialzustand in Start())")]
        public bool rememberVisibility = true;

        private RectTransform _rect;
        private Vector2 _dragOffset;
        private Camera _uiCamera;

        private void AssignDefaultPanelIdIfEmpty()
        {
            if (string.IsNullOrWhiteSpace(panelId))
                panelId = gameObject.name; // eindeutiger Key pro Panel
        }

        private void OnValidate() => AssignDefaultPanelIdIfEmpty();

        private void Awake()
        {
            AssignDefaultPanelIdIfEmpty();

            _rect = GetComponent<RectTransform>();
            if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
            _uiCamera = (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                        ? rootCanvas.worldCamera : null;

            if (closeButton != null)
                closeButton.onClick.AddListener(ClosePanel);
        }

        private void Start()
        {
            // --- Layout einmalig laden (Position + Sichtbarkeit) ---
            var s = HUDPanelLayoutStore.Load(panelId);
            if (s != null)
            {
                _rect.anchoredPosition = new Vector2(s.x, s.y);
                if (clampToCanvas) ClampToCanvas();

                if (rememberVisibility && !s.visible)
                    gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            // Beim Deaktivieren aktuellen Zustand wegspeichern
            SaveCurrentState();
        }

        private void OnApplicationQuit()
        {
            SaveCurrentState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rect, eventData.position, _uiCamera, out var localPoint);
            _dragOffset = localPoint;
        }

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (rootCanvas == null) return;

            var parent = _rect.parent as RectTransform;
            if (parent == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, eventData.position, _uiCamera, out var localPointParent))
            {
                Vector2 targetAnchored = localPointParent - _dragOffset;
                _rect.anchoredPosition = targetAnchored;
                if (clampToCanvas) ClampToCanvas();
            }
        }

        public void OnEndDrag(PointerEventData eventData) => SavePosition();

        public void ClosePanel()
        {
            if (rememberVisibility)
                HUDPanelLayoutStore.Save(panelId, _rect.anchoredPosition, false);

            gameObject.SetActive(false);
        }

        public void ShowPanel()
        {
            gameObject.SetActive(true);
            if (rememberVisibility)
                HUDPanelLayoutStore.Save(panelId, _rect.anchoredPosition, true);
        }

        public void SavePosition()
        {
            HUDPanelLayoutStore.Save(panelId, _rect.anchoredPosition, gameObject.activeSelf);
        }

        public void ResetSavedLayout()
        {
            HUDPanelLayoutStore.Delete(panelId);
        }

        private void SaveCurrentState()
        {
            HUDPanelLayoutStore.Save(panelId, _rect.anchoredPosition, gameObject.activeSelf);
        }

        private void ClampToCanvas()
        {
            var parent = _rect.parent as RectTransform;
            if (parent == null) return;

            var panelSize = _rect.rect.size;
            var parentSize = parent.rect.size;

            Vector2 pos = _rect.anchoredPosition;

            float minX = -parentSize.x * 0.5f + panelSize.x * _rect.pivot.x;
            float maxX = parentSize.x * 0.5f - panelSize.x * (1f - _rect.pivot.x);
            float minY = -parentSize.y * 0.5f + panelSize.y * _rect.pivot.y;
            float maxY = parentSize.y * 0.5f - panelSize.y * (1f - _rect.pivot.y);

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);

            _rect.anchoredPosition = pos;
        }
    }
}
