using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Baut die Forschungsbaum-UI zur Laufzeit auf.
/// </summary>
[DisallowMultipleComponent]
public class ScienceTreeUIController : MonoBehaviour
{
    [SerializeField] private ScienceTreeSceneController sceneController;

    private UITheme _theme;
    private TextMeshProUGUI _detailTitle;
    private TextMeshProUGUI _detailBody;
    private readonly Dictionary<string, ScienceTechNodeView> _nodeViews = new();
    private ScienceTechDefinition _selected;

    private void Awake()
    {
        if (sceneController == null)
            sceneController = GetComponent<ScienceTreeSceneController>();
        _theme = UITheme.Instance;
        BuildUi();
        ScienceTreeService.I.Changed += RefreshAll;
    }

    private void OnDestroy()
    {
        ScienceTreeService.I.Changed -= RefreshAll;
    }

    private void Update()
    {
        if (ScienceTreeService.I.Tick())
            RefreshAll();
        else if (!string.IsNullOrEmpty(ScienceTreeService.I.ActiveResearchId))
            RefreshProgressLabels();
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("ScienceCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var root = canvasGo.GetComponent<RectTransform>();
        Stretch(root);

        var bg = CreateImage(root, "Background", _theme != null ? _theme.panelBackground : new Color(0.04f, 0.07f, 0.13f, 0.96f));
        Stretch(bg.rectTransform);

        var header = CreatePanel(root, "Header", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -72f), Vector2.zero,
            _theme != null ? _theme.panelHeaderBackground : new Color(0.06f, 0.11f, 0.19f, 0.98f));

        CreateLabel(header, "Forschung — Von-Neumann-Katalog", 26, FontStyles.Bold, TextAlignmentOptions.MidlineLeft,
            new Vector2(24f, 0f), new Vector2(-72f, 0f), _theme?.textPrimary ?? Color.white);

        var closeBtn = CreateCloseButton(header);
        closeBtn.onClick.AddListener(() => sceneController?.CloseScienceTree());

        var body = CreatePanel(root, "Body", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 16f), new Vector2(-16f, -88f),
            _theme != null ? _theme.scrollPanelBackground : new Color(0.02f, 0.03f, 0.055f, 0.98f));

        var treeArea = CreatePanel(body, "TreeArea", Vector2.zero, new Vector2(0.62f, 1f), Vector2.zero, Vector2.zero, Color.clear);
        BuildTreeScroll(treeArea);

        var detailArea = CreatePanel(body, "DetailArea", new Vector2(0.62f, 0f), Vector2.one, new Vector2(12f, 0f), Vector2.zero,
            _theme != null ? _theme.backgroundNormal : new Color(0.03f, 0.05f, 0.09f, 0.97f));
        BuildDetailPanel(detailArea);

