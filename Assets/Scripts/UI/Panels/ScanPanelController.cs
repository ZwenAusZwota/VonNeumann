// Assets/Scripts/UI/ScanPanelController.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScanPanelController : MonoBehaviour
{
    [Header("UI Targets")]
    [SerializeField] private Transform listContainer;
    [SerializeField] private GameObject listItemPrefab;
    [SerializeField] private TextMeshProUGUI txtNearScan;
    [SerializeField] private TextMeshProUGUI txtFarScan;

    [Header("Legacy (Auto-Wire)")]
    [SerializeField] private Transform nearContainer;
    [SerializeField] private Transform farContainer;
    [SerializeField] private GameObject listItemPrefabNear;
    [SerializeField] private GameObject listItemPrefabFar;

    [Header("Responsives Layout")]
    [SerializeField] private bool applyResponsiveLayout = true;
    [SerializeField] private RectTransform listPanel;
    [SerializeField] private RectTransform nearScanPanel;
    [SerializeField] private RectTransform farScanButton;
    [SerializeField] private RectTransform nearScanButton;
    [SerializeField] private float headerHeight = 50f;
    [SerializeField] private float scanButtonHeight = 44f;
    [SerializeField] private float sortRowHeight = 30f;
    [SerializeField] private float buttonColumnWidth = 152f;
    [SerializeField] private float outerPadding = 8f;
    [SerializeField] private float buttonSpacing = 4f;
    [SerializeField] private float liveDistanceRefreshInterval = 0.5f;

    private ScanSortMode _sortMode = ScanSortMode.ProbeDistance;
    private bool _scanButtonStyleApplied;
    private RectTransform _sortRow;
    private Button _btnSortProbe;
    private Button _btnSortStar;
    private readonly HashSet<string> _expandedEntryIds = new();
    private readonly List<SystemObject> _displayCatalog = new();
    private float _liveRefreshTimer;
    private string _lastSortSignature = string.Empty;

    private void Awake()
    {
        AutoWireLayoutTargets();
        EnsureSortControls();
        ApplyResponsiveLayout();
        if (Application.isPlaying)
            EnsureScanButtonStyle();
    }

    private void OnRectTransformDimensionsChange()
    {
        ApplyResponsiveLayout();
    }

    private void OnEnable()
    {
        if (HUDBindingService.I != null)
        {
            HUDBindingService.I.OnSelectionChanged += HandleSelectionChanged;
            HUDBindingService.I.OnItemChanged += HandleItemChanged;
            HUDBindingService.I.OnListReset += HandleListReset;
        }
        ApplyResponsiveLayout();
        if (!_scanButtonStyleApplied)
            EnsureScanButtonStyle();
        RefreshAll();
    }

    private void OnDisable()
    {
        if (HUDBindingService.I != null)
        {
            HUDBindingService.I.OnSelectionChanged -= HandleSelectionChanged;
            HUDBindingService.I.OnItemChanged -= HandleItemChanged;
            HUDBindingService.I.OnListReset -= HandleListReset;
        }

        _scanButtonStyleApplied = false;
    }

    private void Update()
    {
        if (!isActiveAndEnabled || liveDistanceRefreshInterval <= 0f)
            return;

        _liveRefreshTimer += Time.deltaTime;
        if (_liveRefreshTimer < liveDistanceRefreshInterval)
            return;

        _liveRefreshTimer = 0f;
        RefreshLiveDistances();
    }

    public void OnNearScanClicked()
    {
        var sel = HUDBindingService.I?.SelectedItem;
        var tr = sel?.Transform;
        if (!tr) return;

        var near = tr.GetComponent<NearScannerController>();
        near?.PerformScan();
    }

    public void OnFarScanClicked()
    {
        var sel = HUDBindingService.I?.SelectedItem;
        var tr = sel?.Transform;
        if (!tr) return;

        var far = tr.GetComponent<FarScannerController>();
        far?.PerformScan();
    }

    public void OnSortByProbeClicked() => SetSortMode(ScanSortMode.ProbeDistance);

    public void OnSortByStarClicked() => SetSortMode(ScanSortMode.StarDistance);

    private void SetSortMode(ScanSortMode mode)
    {
        if (_sortMode == mode) return;
        _sortMode = mode;
        UpdateSortButtonVisuals();
        RefreshAll();
    }

    private void HandleSelectionChanged(HUDItem item) => RefreshAll();
    private void HandleItemChanged(HUDItem item)
    {
        var sel = HUDBindingService.I?.SelectedItem;
        if (sel != null && item != null && sel.Id == item.Id)
            RefreshAll();
    }
    private void HandleListReset(IReadOnlyList<HUDItem> _) => RefreshAll();

    private void AutoWireLayoutTargets()
    {
        if (listPanel == null)
            listPanel = transform.Find("pnlFarScan") as RectTransform;
        if (nearScanPanel == null)
            nearScanPanel = transform.Find("pnlNearScan") as RectTransform;
        if (farScanButton == null)
            farScanButton = transform.Find("txtFarScan") as RectTransform;
        if (nearScanButton == null)
            nearScanButton = transform.Find("txtNearScan") as RectTransform;

        if (farContainer != null && listContainer == null)
            listContainer = farContainer;
        if (nearContainer != null && listContainer == null)
            listContainer = nearContainer;

        if (listContainer == null && listPanel != null)
        {
            var scroll = listPanel.GetComponent<ScrollRect>();
            if (scroll != null && scroll.content != null)
                listContainer = scroll.content;
            else
                listContainer = listPanel.Find("Viewport/Content") ?? listPanel.Find("Content");
        }

        if (listItemPrefab == null)
            listItemPrefab = listItemPrefabFar != null ? listItemPrefabFar : listItemPrefabNear;

        if (farScanButton != null)
            txtFarScan = ResolveScanButtonLabel(farScanButton) ?? txtFarScan;
        if (nearScanButton != null)
            txtNearScan = ResolveScanButtonLabel(nearScanButton) ?? txtNearScan;

        if (nearScanPanel != null)
            nearScanPanel.gameObject.SetActive(false);
    }

    private void EnsureSortControls()
    {
        if (_sortRow != null) return;

        var theme = UITheme.Instance;
        var rowGo = new GameObject("SortRow", typeof(RectTransform));
        _sortRow = rowGo.GetComponent<RectTransform>();
        _sortRow.SetParent(transform, false);

        _btnSortProbe = CreateSortButton(rowGo.transform, "btnSortProbe", "Zur Sonde", OnSortByProbeClicked, theme);
        _btnSortStar = CreateSortButton(rowGo.transform, "btnSortStar", "Zum Stern", OnSortByStarClicked, theme);
        UpdateSortButtonVisuals();
    }

    private static Button CreateSortButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClick, UITheme theme)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(name == "btnSortProbe" ? 0f : 0.5f, 0f);
        rect.anchorMax = new Vector2(name == "btnSortProbe" ? 0.5f : 1f, 1f);
        rect.offsetMin = new Vector2(0f, 0f);
        rect.offsetMax = new Vector2(0f, 0f);

        var img = go.GetComponent<Image>();
        img.color = theme != null ? theme.buttonNormal : new Color(0.08f, 0.16f, 0.27f, 1f);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 12f;
        tmp.color = theme != null ? theme.textPrimary : Color.white;
        tmp.raycastTarget = false;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        if (theme != null)
        {
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = theme.buttonHover;
            colors.pressedColor = theme.buttonPressed;
            colors.selectedColor = theme.buttonHover;
            colors.disabledColor = theme.backgroundDisabled;
            btn.colors = colors;
        }
        btn.onClick.AddListener(onClick);
        return btn;
    }

    private void UpdateSortButtonVisuals()
    {
        var theme = UITheme.Instance;
        StyleSortButton(_btnSortProbe, _sortMode == ScanSortMode.ProbeDistance, theme);
        StyleSortButton(_btnSortStar, _sortMode == ScanSortMode.StarDistance, theme);
    }

    private static void StyleSortButton(Button button, bool active, UITheme theme)
    {
        if (button == null) return;

        var img = button.targetGraphic as Image;
        if (img == null) return;

        if (theme == null)
        {
            img.color = active ? new Color(0.12f, 0.24f, 0.40f, 1f) : new Color(0.08f, 0.16f, 0.27f, 1f);
            return;
        }

        img.color = active ? theme.buttonHover : theme.buttonNormal;

        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.color = active ? theme.textAccent : theme.textSecondary;
    }

    private void ApplyResponsiveLayout()
    {
        if (!applyResponsiveLayout) return;

        AutoWireLayoutTargets();

        float top = headerHeight;
        float farTop = top;
        float nearTop = top + scanButtonHeight + buttonSpacing;
        float sortTop = nearTop + scanButtonHeight + buttonSpacing;
        float leftColumnBottom = sortTop + sortRowHeight;
        float listTopInset = leftColumnBottom + outerPadding;

        if (farScanButton != null)
        {
            SetStretch(
                farScanButton,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(outerPadding, -(farTop + scanButtonHeight)),
                new Vector2(outerPadding + buttonColumnWidth, -farTop));
        }

        if (nearScanButton != null)
        {
            SetStretch(
                nearScanButton,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(outerPadding, -(nearTop + scanButtonHeight)),
                new Vector2(outerPadding + buttonColumnWidth, -nearTop));
        }

        if (_sortRow != null)
        {
            SetStretch(
                _sortRow,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(outerPadding, -(sortTop + sortRowHeight)),
                new Vector2(outerPadding + buttonColumnWidth, -sortTop));
        }

        if (listPanel != null)
        {
            SetStretch(
                listPanel,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(buttonColumnWidth + outerPadding * 2f, outerPadding),
                new Vector2(-outerPadding, -listTopInset));
        }
    }

    private void EnsureScanButtonStyle()
    {
        if (!Application.isPlaying || _scanButtonStyleApplied) return;

        AutoWireLayoutTargets();

        var farOk = StyleScanButton(farScanButton, ref txtFarScan, "Far Scan");
        var nearOk = StyleScanButton(nearScanButton, ref txtNearScan, "Near Scan");
        if (farOk && nearOk)
        {
            if (txtFarScan) txtFarScan.text = "Far Scan";
            if (txtNearScan) txtNearScan.text = "Near Scan";
            _scanButtonStyleApplied = true;
        }
    }

    private static bool StyleScanButton(RectTransform buttonRect, ref TextMeshProUGUI label, string defaultText)
    {
        if (!buttonRect) return false;

        label = ResolveScanButtonLabel(buttonRect) ?? label;

        var theme = UITheme.Instance;
        var bg = GetOrCreateButtonBackground(buttonRect);
        if (bg == null) return false;

        if (theme != null)
            bg.color = theme.buttonNormal;

        label = GetOrCreateLabelChild(buttonRect, label, defaultText);
        if (!label) return false;

        var btn = buttonRect.GetComponent<Button>();
        if (btn != null)
        {
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = theme != null ? theme.buttonHover : new Color(0.9f, 0.95f, 1f, 1f);
            colors.pressedColor = theme != null ? theme.buttonPressed : new Color(0.7f, 0.75f, 0.85f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = theme != null ? theme.backgroundDisabled : new Color(0.4f, 0.4f, 0.4f, 0.5f);
            btn.colors = colors;
        }

        label.color = theme != null ? theme.textPrimary : Color.white;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.margin = Vector4.zero;
        label.raycastTarget = false;
        label.text = defaultText;

        return true;
    }

    private static TextMeshProUGUI ResolveScanButtonLabel(RectTransform buttonRect)
    {
        if (!buttonRect) return null;

        var labelChild = buttonRect.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if (labelChild) return labelChild;

        var onRoot = buttonRect.GetComponent<TextMeshProUGUI>();
        if (onRoot) return onRoot;

        return buttonRect.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private static TextMeshProUGUI GetOrCreateLabelChild(RectTransform buttonRect, TextMeshProUGUI sourceLabel, string defaultText)
    {
        if (!buttonRect) return null;

        var existing = buttonRect.Find("Label")?.GetComponent<TextMeshProUGUI>();
        if (existing != null)
        {
            StretchLabel(existing.rectTransform);
            existing.transform.SetAsLastSibling();
            DisableRootLabel(buttonRect, sourceLabel);
            return existing;
        }

        if (sourceLabel != null && sourceLabel.transform != buttonRect)
        {
            StretchLabel(sourceLabel.rectTransform);
            sourceLabel.transform.SetAsLastSibling();
            return sourceLabel;
        }

        var labelGo = new GameObject("Label", typeof(RectTransform));
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.SetParent(buttonRect, false);
        StretchLabel(labelRect);

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        if (sourceLabel != null)
            CopyTextMeshSettings(sourceLabel, label);
        label.text = defaultText;
        label.raycastTarget = false;
        labelGo.transform.SetAsLastSibling();

        DisableRootLabel(buttonRect, sourceLabel);
        return label;
    }

    private static void DisableRootLabel(RectTransform buttonRect, TextMeshProUGUI sourceLabel)
    {
        if (sourceLabel != null && sourceLabel.transform == buttonRect)
            sourceLabel.enabled = false;

        var rootTmp = buttonRect.GetComponent<TextMeshProUGUI>();
        if (rootTmp != null && rootTmp != sourceLabel)
            rootTmp.enabled = false;
    }

    private static void StretchLabel(RectTransform labelRect)
    {
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    private static void CopyTextMeshSettings(TextMeshProUGUI from, TextMeshProUGUI to)
    {
        if (from == null || to == null) return;

        if (from.font != null)
            to.font = from.font;

        to.fontSize = from.fontSize;
        to.fontSizeMin = from.fontSizeMin;
        to.fontSizeMax = from.fontSizeMax;
        to.enableAutoSizing = from.enableAutoSizing;
        to.alignment = TextAlignmentOptions.Center;
        to.verticalAlignment = VerticalAlignmentOptions.Middle;
    }

    private static Image GetOrCreateButtonBackground(RectTransform buttonRect)
    {
        if (!buttonRect || !Application.isPlaying) return null;

        var existing = buttonRect.Find("ButtonBg") as RectTransform;
        if (existing != null)
            return existing.GetComponent<Image>();

        var bgGo = new GameObject("ButtonBg", typeof(RectTransform), typeof(Image));
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.SetParent(buttonRect, false);
        bgRect.SetAsFirstSibling();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        var bg = bgGo.GetComponent<Image>();
        if (bg != null)
            bg.raycastTarget = true;

        return bg;
    }

    private static void SetStretch(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        if (rect == null) return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private void RefreshAll()
    {
        EnsureListContainerLayout(listContainer);

        var sel = HUDBindingService.I?.SelectedItem;
        var probeTransform = sel?.Transform;
        if (!probeTransform)
        {
            _displayCatalog.Clear();
            ClearContainer(listContainer);
            return;
        }

        var nearVM = probeTransform.GetComponent<NearScanViewModel>();
        var farVM = probeTransform.GetComponent<FarScanViewModel>();

        _displayCatalog.Clear();
        _displayCatalog.AddRange(ScanPanelSortHelper.MergeDistinct(
            nearVM != null ? nearVM.LatestEntries : null,
            farVM != null ? farVM.LatestEntries : null));

        var star = ServiceContainer.Instance?.Get<PlanetRegistry>()?.Star;
        ScanPanelSortHelper.Sort(_displayCatalog, probeTransform.position, star, _sortMode);
        _lastSortSignature = BuildSortSignature(_displayCatalog, probeTransform.position, star);

        RebuildList(listContainer, listItemPrefab, _displayCatalog, probeTransform.position, star);
    }

    private void RefreshLiveDistances()
    {
        if (listContainer == null || _displayCatalog.Count == 0)
            return;

        var sel = HUDBindingService.I?.SelectedItem;
        var probeTransform = sel?.Transform;
        if (!probeTransform)
            return;

        var star = ServiceContainer.Instance?.Get<PlanetRegistry>()?.Star;
        var probePosition = probeTransform.position;

        ScanPanelSortHelper.Sort(_displayCatalog, probePosition, star, _sortMode);
        var signature = BuildSortSignature(_displayCatalog, probePosition, star);
        if (signature != _lastSortSignature)
        {
            _lastSortSignature = signature;
            RebuildList(listContainer, listItemPrefab, _displayCatalog, probePosition, star);
            return;
        }

        UpdateDistanceLabelsInPlace(probePosition, star);
    }

    private void UpdateDistanceLabelsInPlace(Vector3 probePosition, Transform star)
    {
        foreach (var item in listContainer.GetComponentsInChildren<ObjectItemUI>(true))
        {
            if (item?.SObject == null) continue;
            item.UpdateDistanceLabel(FormatDistanceLabel(item.SObject, probePosition, star));
        }
    }

    private string BuildSortSignature(List<SystemObject> entries, Vector3 probePosition, Transform star)
    {
        if (entries == null || entries.Count == 0) return string.Empty;

        var parts = new System.Text.StringBuilder(entries.Count * 12);
        foreach (var entry in entries)
            AppendSortSignature(parts, entry, probePosition, star);
        return parts.ToString();
    }

    private void AppendSortSignature(
        System.Text.StringBuilder parts,
        SystemObject entry,
        Vector3 probePosition,
        Transform star)
    {
        if (entry?.GameObject == null) return;

        parts.Append(entry.GameObject.GetEntityId().ToString());
        parts.Append(':');
        float dist = _sortMode == ScanSortMode.StarDistance
            ? ScanPanelSortHelper.GetStarDistanceUnits(star, entry)
            : ScanPanelSortHelper.GetProbeDistanceUnits(probePosition, entry);
        parts.Append((int)(dist * 10f));
        parts.Append('|');

        foreach (var child in entry.Children)
            AppendSortSignature(parts, child, probePosition, star);
    }

    private void EnsureListContainerLayout(Transform container)
    {
        if (container is not RectTransform rect) return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);

        var vlg = container.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
        }
    }

    private void ClearContainer(Transform container)
    {
        if (!container) return;
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
    }

    private void RebuildList(
        Transform container,
        GameObject prefab,
        List<SystemObject> entries,
        Vector3 probePosition,
        Transform star)
    {
        if (!container || !prefab) return;

        ClearContainer(container);

        foreach (var entry in entries)
            AddEntryRow(container, prefab, entry, probePosition, star, depth: 0, isChildRow: false);

        if (container is RectTransform rect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            if (rect.parent is RectTransform viewport)
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
        }
    }

    private void AddEntryRow(
        Transform container,
        GameObject prefab,
        SystemObject entry,
        Vector3 probePosition,
        Transform star,
        int depth,
        bool isChildRow)
    {
        if (entry == null || !entry.GameObject) return;

        string entryKey = GetEntryKey(entry);
        bool hasChildren = entry.Children != null && entry.Children.Count > 0;
        bool isExpanded = hasChildren && _expandedEntryIds.Contains(entryKey);

        var go = Instantiate(prefab, container, false);
        var item = go.GetComponent<ObjectItemUI>();
        if (item == null)
        {
            Debug.LogWarning("[ScanPanelController] Prefab ohne ObjectItemUI: " + prefab.name);
            return;
        }

        string objectLabel = ResolveObjectLabel(entry);
        string distanceLabel = FormatDistanceLabel(entry, probePosition, star);
        item.Init(entry, depth, hasChildren, isExpanded, isChildRow, distanceLabel, objectLabel);
        item.ExpandToggled += HandleExpandToggled;

        if (!isExpanded || entry.Children == null)
            return;

        foreach (var child in entry.Children)
            AddEntryRow(container, prefab, child, probePosition, star, depth + 1, isChildRow: true);
    }

    private void HandleExpandToggled(ObjectItemUI item)
    {
        if (item?.SObject == null) return;

        string key = GetEntryKey(item.SObject);
        if (item.IsExpanded)
            _expandedEntryIds.Add(key);
        else
            _expandedEntryIds.Remove(key);

        RefreshAll();
    }

    private static string GetEntryKey(SystemObject entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Id))
            return entry.Id;
        return entry.GameObject != null
            ? entry.GameObject.GetEntityId().ToString()
            : entry.GetHashCode().ToString();
    }

    private string FormatDistanceLabel(SystemObject entry, Vector3 probePosition, Transform star)
    {
        float distUnits = _sortMode == ScanSortMode.StarDistance
            ? ScanPanelSortHelper.GetStarDistanceUnits(star, entry)
            : ScanPanelSortHelper.GetProbeDistanceUnits(probePosition, entry);

        if (!float.IsFinite(distUnits)) return string.Empty;

        float distAu = ScanPanelSortHelper.UnitsToAu(distUnits);
        if (distAu >= 0.05f)
            return $"{distAu:0.###} AU";

        float distKm = distUnits * Mathf.Max(PlanetScale.KM_PER_UNIT, 1e-6f);
        return $"{(int)distKm:N0} km";
    }

    private static string ResolveObjectLabel(SystemObject entry)
    {
        if (entry?.GameObject != null)
        {
            var go = entry.GameObject;
            if (go.GetComponent<AsteroidBelt>() != null)
                return string.IsNullOrWhiteSpace(go.name) ? "Asteroidengürtel" : go.name;
            if (ScanAsteroidHelper.IsAsteroidBody(go))
                return ResolveAsteroidLabel(go);
            if (go.CompareTag("Star"))
                return string.IsNullOrWhiteSpace(go.name) ? "Star" : go.name;
            if (go.GetComponent<Planet>() != null || go.CompareTag("Planet"))
                return string.IsNullOrWhiteSpace(go.name) ? "Planet" : go.name;
            if (ScanOrbiterHelper.IsOrbiterBody(go))
                return string.IsNullOrWhiteSpace(go.name) ? (ScanOrbiterHelper.IsMoonBody(go) ? "Mond" : "Satellit") : go.name;
            if (!string.IsNullOrWhiteSpace(go.name))
                return go.name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(entry.DisplayName))
            return StripScannerDisplaySuffix(entry.DisplayName);

        return string.IsNullOrWhiteSpace(entry.Name) ? "Object" : entry.Name;
    }

    private static string ResolveAsteroidLabel(GameObject go)
    {
        if (!string.IsNullOrWhiteSpace(go.name))
            return go.name.Trim();

        var mineable = go.GetComponent<MineableAsteroid>() ?? go.GetComponentInParent<MineableAsteroid>();
        if (mineable != null && !string.IsNullOrEmpty(mineable.materialId))
        {
            var matDef = MaterialDatabase.Get(mineable.materialId);
            if (matDef != null && !string.IsNullOrWhiteSpace(matDef.displayName))
                return matDef.displayName;
        }

        return "Asteroid";
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
}
