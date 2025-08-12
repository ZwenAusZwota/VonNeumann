// Unity HUD Menu – Probe UI (uGUI) with Unity Input System compatibility
// Update: Submenu always vertical and positioned above its related main button.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static ProbeHUD.HUDMenuUI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace ProbeHUD
{

    [System.Serializable]
    public class SubSpec
    {
        public string id;
        public string label;
        public Sprite icon;
        public string tooltip;
        public SubClickEvent OnSubClicked;
    }

    [System.Serializable]
    public class MenuSpec
    {
        public string id;
        public string label;
        public Sprite icon;
        public Color tint = Color.white;
        public MenuSelectedEvent OnMainSelected;
        public List<SubSpec> subs = new List<SubSpec>();
    }

    public class HUDMenuUI : MonoBehaviour
    {
        [Header("References")]
        public RectTransform root;
        public RectTransform mainBar;
        public RectTransform subBar;

        public Button mainButtonPrefab;
        public Button subButtonPrefab;

        [Header("Specs")]
        public List<MenuSpec> menu = new List<MenuSpec>();

        [Header("Style")]
        public Color idleColor = new Color(1, 1, 1, 0.2f);
        public Color activeColor = Color.white;
        public float subShowDuration = 0.15f;


#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        [Header("Input Shortcuts")]
        public Key[] mainHotkeys = new[] { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
#else
        [Header("Input Shortcuts")]
        public KeyCode[] mainHotkeys = new[] { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 };
#endif

        [Header("Events")]
        public MenuSelectedEvent OnMainSelected;
        public SubSelectedEvent OnSubSelected;

        [System.Serializable] public class MenuSelectedEvent : UnityEvent<string> { }
        [System.Serializable] public class SubSelectedEvent : UnityEvent<string, string> { }
        [Serializable] public class SubClickEvent : UnityEvent { }

        private readonly Dictionary<string, Button> _mainButtons = new();
        private readonly Dictionary<string, List<Button>> _subButtons = new();
        private string _current;

        private CanvasGroup _subGroup;
        private Coroutine _anim;

        void Awake()
        {
            if (!root) root = (RectTransform)transform;
            _subGroup = subBar ? subBar.gameObject.GetComponent<CanvasGroup>() : null;
            if (!_subGroup && subBar) _subGroup = subBar.gameObject.AddComponent<CanvasGroup>();
            ApplyVerticalLayout();
        }

        void Start()
        {
            BuildMainBar();
            HighlightNone();
            ShowSubBar(false);
        }

        void Update()
        {
            HandleHotkeys();
            HandleCancelClick();
        }

        private void BuildMainBar()
        {
            ClearChildren(mainBar);
            _mainButtons.Clear();
            foreach (var spec in menu)
            {
                var btn = Instantiate(mainButtonPrefab, mainBar);
                btn.name = $"Main_{spec.id}";
                SetupButtonVisual(btn, spec.icon, spec.label, spec.tint);
                var captured = spec.id;
                btn.onClick.AddListener(() => OnMainButtonClicked(captured));
                _mainButtons[spec.id] = btn;
            }
        }

        private void OnMainButtonClicked(string id)
        {
            if (_current == id)
            {
                DeselectCurrent();
            }
            else
            {
                SelectMain(id);
            }
        }

        private void BuildSubBar(string id)
        {
            ClearChildren(subBar);
            _subButtons[id] = new List<Button>();

            var spec = menu.Find(m => m.id == id);
            if (spec == null || spec.subs == null || spec.subs.Count == 0)
            {
                ShowSubBar(false);
                return;
            }

            foreach (var sub in spec.subs)
            {
                var btn = Instantiate(subButtonPrefab, subBar);
                btn.name = $"Sub_{id}_{sub.id}";
                SetupButtonVisual(btn, sub.icon, sub.label, spec.tint, small: true);
                string subId = sub.id;
                btn.onClick.AddListener(() => OnSubClicked(sub)); //OnSubSelected?.Invoke(id, subId));
                _subButtons[id].Add(btn);
            }

            PositionSubBarAbove(id);
            ShowSubBar(true);
        }

        private void OnSubClicked(SubSpec sub)
        {
            if(sub == null || string.IsNullOrWhiteSpace(sub.id)) return;
            sub.OnSubClicked?.Invoke();
            DeselectCurrent();
        }

        private void PositionSubBarAbove(string id)
        {
            if (!_mainButtons.ContainsKey(id)) return;

            var mainBtnRT = _mainButtons[id].GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            mainBtnRT.GetWorldCorners(corners);
            float buttonCenterX = (corners[0].x + corners[3].x) / 2f;

            Vector3 worldPos = new Vector3(buttonCenterX, corners[1].y + 6f, 0f);
            Vector3 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, worldPos, null, out Vector2 lp);
            localPos = lp;

            subBar.pivot = new Vector2(0.5f, 0f);
            subBar.position = root.TransformPoint(localPos);
        }


        private void SelectMain(string id)
        {
            _current = id;
            HighlightMain(id);
            OnMainSelected?.Invoke(id);
            BuildSubBar(id);
        }

        public void DeselectCurrent()
        {
            if (!string.IsNullOrWhiteSpace(_current)) _current = "";
            HighlightNone();
            ShowSubBar(false);
        }

        private void HighlightMain(string id)
        {
            foreach (var kv in _mainButtons)
            {
                var colors = kv.Value.colors;
                var spec = menu.Find(m => m.id == kv.Key);
                var accent = spec != null ? spec.tint : activeColor;

                if (kv.Key.Equals(id))
                {
                    colors.normalColor = activeColor;
                    colors.selectedColor = activeColor;
                    colors.highlightedColor = Color.Lerp(activeColor, accent, 0.25f);
                }
                else
                {
                    colors.normalColor = idleColor;
                    colors.selectedColor = idleColor;
                    colors.highlightedColor = Color.Lerp(idleColor, activeColor, 0.25f);
                }
                kv.Value.colors = colors;
            }
        }

        private void HighlightNone()
        {
            foreach (var kv in _mainButtons)
            {
                var colors = kv.Value.colors;
                colors.normalColor = idleColor;
                colors.selectedColor = idleColor;
                colors.highlightedColor = Color.Lerp(idleColor, activeColor, 0.25f);
                kv.Value.colors = colors;
            }
            _current = null;
        }

        private void ShowSubBar(bool show)
        {
            if (!_subGroup) return;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(AnimateSub(show));
        }

        private IEnumerator AnimateSub(bool show)
        {
            _subGroup.interactable = false;
            _subGroup.blocksRaycasts = false;
            float t = 0f;
            float startA = _subGroup.alpha;
            float endA = show ? 1f : 0f;
            while (t < subShowDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / subShowDuration);
                _subGroup.alpha = Mathf.Lerp(startA, endA, k);
                yield return null;
            }
            _subGroup.alpha = endA;
            _subGroup.interactable = show;
            _subGroup.blocksRaycasts = show;
            _anim = null;
        }

        private void SetupButtonVisual(Button btn, Sprite icon, string label, Color tint, bool small = false)
        {
            var img = btn.GetComponent<Image>();
            if (img) img.color = new Color(0f, 0f, 0f, small ? 0.35f : 0.5f);
            var iconTf = btn.transform.Find("Icon");
            var labelTf = btn.transform.Find("Label");
            if (iconTf)
            {
                var iconImg = iconTf.GetComponent<Image>();
                if (iconImg)
                {
                    iconImg.sprite = icon;
                    iconImg.color = tint;
                    iconImg.enabled = icon;
                }
            }
            if (labelTf)
            {
                var tmp = labelTf.GetComponent<TMPro.TextMeshProUGUI>();
                if (tmp)
                {
                    tmp.text = label;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 10;
                    tmp.fontSizeMax = small ? 18 : 24;
                }
                else
                {
                    var txt = labelTf.GetComponent<Text>();
                    if (txt)
                    {
                        txt.text = label;
                        txt.resizeTextForBestFit = true;
                        txt.resizeTextMinSize = 10;
                        txt.resizeTextMaxSize = small ? 18 : 24;
                    }
                }
            }
        }

        private void ClearChildren(RectTransform rt)
        {
            for (int i = rt.childCount - 1; i >= 0; i--)
            {
                Destroy(rt.GetChild(i).gameObject);
            }
        }

        private void HandleHotkeys()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (menu == null || menu.Count == 0) return;
            if (Keyboard.current == null) return;
            var keys = new[]
            {
                Keyboard.current.digit1Key, Keyboard.current.digit2Key, Keyboard.current.digit3Key,
                Keyboard.current.digit4Key, Keyboard.current.digit5Key, Keyboard.current.digit6Key,
                Keyboard.current.digit7Key, Keyboard.current.digit8Key, Keyboard.current.digit9Key
            };
            var numpad = new[]
            {
                Keyboard.current.numpad1Key, Keyboard.current.numpad2Key, Keyboard.current.numpad3Key,
                Keyboard.current.numpad4Key, Keyboard.current.numpad5Key, Keyboard.current.numpad6Key,
                Keyboard.current.numpad7Key, Keyboard.current.numpad8Key, Keyboard.current.numpad9Key
            };
            for (int i = 0; i < menu.Count && i < keys.Length && i < mainHotkeys.Length; i++)
            {
                bool pressed = (keys[i] != null && keys[i].wasPressedThisFrame) || (numpad[i] != null && numpad[i].wasPressedThisFrame);
                if (pressed) { OnMainButtonClicked(menu[i].id); break; }
            }
#else
            for (int i = 0; i < mainHotkeys.Length && i < menu.Count; i++)
            {
                if (Input.GetKeyDown(mainHotkeys[i])) { OnMainButtonClicked(menu[i].id); break; }
            }
#endif
        }

        private void HandleCancelClick()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) { DeselectCurrent(); }
#else
            if (Input.GetMouseButtonDown(1)) { DeselectCurrent(); }
#endif
        }

        private void ApplyVerticalLayout()
        {
            if (!subBar) return;
            var hl = subBar.GetComponent<HorizontalLayoutGroup>();
            if (hl) Destroy(hl);
            var vl = subBar.GetComponent<VerticalLayoutGroup>();
            if (!vl) vl = subBar.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childAlignment = TextAnchor.MiddleCenter;
            vl.spacing = 8;
            vl.padding = new RectOffset(8, 8, 6, 6);
        }
    }
}
