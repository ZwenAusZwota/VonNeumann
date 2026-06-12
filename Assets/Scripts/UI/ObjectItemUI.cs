// Assets/Scripts/UI/ObjectItemUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ObjectItemUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI Components")]
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI labelName;
    [SerializeField] private TextMeshProUGUI labelType;
    [SerializeField] private TextMeshProUGUI labelDistance;
    [SerializeField] private Image typeIndicator;

    [Header("Theme")]
    [SerializeField] private UITheme theme;
    [SerializeField] private bool useTheme = true;

    [Header("Colors (Fallback wenn kein Theme)")]
    public Color normalColor = new(0.03f, 0.05f, 0.09f, 0.97f);
    public Color hoverColor = new(0.06f, 0.10f, 0.17f, 0.98f);
    public Color selectedColor = new(0.10f, 0.25f, 0.41f, 1f);

    [Header("Type Colors (Fallback wenn kein Theme)")]
    public Color asteroidColor = new(0.6f, 0.4f, 0.2f, 1f);
    public Color planetColor = new(0.2f, 0.6f, 0.8f, 1f);
    public Color starColor = new(1f, 0.9f, 0.3f, 1f);
    public Color stationColor = new(0.5f, 0.8f, 0.3f, 1f);

    [Header("Typography")]
    [SerializeField] private float nameFontSize = 15f;
    [SerializeField] private float detailFontSize = 11f;
    [SerializeField] private float itemHeight = 46f;

    private const float IndicatorWidth = 5f;
    private const float ExpandButtonWidth = 18f;
    private const float DepthIndent = 16f;
    private const float PaddingH = 8f;
    private const float PaddingV = 5f;
    private const float ChildItemHeight = 38f;

    private Color GetNormalColor() => (useTheme && theme != null) ? theme.backgroundNormal : normalColor;
    private Color GetHoverColor() => (useTheme && theme != null) ? theme.backgroundHover : hoverColor;
    private Color GetSelectedColor() => (useTheme && theme != null) ? theme.backgroundSelected : selectedColor;
    private Color GetAsteroidColor() => (useTheme && theme != null) ? theme.asteroidColor : asteroidColor;
    private Color GetPlanetColor() => (useTheme && theme != null) ? theme.planetColor : planetColor;
    private Color GetStarColor() => (useTheme && theme != null) ? theme.starColor : starColor;
    private Color GetStationColor() => (useTheme && theme != null) ? theme.stationColor : stationColor;

    public SystemObject SObject { get; private set; }
    public bool Selected { get; private set; }
    public bool IsExpanded { get; private set; }
    public event System.Action<ObjectItemUI> ExpandToggled;

    private Button _expandButton;
    private Text _expandGlyph;
    private static Font _expandFont;
    private int _treeDepth;
    private bool _hasChildren;
    private bool _isChildRow;
    private float _activeItemHeight;

    private static readonly Dictionary<Transform, ObjectItemUI> _selectedByList = new();

    private void Awake()
    {
        if (useTheme && theme == null)
            theme = UITheme.Instance;
        AutoWireComponents();
        ConfigureLayout();
    }

    private void AutoWireComponents()
    {
        if (!background) background = FindComponent<Image>("Background");
        if (!typeIndicator) typeIndicator = FindComponent<Image>("typeIndicator");
        if (!labelName) labelName = FindComponent<TextMeshProUGUI>("labelName");
        if (!labelType) labelType = FindComponent<TextMeshProUGUI>("labelType");
        if (!labelDistance) labelDistance = FindComponent<TextMeshProUGUI>("labelDistance");
        if (!icon) icon = FindComponent<Image>("Icon");
    }

    private T FindComponent<T>(string childName) where T : Component
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName)
            {
                var c = t.GetComponent<T>();
                if (c != null) return c;
            }
        }
        return null;
    }

    private void ConfigureLayout()
    {
        RemoveLegacyLayoutObjects();

        _activeItemHeight = _isChildRow ? ChildItemHeight : itemHeight;
        var root = (RectTransform)transform;
        StretchToParentWidth(root, _activeItemHeight);

        var itemLe = GetComponent<LayoutElement>() ?? gameObject.AddComponent<LayoutElement>();
        itemLe.minHeight = _activeItemHeight;
        itemLe.preferredHeight = _activeItemHeight;
        itemLe.flexibleWidth = 1f;
        itemLe.minWidth = 0f;

        float leftInset = _treeDepth * DepthIndent;
        float expandWidth = ExpandButtonWidth;
        float textLeft = leftInset + expandWidth + IndicatorWidth + PaddingH;
        EnsureExpandButton(leftInset, expandWidth, _hasChildren);

        if (background != null)
        {
            background.transform.SetParent(transform, false);
            background.transform.SetAsFirstSibling();
            StretchRect(background.rectTransform, 0f, 0f, 0f, 0f);
        }

        if (typeIndicator != null)
        {
            typeIndicator.transform.SetParent(transform, false);
            typeIndicator.transform.SetSiblingIndex(_expandButton != null ? 2 : 1);
            var rt = typeIndicator.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(leftInset + expandWidth, 0f);
            rt.sizeDelta = new Vector2(IndicatorWidth, 0f);
        }

        ReparentToRoot(labelName);
        ReparentToRoot(labelType);
        ReparentToRoot(labelDistance);

        if (_isChildRow)
            ConfigureChildRowLabels(textLeft);
        else
            ConfigureRootRowLabels(textLeft);
    }

    private void RemoveLegacyLayoutObjects()
    {
        var hlg = GetComponent<HorizontalLayoutGroup>();
        if (hlg != null)
            Destroy(hlg);

        var legacyPanel = transform.Find("ContentPanel");
        if (legacyPanel != null)
            Destroy(legacyPanel.gameObject);
    }

    private void ReparentToRoot(TextMeshProUGUI label)
    {
        if (label != null)
            label.transform.SetParent(transform, false);
    }

    private static void StretchToParentWidth(RectTransform rt, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, height);
    }

    private static void StretchRect(RectTransform rt, float left, float bottom, float right, float top)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private static void PlaceRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    private void ConfigureRootRowLabels(float textLeft)
    {
        if (labelName != null)
        {
            PlaceRect(labelName.rectTransform,
                new Vector2(0f, 0.45f), Vector2.one,
                new Vector2(textLeft, 0f), new Vector2(-PaddingH, -PaddingV));
            SetupNameLabel();
        }

        if (labelType != null)
        {
            PlaceRect(labelType.rectTransform,
                Vector2.zero, new Vector2(0.5f, 0.45f),
                new Vector2(textLeft, PaddingV), new Vector2(-4f, 0f));
            SetupDetailLabel(labelType, TextAlignmentOptions.BottomLeft, theme?.textSecondary ?? Color.gray);
        }

        if (labelDistance != null)
        {
            PlaceRect(labelDistance.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(1f, 0.45f),
                new Vector2(4f, PaddingV), new Vector2(-PaddingH, 0f));
            var accent = (useTheme && theme != null) ? theme.textAccent : new Color(0.5f, 0.9f, 1f);
            SetupDetailLabel(labelDistance, TextAlignmentOptions.BottomRight, accent);
        }
    }

    private void ConfigureChildRowLabels(float textLeft)
    {
        if (labelName != null)
        {
            PlaceRect(labelName.rectTransform,
                new Vector2(0f, 0.5f), Vector2.one,
                new Vector2(textLeft, 0f), new Vector2(-PaddingH, 0f));
            SetupNameLabel();
            labelName.fontSize = 13f;
        }

        if (labelType != null)
        {
            PlaceRect(labelType.rectTransform,
                Vector2.zero, new Vector2(1f, 0.5f),
                new Vector2(textLeft, 0f), new Vector2(-PaddingH, 0f));
            SetupDetailLabel(labelType, TextAlignmentOptions.BottomLeft, theme?.textSecondary ?? Color.gray);
            labelType.fontSize = 10f;
        }

        if (labelDistance != null)
        {
            PlaceRect(labelDistance.rectTransform,
                new Vector2(0.55f, 0f), new Vector2(1f, 0.5f),
                new Vector2(4f, 0f), new Vector2(-PaddingH, 0f));
            var accent = (useTheme && theme != null) ? theme.textAccent : new Color(0.5f, 0.9f, 1f);
            SetupDetailLabel(labelDistance, TextAlignmentOptions.BottomRight, accent);
            labelDistance.fontSize = 10f;
        }
    }

    private void SetupNameLabel()
    {
        labelName.raycastTarget = false;
        labelName.enableAutoSizing = false;
        labelName.fontSize = nameFontSize;
        labelName.fontStyle = FontStyles.Bold;
        labelName.alignment = TextAlignmentOptions.TopLeft;
        labelName.overflowMode = TextOverflowModes.Ellipsis;
        labelName.color = (useTheme && theme != null) ? theme.textPrimary : Color.white;
    }

    private void SetupDetailLabel(TextMeshProUGUI tmp, TextAlignmentOptions alignment, Color color)
    {
        tmp.raycastTarget = false;
        tmp.enableAutoSizing = false;
        tmp.fontSize = detailFontSize;
        tmp.fontStyle = FontStyles.Normal;
        tmp.alignment = alignment;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.color = color;
    }

    public void Init(
        SystemObject so,
        int treeDepth = 0,
        bool hasChildren = false,
        bool isExpanded = false,
        bool isChildRow = false,
        string distanceText = null,
        string displayNameOverride = null)
    {
        SObject = so;
        _treeDepth = treeDepth;
        _hasChildren = hasChildren;
        IsExpanded = isExpanded;
        _isChildRow = isChildRow;

        string displayName = string.IsNullOrWhiteSpace(displayNameOverride)
            ? ResolveDisplayName(so)
            : displayNameOverride.Trim();
        string typeName = ResolveTypeName(so);
        Color indicatorColor = ResolveIndicatorColor(so);

        if (labelName) labelName.text = displayName;
        if (labelType) labelType.text = typeName;
        if (labelDistance) labelDistance.text = distanceText ?? string.Empty;
        if (typeIndicator) typeIndicator.color = indicatorColor;
        if (background) background.color = GetNormalColor();

        Selected = false;
        ApplyLabelColors();
        RefreshTextLayout();
        UpdateExpandVisual();
    }

    public void UpdateDistanceLabel(string distanceText)
    {
        if (labelDistance != null)
            labelDistance.text = distanceText ?? string.Empty;
    }

    private void EnsureExpandButton(float leftInset, float expandWidth, bool showInteractive)
    {
        if (!showInteractive)
        {
            if (_expandButton != null)
                _expandButton.gameObject.SetActive(false);
            return;
        }

        if (_expandButton != null && _expandGlyph == null)
        {
            Destroy(_expandButton.gameObject);
            _expandButton = null;
        }

        if (_expandButton == null)
        {
            var go = new GameObject("ExpandButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            go.transform.SetSiblingIndex(1);

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.01f);

            var glyphGo = new GameObject("ExpandGlyph", typeof(RectTransform));
            glyphGo.transform.SetParent(go.transform, false);
            var glyphRect = glyphGo.GetComponent<RectTransform>();
            glyphRect.anchorMin = Vector2.zero;
            glyphRect.anchorMax = Vector2.one;
            glyphRect.offsetMin = Vector2.zero;
            glyphRect.offsetMax = Vector2.zero;
            _expandGlyph = glyphGo.AddComponent<Text>();
            _expandGlyph.font = GetExpandFont();
            _expandGlyph.alignment = TextAnchor.MiddleCenter;
            _expandGlyph.fontSize = 14;
            _expandGlyph.color = (useTheme && theme != null) ? theme.textSecondary : Color.gray;
            _expandGlyph.raycastTarget = false;
            _expandGlyph.supportRichText = false;

            _expandButton = go.GetComponent<Button>();
            _expandButton.targetGraphic = bg;
            _expandButton.onClick.AddListener(HandleExpandClicked);
        }

        _expandButton.gameObject.SetActive(true);
        var rect = _expandButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(leftInset, 0f);
        rect.sizeDelta = new Vector2(expandWidth, 0f);
        UpdateExpandVisual();
    }

    private void HandleExpandClicked()
    {
        IsExpanded = !IsExpanded;
        UpdateExpandVisual();
        ExpandToggled?.Invoke(this);
    }

    private void UpdateExpandVisual()
    {
        if (_expandGlyph != null)
            _expandGlyph.text = IsExpanded ? "-" : "+";
    }

    private static Font GetExpandFont()
    {
        if (_expandFont == null)
            _expandFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _expandFont;
    }

    private static string ResolveDisplayName(SystemObject so)
    {
        if (so == null) return "Object";

        if (so.GameObject != null)
        {
            if (so.GameObject.GetComponent<AsteroidBelt>() != null)
                return string.IsNullOrWhiteSpace(so.GameObject.name) ? "Asteroidengürtel" : so.GameObject.name.Trim();
            if (ScanAsteroidHelper.IsAsteroidBody(so.GameObject) && !string.IsNullOrWhiteSpace(so.GameObject.name))
                return so.GameObject.name.Trim();
            if (ScanOrbiterHelper.IsOrbiterBody(so.GameObject) && !string.IsNullOrWhiteSpace(so.GameObject.name))
                return so.GameObject.name.Trim();
            if (!string.IsNullOrWhiteSpace(so.GameObject.name))
                return so.GameObject.name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(so.DisplayName))
            return StripScannerDisplaySuffix(so.DisplayName);

        return string.IsNullOrWhiteSpace(so.Name) ? "Object" : so.Name;
    }

    private string ResolveTypeName(SystemObject so)
    {
        if (so == null) return string.Empty;
        if (so.RequiresNearScan)
            return "Near Scan erforderlich";

        if (so.GameObject == null)
            return string.IsNullOrWhiteSpace(so.Name) ? string.Empty : so.Name;

        if (IsAsteroid(so.GameObject))
            return GetAsteroidMaterialLabel(so.GameObject);
        if (so.GameObject.CompareTag("Moon"))
            return "Mond";
        if (ScanOrbiterHelper.IsSatelliteBody(so.GameObject))
            return "Satellit";
        if (so.GameObject.GetComponent<Planet>() != null || so.GameObject.CompareTag("Planet"))
            return "Planet";
        if (so.GameObject.CompareTag("Star"))
            return "Star";
        if (so.GameObject.GetComponent<AsteroidBelt>() != null)
            return "Asteroidengürtel";

        return "Station";
    }

    private Color ResolveIndicatorColor(SystemObject so)
    {
        if (so?.GameObject == null)
            return GetStationColor();

        if (IsAsteroid(so.GameObject) || so.GameObject.GetComponent<AsteroidBelt>() != null)
            return GetAsteroidColor();
        if (so.GameObject.GetComponent<Planet>() != null || so.GameObject.CompareTag("Planet"))
            return GetPlanetColor();
        if (so.GameObject.CompareTag("Star"))
            return GetStarColor();
        if (so.GameObject.CompareTag("Moon") || ScanOrbiterHelper.IsSatelliteBody(so.GameObject))
            return GetPlanetColor();

        return GetStationColor();
    }

    private void RefreshTextLayout()
    {
        ConfigureLayout();
        if (labelName) labelName.ForceMeshUpdate();
        if (labelType) labelType.ForceMeshUpdate();
        if (labelDistance) labelDistance.ForceMeshUpdate();
    }

    private static bool IsAsteroid(GameObject go)
    {
        if (go == null) return false;
        if (go.CompareTag("Asteroid")) return true;
        return go.GetComponentInParent<MineableAsteroid>() != null;
    }

    private static string GetAsteroidMaterialLabel(GameObject go)
    {
        var mineable = go.GetComponent<MineableAsteroid>() ?? go.GetComponentInParent<MineableAsteroid>();
        if (mineable == null || string.IsNullOrEmpty(mineable.materialId))
            return "";

        var matDef = MaterialDatabase.Get(mineable.materialId);
        return matDef != null ? matDef.displayName : mineable.materialId;
    }

    private static string StripScannerDisplaySuffix(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        const char emDash = '\u2014';
        int split = text.IndexOf(emDash);
        if (split > 0)
        {
            string namePart = text[..split].Trim();
            if (!string.IsNullOrWhiteSpace(namePart))
                return namePart;
        }

        split = text.IndexOf('?');
        if (split > 0)
        {
            string namePart = text[..split].Trim();
            if (!string.IsNullOrWhiteSpace(namePart))
                return namePart;
        }

        return text.Trim();
    }

    private static string ExtractDistanceLabel(string displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return "";

        const char emDash = '\u2014';
        int split = displayName.IndexOf(emDash);
        if (split >= 0)
            return displayName[(split + 1)..].Trim();

        const string hyphen = " - ";
        split = displayName.IndexOf(hyphen, System.StringComparison.Ordinal);
        return split >= 0 ? displayName[(split + hyphen.Length)..].Trim() : "";
    }

    private void ApplyLabelColors()
    {
        if (!useTheme || theme == null) return;
        if (labelName) { labelName.color = theme.textPrimary; labelName.fontSize = nameFontSize; }
        if (labelType) { labelType.color = theme.textSecondary; labelType.fontSize = detailFontSize; }
        if (labelDistance) { labelDistance.color = theme.textAccent; labelDistance.fontSize = detailFontSize; }
    }

    public void SetSelected(bool sel)
    {
        Selected = sel;
        if (background) background.color = sel ? GetSelectedColor() : GetNormalColor();
    }

    public void OnPointerEnter(PointerEventData _)
    {
        if (!Selected && background)
        {
            background.color = GetHoverColor();
            transform.localScale = Vector3.one * 1.02f;
        }
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (!Selected && background)
        {
            background.color = GetNormalColor();
            transform.localScale = Vector3.one;
        }
    }

    private void OnDestroy()
    {
        var listRoot = transform.parent;
        if (listRoot != null && _selectedByList.TryGetValue(listRoot, out var selected) && selected == this)
            _selectedByList.Remove(listRoot);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_expandButton != null && _hasChildren)
        {
            var expandRect = _expandButton.transform as RectTransform;
            if (expandRect != null && RectTransformUtility.RectangleContainsScreenPoint(
                    expandRect, eventData.position, eventData.pressEventCamera))
                return;
        }

        var listRoot = transform.parent;
        if (listRoot != null && _selectedByList.TryGetValue(listRoot, out var prev) && prev != null && prev != this)
            prev.SetSelected(false);

        if (listRoot != null)
            _selectedByList[listRoot] = this;
        SetSelected(true);

        if (SObject != null && SObject.GameObject)
        {
            if (!ProbeAutopilot.TrySetNavTargetOnSelectedProbe(SObject.GameObject.transform))
                Debug.LogWarning("[ObjectItemUI] Konnte Nav-Ziel nicht setzen (keine Sonde im HUD selektiert?).");
        }
    }
}
