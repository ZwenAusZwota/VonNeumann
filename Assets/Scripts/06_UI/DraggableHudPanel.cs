// Assets/Scripts/UI/DraggableHudPanel.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace SpaceGame.UI
{
    /// <summary>
    /// Verschiebbares, skalierbares HUD-Panel mit X-Schließen & JSON-Persistenz
    /// (Position, Größe, Sichtbarkeit) pro Panel-ID.
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
        [Tooltip("Sichtbarkeit mit speichern/laden?")]
        public bool rememberVisibility = true;
        [Tooltip("Spieler darf die Panelgröße ändern (Griff unten rechts).")]
        public bool allowResize = true;
        [Tooltip("Position und Größe mit speichern/laden?")]
        public bool rememberLayout = true;

        [Header("Größenlimits")]
        public Vector2 minSize = new(240f, 180f);
        [Tooltip("Leer (0,0) = kein Maximum.")]
        public Vector2 maxSize = Vector2.zero;

        private const float CloseButtonSize = 28f;
        private static readonly Vector2 CloseButtonInset = new(-6f, -25f);
        private const float CloseButtonFontSize = 16f;
        private const float HeaderHeight = 50f;
        private const float ResizeHandleSize = 18f;

        private RectTransform _rect;
        private Vector2 _dragOffset;
        private Camera _uiCamera;
        private GameObject _resizeHandle;
        private bool _layoutRestored;
        private bool _isResizing;

        public RectTransform ParentRect => _rect != null ? _rect.parent as RectTransform : null;
        public Camera UiCamera => _uiCamera;
        public Vector2 CurrentSize => _rect != null ? _rect.rect.size : Vector2.zero;

        private void AssignDefaultPanelIdIfEmpty()
        {
            if (string.IsNullOrWhiteSpace(panelId))
                panelId = gameObject.name;
        }

        private void OnValidate() => AssignDefaultPanelIdIfEmpty();

        private void Awake()
        {
            AssignDefaultPanelIdIfEmpty();
            EnsureRuntimeRefs();

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(ClosePanel);
                ApplyStandardCloseButton(closeButton);
            }

            HudPanelThemeApplier.ApplyTo(transform);
        }

        private void OnEnable()
        {
            EnsureRuntimeRefs();
            if (allowResize)
                EnsureResizeHandle();
        }

        private void Start()
        {
            if (!_layoutRestored)
                ApplyInitialLayoutFromSave();
        }

        private void OnDisable()
        {
            if (rememberLayout)
                SaveCurrentState();
        }

        private void OnApplicationQuit()
        {
            if (rememberLayout)
                SaveCurrentState();
        }

        private void EnsureRuntimeRefs()
        {
            _rect ??= GetComponent<RectTransform>();
            if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
            _uiCamera = (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? rootCanvas.worldCamera
                : null;
        }

        /// <summary>Einheitliches Close-Button-Layout für alle HUD-Panels.</summary>
        public static void ApplyStandardCloseButton(Button button)
        {
            if (button == null) return;

            var header = button.transform.parent as RectTransform;
            if (header != null)
                ApplyStandardHeaderRow(header);

            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(CloseButtonSize, CloseButtonSize);
            rect.anchoredPosition = CloseButtonInset;

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.fontSize = CloseButtonFontSize;
                label.alignment = TextAlignmentOptions.Center;
            }
        }

        private static void ApplyStandardHeaderRow(RectTransform header)
        {
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 0.5f);
            header.sizeDelta = new Vector2(0f, HeaderHeight);
            header.anchoredPosition = new Vector2(0f, -(HeaderHeight * 0.5f));
        }

        /// <summary>Drei kurze Balken unten rechts – ohne Sonderzeichen in der Schrift.</summary>
        private static void ApplyResizeGripLines(RectTransform handleRect, Color lineColor)
        {
            const float lineThickness = 2f;
            const float lineLength = 5f;
            const float step = 4f;

            for (var i = 0; i < 3; i++)
            {
                var line = new GameObject($"GripLine{i}", typeof(RectTransform), typeof(Image));
                var rect = line.GetComponent<RectTransform>();
                rect.SetParent(handleRect, false);
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                rect.sizeDelta = new Vector2(lineLength + i * step, lineThickness);
                rect.anchoredPosition = new Vector2(-4f - i * step, 4f + i * step);

                var img = line.GetComponent<Image>();
                img.color = lineColor;
                img.raycastTarget = false;
            }
        }

        /// <summary>Layout aus dem Speicher anwenden (beim Szenenstart, auch wenn inaktiv).</summary>
        public void ApplyInitialLayoutFromSave()
        {
            if (!rememberLayout && !rememberVisibility) return;

            EnsureRuntimeRefs();
            var state = HUDPanelLayoutStore.Load(panelId);
            if (state == null) return;

            // Sichtbarkeit nur beim Szenenstart (Panel noch inaktiv) – nicht beim ersten manuellen Öffnen.
            bool applyVisibility = rememberVisibility && !gameObject.activeSelf;
            ApplySavedState(state, applyVisibility);
            _layoutRestored = true;
        }

        /// <summary>Kompatibilität mit bestehendem UIPanelManager-Aufruf.</summary>
        public void ApplyInitialVisibilityFromSave() => ApplyInitialLayoutFromSave();

        public void BeginResize()
        {
            EnsureRuntimeRefs();
            NormalizeToCenterAnchor();
            _isResizing = true;
        }

        public void ResizeFromBottomRight(Vector2 targetSize)
        {
            EnsureRuntimeRefs();
            SetFixedSizeKeepingTopLeft(targetSize);
            if (clampToCanvas) ClampToCanvas();
        }

        public void EndResize()
        {
            _isResizing = false;
            SaveCurrentState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isResizing) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rect, eventData.position, _uiCamera, out var localPoint);
            _dragOffset = localPoint;
        }

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isResizing || rootCanvas == null) return;

            var parent = ParentRect;
            if (parent == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, eventData.position, _uiCamera, out var localPointParent))
            {
                _rect.anchoredPosition = localPointParent - _dragOffset;
                if (clampToCanvas) ClampToCanvas();
            }
        }

        public void OnEndDrag(PointerEventData eventData) => SaveCurrentState();

        public void ClosePanel()
        {
            if (rememberLayout || rememberVisibility)
                SaveCurrentState(visible: false);

            gameObject.SetActive(false);
        }

        public void ShowPanel()
        {
            gameObject.SetActive(true);
            if (rememberLayout || rememberVisibility)
                SaveCurrentState(visible: true);
        }

        public void SavePosition() => SaveCurrentState();

        public void ResetSavedLayout()
        {
            HUDPanelLayoutStore.Delete(panelId);
            _layoutRestored = false;
        }

        private void SaveCurrentState(bool? visible = null)
        {
            if (!rememberLayout && !rememberVisibility) return;

            EnsureRuntimeRefs();
            bool isVisible = visible ?? gameObject.activeSelf;

            if (rememberLayout)
            {
                HUDPanelLayoutStore.Save(
                    panelId,
                    _rect.anchoredPosition,
                    _rect.rect.size,
                    isVisible);
            }
            else if (rememberVisibility)
            {
                HUDPanelLayoutStore.Save(panelId, _rect.anchoredPosition, isVisible);
            }
        }

        private void ApplySavedState(DraggableHudPanelState state, bool applyVisibility)
        {
            if (state.HasSavedSize)
                ApplyFixedLayout(_rect, new Vector2(state.x, state.y), new Vector2(state.width, state.height));
            else
                _rect.anchoredPosition = new Vector2(state.x, state.y);

            if (clampToCanvas) ClampToCanvas();

            if (applyVisibility)
                gameObject.SetActive(state.visible);
        }

        private void EnsureResizeHandle()
        {
            if (!allowResize || _resizeHandle != null) return;

            EnsureRuntimeRefs();

            _resizeHandle = new GameObject("ResizeHandle", typeof(RectTransform), typeof(Image), typeof(HudPanelResizeHandle));
            var handleRect = _resizeHandle.GetComponent<RectTransform>();
            handleRect.SetParent(_rect, false);
            handleRect.SetAsLastSibling();
            handleRect.anchorMin = new Vector2(1f, 0f);
            handleRect.anchorMax = new Vector2(1f, 0f);
            handleRect.pivot = new Vector2(1f, 0f);
            handleRect.sizeDelta = new Vector2(ResizeHandleSize, ResizeHandleSize);
            handleRect.anchoredPosition = new Vector2(-4f, 4f);

            var image = _resizeHandle.GetComponent<Image>();
            image.color = new Color(0.55f, 0.85f, 1f, 0.45f);
            image.raycastTarget = true;

            ApplyResizeGripLines(handleRect, new Color(0.85f, 0.95f, 1f, 0.9f));

            _resizeHandle.GetComponent<HudPanelResizeHandle>().Initialize(this);
        }

        private void NormalizeToCenterAnchor()
        {
            if (IsCenterAnchored(_rect)) return;

            ConvertToCenterAnchor(_rect, out var size, out var anchoredPos);
            _rect.sizeDelta = size;
            _rect.anchoredPosition = anchoredPos;
        }

        private void SetFixedSizeKeepingTopLeft(Vector2 targetSize)
        {
            NormalizeToCenterAnchor();

            targetSize.x = Mathf.Max(targetSize.x, minSize.x);
            targetSize.y = Mathf.Max(targetSize.y, minSize.y);

            if (maxSize.x > 0f) targetSize.x = Mathf.Min(targetSize.x, maxSize.x);
            if (maxSize.y > 0f) targetSize.y = Mathf.Min(targetSize.y, maxSize.y);

            var parent = ParentRect;
            if (parent != null)
            {
                var parentSize = parent.rect.size;
                if (maxSize == Vector2.zero)
                {
                    targetSize.x = Mathf.Min(targetSize.x, parentSize.x);
                    targetSize.y = Mathf.Min(targetSize.y, parentSize.y);
                }
            }

            var oldSize = _rect.rect.size;
            var delta = targetSize - oldSize;
            _rect.sizeDelta = targetSize;
            _rect.anchoredPosition += new Vector2(delta.x * 0.5f, -delta.y * 0.5f);
        }

        private static void ApplyFixedLayout(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (!IsCenterAnchored(rect))
                ConvertToCenterAnchor(rect, out _, out _);

            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static bool IsCenterAnchored(RectTransform rect)
        {
            const float eps = 0.001f;
            var center = new Vector2(0.5f, 0.5f);
            return Vector2.Distance(rect.anchorMin, center) < eps
                   && Vector2.Distance(rect.anchorMax, center) < eps;
        }

        private static void ConvertToCenterAnchor(RectTransform rect, out Vector2 size, out Vector2 anchoredPos)
        {
            var parent = rect.parent as RectTransform;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, rect);
            size = bounds.size;
            anchoredPos = bounds.center;

            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;
        }

        private void ClampToCanvas()
        {
            var parent = ParentRect;
            if (parent == null) return;

            var panelSize = _rect.rect.size;
            var parentSize = parent.rect.size;

            var pos = _rect.anchoredPosition;

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