        RefreshAll();
    }

    private void BuildDetailPanel(RectTransform detailArea)
    {
        _detailTitle = CreateTopAnchoredLabel(detailArea, "Technologie wählen", 22, FontStyles.Bold,
            _theme?.textPrimary ?? Color.white, 44f);

        var bodyGo = new GameObject("DetailBody", typeof(RectTransform));
        bodyGo.transform.SetParent(detailArea, false);
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(16f, 16f);
        bodyRt.offsetMax = new Vector2(-16f, -52f);

        _detailBody = bodyGo.AddComponent<TextMeshProUGUI>();
        _detailBody.text = "Wähle links eine Technologie aus.";
        _detailBody.fontSize = 14;
        _detailBody.alignment = TextAlignmentOptions.TopLeft;
        _detailBody.color = _theme?.textSecondary ?? Color.gray;
        _detailBody.textWrappingMode = TextWrappingModes.Normal;
        _detailBody.overflowMode = TextOverflowModes.Overflow;
        _detailBody.raycastTarget = false;
    }

    private void BuildTreeScroll(RectTransform parent)
    {
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scrollGo.transform.SetParent(parent, false);
        var scrollRect = scrollGo.GetComponent<RectTransform>();
        Stretch(scrollRect);
        var scrollImg = scrollGo.GetComponent<Image>();
        scrollImg.color = _theme != null ? _theme.scrollViewportBackground : new Color(0.015f, 0.025f, 0.045f, 0.99f);

        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        var viewport = viewportGo.GetComponent<RectTransform>();
        Stretch(viewport);
        viewportGo.GetComponent<Image>().color = Color.white;
        viewportGo.GetComponent<Mask>().showMaskGraphic = false;

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewport, false);
        var content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 0.5f);
        content.anchoredPosition = Vector2.zero;

        var hlg = contentGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16f;
        hlg.padding = new RectOffset(16, 16, 16, 16);
        hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        var fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = true;
        scroll.vertical = false;

        int maxTier = 0;
        foreach (var tech in ScienceTreeCatalog.All)
            maxTier = Mathf.Max(maxTier, tech.Tier);

        for (int tier = 0; tier <= maxTier; tier++)
            BuildTierColumn(content, tier);
    }

    private void BuildTierColumn(Transform parent, int tier)
    {
        var colGo = new GameObject($"Tier_{tier}", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
        colGo.transform.SetParent(parent, false);

        var le = colGo.GetComponent<LayoutElement>();
        le.minWidth = 260f;
        le.preferredWidth = 260f;

        var vlg = colGo.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fitter = colGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        string tierTitle = tier == 0 ? "Stufe 0 — Kern" : $"Stufe {tier}";
        var tierLabel = CreateLabel(colGo.GetComponent<RectTransform>(), tierTitle, 16, FontStyles.Bold, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.zero, _theme?.textAccent ?? Color.cyan);
        tierLabel.alignment = TextAlignmentOptions.Center;

        foreach (var tech in ScienceTreeService.I.GetByTier(tier))
        {
            var node = CreateTechNode(colGo.transform, tech);
            _nodeViews[tech.Id] = node;
        }
    }

    private ScienceTechNodeView CreateTechNode(Transform parent, ScienceTechDefinition tech)
    {
        var go = new GameObject(tech.Id, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var le = go.GetComponent<LayoutElement>();
        le.minHeight = 108f;
        le.preferredHeight = 108f;

        var img = go.GetComponent<Image>();
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        var title = CreateLabel(go.GetComponent<RectTransform>(), tech.Title, 15, FontStyles.Bold, TextAlignmentOptions.TopLeft,
            new Vector2(10f, -8f), new Vector2(-10f, -40f), _theme?.textPrimary ?? Color.white);
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Ellipsis;

        CreateLabel(go.GetComponent<RectTransform>(), tech.Branch, 11, FontStyles.Italic, TextAlignmentOptions.TopLeft,
            new Vector2(10f, -40f), new Vector2(-10f, -58f), _theme?.textSecondary ?? Color.gray);

        var duration = CreateLabel(go.GetComponent<RectTransform>(), ScienceTreeService.FormatDuration(tech.DurationSeconds), 12,
            FontStyles.Normal, TextAlignmentOptions.BottomRight,
            new Vector2(10f, 8f), new Vector2(-10f, 28f), _theme?.textAccent ?? Color.cyan);

        var view = new ScienceTechNodeView
        {
            Definition = tech,
            Background = img,
            Button = btn,
            MetaLabel = duration
        };

        btn.onClick.AddListener(() => SelectTech(tech));
        return view;
    }

    private void SelectTech(ScienceTechDefinition tech)
    {
        if (_selected != null && _selected.Id == tech.Id && ScienceTreeService.I.CanStartResearch(tech))
        {
            ScienceTreeService.I.TryStartResearch(tech);
            RefreshAll();
            return;
        }

        _selected = tech;
        RefreshDetail();
        RefreshNodeVisuals();
    }

    private void RefreshAll()
    {
        RefreshNodeVisuals();
        RefreshDetail();
    }

    private void RefreshProgressLabels()
    {
        foreach (var kvp in _nodeViews)
            UpdateMetaLabel(kvp.Value);

        if (_selected != null && ScienceTreeService.I.GetState(_selected) == ScienceTechState.InProgress)
            RefreshDetail();
    }

    private void RefreshNodeVisuals()
    {
        foreach (var kvp in _nodeViews)
        {
            var tech = kvp.Value.Definition;
            var state = ScienceTreeService.I.GetState(tech);
            kvp.Value.Background.color = state switch
            {
                ScienceTechState.Researched => _theme != null ? _theme.successColor * 0.35f : new Color(0.1f, 0.35f, 0.2f, 0.9f),
                ScienceTechState.InProgress => _theme != null ? _theme.progressWarning * 0.35f : new Color(0.35f, 0.28f, 0.1f, 0.9f),
                ScienceTechState.Available => _theme != null ? _theme.backgroundHover : new Color(0.08f, 0.16f, 0.27f, 0.95f),
                _ => _theme != null ? _theme.backgroundDisabled : new Color(0.05f, 0.07f, 0.11f, 0.75f)
            };

            UpdateMetaLabel(kvp.Value);
        }
    }

    private void UpdateMetaLabel(ScienceTechNodeView view)
    {
        if (view.MetaLabel == null) return;

        var tech = view.Definition;
        var state = ScienceTreeService.I.GetState(tech);
        view.MetaLabel.text = state switch
        {
            ScienceTechState.Researched => "Erforscht",
            ScienceTechState.InProgress => $"Verbleibend: {ScienceTreeService.FormatDuration(ScienceTreeService.I.GetRemainingSeconds())}",
            _ => ScienceTreeService.FormatDuration(tech.DurationSeconds)
        };
    }

    private void RefreshDetail()
    {
        if (_detailTitle == null || _detailBody == null) return;

        if (_selected == null)
        {
            _detailTitle.text = "Technologie wählen";
            _detailBody.text = "Wähle links eine Technologie aus dem Bobiverse-inspirierten Forschungskatalog.";
            return;
        }

        var state = ScienceTreeService.I.GetState(_selected);
        _detailTitle.text = _selected.Title;

        string prereq = _selected.Prerequisites != null && _selected.Prerequisites.Length > 0
            ? string.Join(", ", _selected.Prerequisites)
            : "keine";

        _detailBody.text =
            $"{_selected.Description}\n\n" +
            $"Zweig: {_selected.Branch}\n" +
            $"Stufe: {_selected.Tier}\n" +
            $"Dauer: {ScienceTreeService.FormatDuration(_selected.DurationSeconds)}\n" +
            $"Voraussetzungen: {prereq}\n" +
            $"Status: {StateLabel(state)}";

        if (state == ScienceTechState.InProgress)
            _detailBody.text += $"\n\nVerbleibend: {ScienceTreeService.FormatDuration(ScienceTreeService.I.GetRemainingSeconds())}";
        else if (state == ScienceTechState.Available && ScienceTreeService.I.CanStartResearch(_selected))
            _detailBody.text += "\n\n→ Erneut auf den Knoten klicken, um die Forschung zu starten.";
        else if (state == ScienceTechState.Available && !string.IsNullOrEmpty(ScienceTreeService.I.ActiveResearchId))
            _detailBody.text += "\n\n(Eine andere Forschung läuft bereits.)";
    }

    private static string StateLabel(ScienceTechState state) => state switch
    {
        ScienceTechState.Researched => "Erforscht",
        ScienceTechState.InProgress => "In Arbeit",
        ScienceTechState.Available => "Verfügbar",
        _ => "Gesperrt"
    };

    private Button CreateCloseButton(Transform parent)
    {
        var go = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(44f, 44f);
        rt.anchoredPosition = new Vector2(-12f, 0f);

        var img = go.GetComponent<Image>();
        img.color = _theme != null ? _theme.buttonNormal : new Color(0.08f, 0.16f, 0.27f, 1f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        var label = CreateLabel(go.transform, "X", 24, FontStyles.Bold, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.zero, _theme?.textPrimary ?? Color.white);
        label.raycastTarget = false;
        return btn;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        go.GetComponent<Image>().color = color;
        return rt;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    private static TextMeshProUGUI CreateTopAnchoredLabel(Transform parent, string text, float size, FontStyles style, Color color, float height)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, height);
        rt.anchoredPosition = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.color = color;
        tmp.margin = new Vector4(16f, 12f, 16f, 0f);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string text, float size, FontStyles style, TextAlignmentOptions align,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        if (parent is RectTransform prt && prt.GetComponent<LayoutGroup>() != null)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 28f);
        }
        else
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    private sealed class ScienceTechNodeView
    {
        public ScienceTechDefinition Definition;
        public Image Background;
        public Button Button;
        public TextMeshProUGUI MetaLabel;
    }
}
