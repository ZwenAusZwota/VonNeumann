using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime-UI für den Sonden-Fabrikator.
/// </summary>
[DisallowMultipleComponent]
public class FabricatorUIController : MonoBehaviour
{
    [SerializeField] private FabricatorSceneController sceneController;

    private UITheme _theme;
    private FabricatorController _fabricator;
    private ProductBlueprint _selectedBlueprint;
    private TextMeshProUGUI _descTitle;
    private TextMeshProUGUI _descBody;
    private TextMeshProUGUI _descMeta;
    private RectTransform _blueprintGrid;
    private RectTransform _queueContent;
    private readonly List<GameObject> _blueprintButtons = new();
    private readonly List<GameObject> _queueRows = new();
    private Slider _runningSlider;
    private TextMeshProUGUI _runningNameLabel;
    private TextMeshProUGUI _runningPercentLabel;
    private float _runningBuildTime;
    private string _lastQueueSignature = "";
    private string _lastTemplateSignature = "";

    private void Awake()
    {
        if (sceneController == null)
            sceneController = GetComponent<FabricatorSceneController>();
        _theme = UITheme.Instance;
        BuildUi();
        RebindFabricator();
    }

    private void OnEnable()
    {
        if (HUDBindingService.I != null)
            HUDBindingService.I.OnSelectionChanged += OnSelectionChanged;
        RebindFabricator();
    }

    private void OnDisable()
    {
        if (HUDBindingService.I != null)
            HUDBindingService.I.OnSelectionChanged -= OnSelectionChanged;
        UnbindFabricator();
    }

    private void OnSelectionChanged(HUDItem _) => RebindFabricator();

    private void BuildUi()
    {
        var canvasGo = new GameObject("FabricatorCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var root = canvasGo.GetComponent<RectTransform>();
        OverlayUiKit.Stretch(root);

        var bg = OverlayUiKit.CreateImage(root, "Background", _theme?.panelBackground ?? new Color(0.04f, 0.07f, 0.13f, 0.96f));
        OverlayUiKit.Stretch(bg.rectTransform);

        var header = OverlayUiKit.CreatePanel(root, "Header", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -72f), Vector2.zero,
            _theme?.panelHeaderBackground ?? new Color(0.06f, 0.11f, 0.19f, 0.98f));
        OverlayUiKit.CreateLabel(header, "Fabrikator", 26, FontStyles.Bold, TextAlignmentOptions.MidlineLeft,
            new Vector2(24f, 0f), new Vector2(-72f, 0f), _theme?.textPrimary ?? Color.white);
        OverlayUiKit.CreateCloseButton(header, _theme, () => sceneController?.CloseFabricator());

        var body = OverlayUiKit.CreatePanel(root, "Body", Vector2.zero, Vector2.one, new Vector2(16f, 16f), new Vector2(-16f, -88f),
            _theme?.scrollPanelBackground ?? new Color(0.02f, 0.03f, 0.055f, 0.98f));

        var left = OverlayUiKit.CreatePanel(body, "Blueprints", Vector2.zero, new Vector2(0.42f, 1f), Vector2.zero, Vector2.zero,
            _theme?.backgroundNormal ?? new Color(0.03f, 0.05f, 0.09f, 0.97f));
        OverlayUiKit.CreateTopLabel(left, "Baupläne", 18, FontStyles.Bold, _theme?.textAccent ?? Color.cyan);

        var bpScroll = OverlayUiKit.CreateVerticalScroll(left, out _blueprintGrid);
        var bpScrollRt = bpScroll;
        bpScrollRt.anchorMin = Vector2.zero;
        bpScrollRt.anchorMax = Vector2.one;
        bpScrollRt.offsetMin = new Vector2(0f, 8f);
        bpScrollRt.offsetMax = new Vector2(0f, -40f);

        var right = OverlayUiKit.CreatePanel(body, "RightColumn", new Vector2(0.42f, 0f), Vector2.one, new Vector2(12f, 0f), Vector2.zero,
            Color.clear);

        // Obere Hälfte: Detailanzeige
        var detailPanel = OverlayUiKit.CreatePanel(right, "Details", new Vector2(0f, 0.5f), Vector2.one,
            new Vector2(0f, 6f), new Vector2(0f, 0f),
            _theme?.backgroundNormal ?? new Color(0.03f, 0.05f, 0.09f, 0.97f));

        _descTitle = OverlayUiKit.CreateTopLabel(detailPanel, "Bauplan wählen", 20, FontStyles.Bold, _theme?.textPrimary ?? Color.white);
        _descMeta = OverlayUiKit.CreateTopLabel(detailPanel, "", 13, FontStyles.Normal, _theme?.textAccent ?? Color.cyan);
        var metaRt = _descMeta.rectTransform;
        metaRt.anchoredPosition = new Vector2(0f, -36f);

        var bodyGo = new GameObject("DescBody", typeof(RectTransform));
        bodyGo.transform.SetParent(detailPanel, false);
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(16f, 56f);
        bodyRt.offsetMax = new Vector2(-16f, -72f);
        _descBody = bodyGo.AddComponent<TextMeshProUGUI>();
        _descBody.fontSize = 14;
        _descBody.alignment = TextAlignmentOptions.TopLeft;
        _descBody.color = _theme?.textSecondary ?? Color.gray;
        _descBody.textWrappingMode = TextWrappingModes.Normal;

        var addBtn = OverlayUiKit.CreateButton(detailPanel, "Zur Warteschlange", 200f, 40f, _theme);
        var addRt = addBtn.GetComponent<RectTransform>();
        addRt.anchorMin = new Vector2(0f, 0f);
        addRt.anchorMax = new Vector2(0f, 0f);
        addRt.pivot = new Vector2(0f, 0f);
        addRt.anchoredPosition = new Vector2(16f, 12f);
        addBtn.onClick.AddListener(OnAddToQueue);

        // Untere Hälfte: Warteschlange
        var queuePanel = OverlayUiKit.CreatePanel(right, "Queue", Vector2.zero, new Vector2(1f, 0.5f),
            new Vector2(0f, 0f), new Vector2(0f, -6f),
            _theme?.scrollViewportBackground ?? new Color(0.015f, 0.025f, 0.045f, 0.99f));
        OverlayUiKit.CreateTopLabel(queuePanel, "Produktions-Warteschlange", 16, FontStyles.Bold, _theme?.textAccent ?? Color.cyan);

        var scroll = OverlayUiKit.CreateVerticalScroll(queuePanel, out _queueContent);
        var scrollRt = scroll;
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(0f, 0f);
        scrollRt.offsetMax = new Vector2(0f, -36f);
    }

    private void RebindFabricator()
    {
        var sel = HUDBindingService.I?.SelectedItem;
        var fab = sel?.Transform != null ? sel.Transform.GetComponent<FabricatorController>() : null;
        if (fab == null)
        {
#if UNITY_2023_1_OR_NEWER
            fab = Object.FindAnyObjectByType<FabricatorController>(FindObjectsInactive.Include);
#else
            fab = Object.FindObjectOfType<FabricatorController>();
#endif
        }

        if (fab == _fabricator)
        {
            _fabricator.ForceRefreshUI();
            return;
        }

        UnbindFabricator();
        _fabricator = fab;
        if (_fabricator == null)
        {
            ShowNoFabricator();
            return;
        }

        _fabricator.TemplatesUpdated += OnTemplatesUpdated;
        _fabricator.QueueUpdated += OnQueueUpdated;
        _fabricator.ForceRefreshUI();

        if (_blueprintButtons.Count == 0)
            OnTemplatesUpdated(FabricatorBlueprintRegistry.GetFor(_fabricator.fabricatorType));
    }

    private void UnbindFabricator()
    {
        if (_fabricator == null) return;
        _fabricator.TemplatesUpdated -= OnTemplatesUpdated;
        _fabricator.QueueUpdated -= OnQueueUpdated;
        _fabricator = null;
        ClearBlueprints();
        ClearQueue();
        _selectedBlueprint = null;
        _lastTemplateSignature = "";
    }

    private void ShowNoFabricator()
    {
        if (_descTitle) _descTitle.text = "Kein Fabrikator";
        if (_descMeta) _descMeta.text = "";
        if (_descBody) _descBody.text = "Keine Sonde mit Fabrikator-Modul gefunden.";
        ClearBlueprints();
        ClearQueue();
    }

    private void OnTemplatesUpdated(IReadOnlyList<ProductBlueprint> templates)
    {
        var signature = BuildTemplateSignature(templates);
        if (signature == _lastTemplateSignature && _blueprintButtons.Count > 0)
            return;

        _lastTemplateSignature = signature;
        DestroyBlueprintButtons();

        if (templates == null || templates.Count == 0)
        {
            if (_descBody) _descBody.text = "Keine Baupläne verfügbar.";
            return;
        }

        AddBlueprintGroup("Einbau in Sonde (Equipment)",
            templates.Where(t => t.category == ProductBlueprint.ProductCategory.Equipment));
        AddBlueprintGroup("Autonome Einheiten (extern)",
            templates.Where(t => t.category == ProductBlueprint.ProductCategory.ExternalUnit));
        AddBlueprintGroup("Komponenten",
            templates.Where(t => t.category == ProductBlueprint.ProductCategory.Component));

        SelectBlueprint(templates[0]);
    }

    private static string BuildTemplateSignature(IReadOnlyList<ProductBlueprint> templates)
    {
        if (templates == null || templates.Count == 0) return string.Empty;
        var parts = new List<string>(templates.Count);
        foreach (var bp in templates)
            parts.Add(bp != null ? bp.productId ?? bp.name : "<null>");
        return string.Join("|", parts);
    }

    private void AddBlueprintGroup(string title, IEnumerable<ProductBlueprint> items)
    {
        var list = items.ToList();
        if (list.Count == 0) return;

        var headerGo = new GameObject(title, typeof(RectTransform), typeof(LayoutElement));
        headerGo.transform.SetParent(_blueprintGrid, false);
        headerGo.GetComponent<LayoutElement>().minHeight = 28f;
        var header = headerGo.AddComponent<TextMeshProUGUI>();
        header.text = title;
        header.fontSize = 13;
        header.fontStyle = FontStyles.Bold;
        header.color = _theme?.textAccent ?? Color.cyan;
        header.margin = new Vector4(4f, 8f, 4f, 0f);
        header.raycastTarget = false;
        _blueprintButtons.Add(headerGo);

        foreach (var bp in list)
        {
            var go = new GameObject(bp.productId ?? bp.displayName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(_blueprintGrid, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 44f;
            le.preferredHeight = 44f;
            le.flexibleWidth = 1f;
            var img = go.GetComponent<Image>();
            img.color = _theme?.buttonNormal ?? new Color(0.08f, 0.16f, 0.27f, 1f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var local = bp;
            btn.onClick.AddListener(() => SelectBlueprint(local));

            var label = OverlayUiKit.CreateLabel(go.transform, bp.displayName, 13, FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft, new Vector2(12f, 0f), new Vector2(-12f, 0f), _theme?.textPrimary ?? Color.white);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            _blueprintButtons.Add(go);
        }
    }

    private void SelectBlueprint(ProductBlueprint bp)
    {
        _selectedBlueprint = bp;
        HighlightSelectedBlueprint(bp);
        if (bp == null)
        {
            if (_descTitle) _descTitle.text = "Bauplan wählen";
            if (_descMeta) _descMeta.text = "";
            if (_descBody) _descBody.text = "";
            return;
        }

        if (_descTitle) _descTitle.text = bp.displayName;
        string category = bp.category switch
        {
            ProductBlueprint.ProductCategory.Equipment => "Einbau in Sonde",
            ProductBlueprint.ProductCategory.ExternalUnit => "Autonome Einheit",
            _ => "Komponente"
        };
        string effect = bp.category switch
        {
            ProductBlueprint.ProductCategory.Equipment => "Wird nach Fertigstellung direkt eingebaut.",
            ProductBlueprint.ProductCategory.ExternalUnit => "Wird nach Fertigstellung als eigenständige Einheit eingesetzt.",
            _ => "Wird ins Cargo gelagert."
        };
        if (_descMeta) _descMeta.text = $"{category} | {(bp.buildTime > 0f ? $"{bp.buildTime:0.#} s" : "sofort")}";
        if (_descBody) _descBody.text =
            (string.IsNullOrWhiteSpace(bp.description) ? "Keine Beschreibung." : bp.description) +
            $"\n\n{effect}";
    }

    private void OnAddToQueue()
    {
        if (_fabricator == null || _selectedBlueprint == null) return;
        _fabricator.Enqueue(_selectedBlueprint);
    }

    private void OnQueueUpdated(ProductBlueprint current, float timeRemaining, IReadOnlyList<ProductBlueprint> queue)
    {
        var signature = BuildQueueSignature(current, queue);
        if (signature == _lastQueueSignature && current != null)
        {
            UpdateRunningProgress(timeRemaining);
            return;
        }

        _lastQueueSignature = signature;
        RebuildQueue(current, timeRemaining, queue);
    }

    private void RebuildQueue(ProductBlueprint current, float timeRemaining, IReadOnlyList<ProductBlueprint> queue)
    {
        ClearQueue();
        _runningSlider = null;
        _runningNameLabel = null;
        _runningPercentLabel = null;

        if (current != null)
            CreateQueueRow(GetBlueprintDisplayName(current), true, current.buildTime, timeRemaining, 0);

        if (queue == null) return;
        for (int i = 0; i < queue.Count; i++)
        {
            if (current != null && i == 0) continue;
            var bp = queue[i];
            if (bp == null) continue;
            int index = i;
            CreateQueueRow(GetBlueprintDisplayName(bp), false, 0f, 0f, index);
        }
    }

    private void CreateQueueRow(string title, bool running, float totalTime, float timeRemaining, int queueIndex)
    {
        var row = new GameObject("QueueRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        row.transform.SetParent(_queueContent, false);
        var le = row.GetComponent<LayoutElement>();
        le.minHeight = running ? 48f : 40f;
        le.preferredHeight = running ? 48f : 40f;
        row.GetComponent<Image>().color = _theme?.backgroundHover ?? new Color(0.08f, 0.16f, 0.27f, 0.95f);

        var removeBtn = OverlayUiKit.CreateButton(row.transform, "X", 36f, 28f, _theme);
        var removeRt = removeBtn.GetComponent<RectTransform>();
        removeRt.anchorMin = new Vector2(1f, 1f);
        removeRt.anchorMax = new Vector2(1f, 1f);
        removeRt.pivot = new Vector2(1f, 1f);
        removeRt.anchoredPosition = new Vector2(-8f, -8f);
        int capturedIndex = queueIndex;
        removeBtn.onClick.AddListener(() => _fabricator?.RemoveAt(capturedIndex));

        if (running)
        {
            _runningSlider = OverlayUiKit.CreateSlider(row.transform, new Vector2(12f, 8f), new Vector2(-52f, -8f),
                _theme?.progressEmpty ?? new Color(0.08f, 0.1f, 0.16f, 1f),
                _theme?.progressFull ?? new Color(0.18f, 0.8f, 0.53f, 1f));

            _runningNameLabel = CreateSliderOverlayLabel(_runningSlider.transform, title, TextAlignmentOptions.MidlineLeft, 8f, 52f);
            _runningPercentLabel = CreateSliderOverlayLabel(_runningSlider.transform, "0 %", TextAlignmentOptions.MidlineRight, 8f, 52f);

            _runningBuildTime = totalTime;
            UpdateRunningProgress(timeRemaining);
        }
        else
        {
            OverlayUiKit.CreateLabel(row.transform, title, 13, FontStyles.Normal, TextAlignmentOptions.MidlineLeft,
                new Vector2(12f, 0f), new Vector2(-52f, 0f), _theme?.textPrimary ?? Color.white);
        }

        _queueRows.Add(row);
    }

    private void UpdateRunningProgress(float timeRemaining)
    {
        if (_runningSlider == null) return;
        float total = Mathf.Max(0.0001f, _runningBuildTime);
        float done = total - Mathf.Clamp(timeRemaining, 0f, total);
        float pct = Mathf.Clamp01(done / total);
        _runningSlider.value = pct;
        if (_runningPercentLabel != null)
            _runningPercentLabel.text = $"{Mathf.RoundToInt(pct * 100f)} %";
    }

    private static TextMeshProUGUI CreateSliderOverlayLabel(
        Transform slider, string text, TextAlignmentOptions align, float pad, float oppositeReserve)
    {
        var go = new GameObject("BarLabel", typeof(RectTransform));
        go.transform.SetParent(slider, false);
        go.transform.SetAsLastSibling();
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        bool left = align == TextAlignmentOptions.MidlineLeft || align == TextAlignmentOptions.Left;
        if (left)
        {
            rt.offsetMin = new Vector2(pad, 0f);
            rt.offsetMax = new Vector2(-oppositeReserve, 0f);
        }
        else
        {
            rt.offsetMin = new Vector2(oppositeReserve, 0f);
            rt.offsetMax = new Vector2(-pad, 0f);
        }

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 12;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.outlineWidth = 0.15f;
        tmp.outlineColor = new Color(0f, 0f, 0f, 0.75f);
        return tmp;
    }

    private static string GetBlueprintDisplayName(ProductBlueprint bp)
    {
        if (bp == null) return "—";
        if (!string.IsNullOrWhiteSpace(bp.displayName)) return bp.displayName;
        if (!string.IsNullOrWhiteSpace(bp.productId)) return bp.productId;
        return bp.name;
    }

    private static string BuildQueueSignature(ProductBlueprint current, IReadOnlyList<ProductBlueprint> queue)
    {
        var parts = new List<string>();
        if (current != null) parts.Add(current.productId ?? current.name);
        if (queue != null)
        {
            foreach (var bp in queue)
                parts.Add(bp != null ? bp.productId ?? bp.name : "<null>");
        }
        return string.Join("|", parts);
    }

    private void HighlightSelectedBlueprint(ProductBlueprint bp)
    {
        var normal = _theme?.buttonNormal ?? new Color(0.08f, 0.16f, 0.27f, 1f);
        var selected = _theme?.buttonHover ?? new Color(0.12f, 0.24f, 0.40f, 1f);

        foreach (var go in _blueprintButtons)
        {
            if (go == null || !go.TryGetComponent<Button>(out var btn)) continue;
            var img = btn.targetGraphic as Image;
            if (img == null) continue;
            bool isSelected = bp != null && go.name == (bp.productId ?? bp.displayName);
            img.color = isSelected ? selected : normal;
        }
    }

    private void DestroyBlueprintButtons()
    {
        foreach (var go in _blueprintButtons)
            if (go) Destroy(go);
        _blueprintButtons.Clear();
    }

    private void ClearBlueprints()
    {
        DestroyBlueprintButtons();
        _lastTemplateSignature = "";
    }

    private void ClearQueue()
    {
        foreach (var go in _queueRows)
            if (go) Destroy(go);
        _queueRows.Clear();
        _lastQueueSignature = "";
    }
}
