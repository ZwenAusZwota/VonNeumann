// Assets/Scripts/UI/FabricatorPanelController.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;          // LayoutRebuilder, Slider, Button
using TMPro;

public class FabricatorPanelController : MonoBehaviour
{
    [Header("UI Targets")]
    [SerializeField] private Transform blueprintPanel;
    [SerializeField] private Transform descriptionPanel;
    [SerializeField] private Transform queuePanel;              // <- Content des ScrollRects
    [SerializeField] private GameObject templateButtonPrefab;
    [SerializeField] private GameObject queueItemPrefab;

    [Header("Description Fields")]
    [SerializeField] private TMP_Text descTitle;
    [SerializeField] private Image descIcon;
    [SerializeField] private TMP_Text descText;
    [SerializeField] private TMP_Text descBuildTime;
    [SerializeField] private Transform costsContainer;
    [SerializeField] private GameObject costRowPrefab;
    [SerializeField] private Button addToQueueButton; // "Add to Queue"

    [Header("HUD Layout (Blueprint-Gitter)")]
    public int X_START = 0;
    public int Y_START = 0;
    public int X_SPACE_BETWEEN_ITEMS = 55;
    public int Y_SPACE_BETWEEN_ITEMS = 55;
    public int NUMBER_OF_COLUMNS = 4;

    private FabricatorController boundFab;

    private readonly List<GameObject> templateItems = new();
    private readonly List<GameObject> queueItems = new();
    private readonly List<GameObject> costRows = new();

    private ProductBlueprint _selectedBlueprint;

    // ---- UI-Update-Optimierung ----
    private List<string> _lastQueueOrder = new();         // [ currentId? , queuedIds ... ]
    private QueueItemUI _runningItemUI;                   // Referenz auf UI des laufenden Items

    private void Awake()
    {
        // Fallback: Add-to-Queue-Button innerhalb des DescriptionPanels finden
        if (addToQueueButton == null && descriptionPanel != null)
        {
            var allBtns = descriptionPanel.GetComponentsInChildren<Button>(true);
            foreach (var b in allBtns)
            {
                if (b.name == "btnAddToQueue") { addToQueueButton = b; break; }
            }
        }

        if (addToQueueButton != null)
        {
            addToQueueButton.onClick.RemoveAllListeners();
            addToQueueButton.onClick.AddListener(OnClickAddToQueue);
        }
    }

    private void OnEnable()
    {
        if (HUDBindingService.I != null)
        {
            HUDBindingService.I.OnSelectionChanged += HandleSelectionChanged;
            HUDBindingService.I.OnItemChanged += HandleItemChanged;
            HUDBindingService.I.OnListReset += HandleListReset;
        }
        RebindToSelected();
    }

    private void OnDisable()
    {
        if (HUDBindingService.I != null)
        {
            HUDBindingService.I.OnSelectionChanged -= HandleSelectionChanged;
            HUDBindingService.I.OnItemChanged -= HandleItemChanged;
            HUDBindingService.I.OnListReset -= HandleListReset;
        }
        UnbindCurrent();
    }

    private void HandleSelectionChanged(HUDItem _) => RebindToSelected();

    private void HandleItemChanged(HUDItem item)
    {
        var sel = HUDBindingService.I?.SelectedItem;
        if (sel != null && item != null && sel.Id == item.Id)
            boundFab?.ForceRefreshUI();
    }

    private void HandleListReset(IReadOnlyList<HUDItem> _) => RebindToSelected();

    private void RebindToSelected()
    {
        var sel = HUDBindingService.I?.SelectedItem;
        var tr = sel?.Transform;
        var fab = tr ? tr.GetComponent<FabricatorController>() : null;

        if (fab == boundFab) return;

        UnbindCurrent();
        Bind(fab);
    }

    private void Bind(FabricatorController fab)
    {
        boundFab = fab;
        if (boundFab == null)
        {
            ClearTemplates();
            RebuildQueueUI(null, 0f, null); // leert sauber
            ClearDescription();
            return;
        }

        boundFab.TemplatesUpdated += OnTemplatesUpdated;
        boundFab.QueueUpdated += OnQueueUpdated;
        boundFab.ProductionStarted += OnProductionStarted;
        boundFab.ProductionCompleted += OnProductionCompleted;
        boundFab.ProductionWaitingForResources += OnWaitingForResources;

        boundFab.ForceRefreshUI();
    }

    private void UnbindCurrent()
    {
        if (boundFab == null) return;

        boundFab.TemplatesUpdated -= OnTemplatesUpdated;
        boundFab.QueueUpdated -= OnQueueUpdated;
        boundFab.ProductionStarted -= OnProductionStarted;
        boundFab.ProductionCompleted -= OnProductionCompleted;
        boundFab.ProductionWaitingForResources -= OnWaitingForResources;

        boundFab = null;

        ClearTemplates();
        RebuildQueueUI(null, 0f, null);
        ClearDescription();
    }

    /* ---------- Templates ---------- */
    private void OnTemplatesUpdated(IReadOnlyList<ProductBlueprint> templates)
    {
        if (blueprintPanel == null || templateButtonPrefab == null) return;

        ClearTemplates();

        for (int i = 0; i < templates.Count; i++)
        {
            var bp = templates[i];
            var go = Instantiate(templateButtonPrefab, blueprintPanel);
            templateItems.Add(go);

            var rect = go.GetComponent<RectTransform>();
            if (rect != null) rect.localPosition = GetPosition(i);

            var txt = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (txt) txt.text = bp.displayName;

            var img = go.GetComponentInChildren<Image>(true);
            if (img && bp.icon) img.sprite = bp.icon;

            var btn = go.GetComponent<Button>();
            if (btn)
            {
                var localBp = bp;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => ShowDescription(localBp));
            }
        }

        if (templates.Count > 0) ShowDescription(templates[0]);
    }

    private void ShowDescription(ProductBlueprint bp)
    {
        _selectedBlueprint = bp;
        if (bp == null) { ClearDescription(); return; }

        if (descTitle) descTitle.text = !string.IsNullOrEmpty(bp.displayName) ? bp.displayName : bp.name;
        if (descIcon) { descIcon.enabled = bp.icon != null; descIcon.sprite = bp.icon; }
        if (descText) descText.text = bp.description ?? "";
        if (descBuildTime) descBuildTime.text = bp.buildTime > 0f ? $"{bp.buildTime:0.#} s" : "sofort";

        ClearCosts();
        if (costsContainer && costRowPrefab)
        {
            if (bp.resourceCosts != null)
            {
                foreach (var c in bp.resourceCosts)
                {
                    var row = Instantiate(costRowPrefab, costsContainer);
                    costRows.Add(row);
                    var texts = row.GetComponentsInChildren<TMP_Text>(true);
                    if (texts.Length >= 2)
                    {
                        texts[0].text = c.resource ? c.resource.name : "Resource";
                        texts[1].text = c.amount.ToString();
                    }
                }
            }
            if (bp.componentCosts != null)
            {
                foreach (var c in bp.componentCosts)
                {
                    var row = Instantiate(costRowPrefab, costsContainer);
                    costRows.Add(row);
                    var texts = row.GetComponentsInChildren<TMP_Text>(true);
                    if (texts.Length >= 2)
                    {
                        texts[0].text = c.product ? c.product.displayName : "Component";
                        texts[1].text = c.amount.ToString();
                    }
                }
            }
        }

        if (descriptionPanel && !descriptionPanel.gameObject.activeSelf)
            descriptionPanel.gameObject.SetActive(true);
    }

    private void OnClickAddToQueue()
    {
        if (boundFab == null || _selectedBlueprint == null) return;
        boundFab.Enqueue(_selectedBlueprint);
    }

    /* ---------- Queue ---------- */
    private void OnQueueUpdated(ProductBlueprint current, float timeRemaining, IReadOnlyList<ProductBlueprint> queue)
    {
        // 1) berechne aktuelle Ordnung (IDs)
        var currOrder = BuildOrderSignature(current, queue);

        // 2) Struktur-Change?
        bool structureChanged = !SameOrder(_lastQueueOrder, currOrder);

        if (structureChanged)
        {
            // Voller Rebuild NUR wenn sich Inhalt/Reihenfolge änderte
            RebuildQueueUI(current, timeRemaining, queue);
            _lastQueueOrder = currOrder;
        }
        else
        {
            // Nur Fortschritt des laufenden Items aktualisieren
            UpdateRunningProgress(current, timeRemaining);
        }
    }

    private void RebuildQueueUI(ProductBlueprint current, float timeRemaining, IReadOnlyList<ProductBlueprint> queue)
    {
        // räume UI-Elemente einmalig ab
        foreach (var go in queueItems) if (go) Destroy(go);
        queueItems.Clear();
        _runningItemUI = null;

        if (queuePanel == null || queueItemPrefab == null)
        {
            return;
        }

        // Laufendes Item
        if (current != null)
        {
            var go = CreateQueueItemGO();
            if (go != null)
            {
                queueItems.Add(go);

                var ui = go.GetComponent<QueueItemUI>();
                if (ui != null)
                {
                    float total = Mathf.Max(0.0001f, current.buildTime);
                    float done = total - Mathf.Clamp(timeRemaining, 0f, total);
                    float pct = Mathf.Clamp01(done / total);
                    ui.BindRunningItem(boundFab, current, 0, pct, timeRemaining);
                    _runningItemUI = ui; // merken für reine Fortschritt-Updates
                }
            }
        }

        // Wartende Items (Queue)
        if (queue != null)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                if (current != null && i == 0) continue; // Index 0 ist laufendes Element

                var bp = queue[i];
                var go = CreateQueueItemGO();
                if (go != null)
                {
                    queueItems.Add(go);

                    var ui = go.GetComponent<QueueItemUI>();
                    if (ui != null)
                    {
                        int index = i; // echter Queue-Index im Controller
                        ui.BindQueuedItem(boundFab, bp, index);
                    }
                }
            }
        }

        // Layout einmal hart aktualisieren
        var rt = queuePanel as RectTransform;
        if (rt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    private void UpdateRunningProgress(ProductBlueprint current, float timeRemaining)
    {
        if (_runningItemUI == null || current == null) return;

        float total = Mathf.Max(0.0001f, current.buildTime);
        float done = total - Mathf.Clamp(timeRemaining, 0f, total);
        float pct = Mathf.Clamp01(done / total);

        // QueueItemUI aktualisiert Slider + ETA intern
        _runningItemUI.BindRunningItem(boundFab, current, 0, pct, timeRemaining);
    }

    /* ---------- Production Events (optional nutzbar) ---------- */
    private void OnProductionStarted(ProductBlueprint bp) { /* optional UI Effekte */ }
    private void OnProductionCompleted(ProductBlueprint bp, bool stored) { /* optional Meldungen */ }
    private void OnWaitingForResources(ProductBlueprint bp) { /* optional Hinweis */ }

    /* ---------- Utilities ---------- */
    private void ClearTemplates()
    {
        foreach (var go in templateItems) if (go) Destroy(go);
        templateItems.Clear();
    }

    private void ClearCosts()
    {
        foreach (var go in costRows) if (go) Destroy(go);
        costRows.Clear();
    }

    private void ClearDescription()
    {
        if (descTitle) descTitle.text = "";
        if (descText) descText.text = "";
        if (descIcon) { descIcon.sprite = null; descIcon.enabled = false; }
        if (descBuildTime) descBuildTime.text = "";
        ClearCosts();
    }

    private Vector3 GetPosition(int i)
    {
        return new Vector3(
            X_START + (X_SPACE_BETWEEN_ITEMS * (i % NUMBER_OF_COLUMNS)),
            Y_START + (-(Y_SPACE_BETWEEN_ITEMS * (i / NUMBER_OF_COLUMNS))),
            0f
        );
    }

    /// <summary>Erzeugt ein Queue-Item als Kind von queuePanel (Content)</summary>
    private GameObject CreateQueueItemGO()
    {
        if (queueItemPrefab == null || queuePanel == null)
        {
            Debug.LogWarning("[FabricatorPanelController] queueItemPrefab oder queuePanel fehlt.");
            return null;
        }

        var go = Instantiate(queueItemPrefab);
        if (!go.activeSelf) go.SetActive(true);

        var parentRt = queuePanel as RectTransform;
        var childRt = go.GetComponent<RectTransform>();
        if (parentRt == null || childRt == null)
        {
            Debug.LogWarning("[FabricatorPanelController] Parent oder Child hat kein RectTransform.");
        }

        // Einfach sauber parenten – LayoutGroup regelt Position/Größe
        childRt.SetParent(parentRt, false);
        childRt.SetAsLastSibling();

        // Kein manuelles Anchor/Position-Forcing – das macht die VerticalLayoutGroup

        return go;
    }

    /// <summary>Baut eine eindeutige Signatur der Reihenfolge (IDs) für Strukturvergleich</summary>
    private static List<string> BuildOrderSignature(ProductBlueprint current, IReadOnlyList<ProductBlueprint> queue)
    {
        var list = new List<string>((queue?.Count ?? 0) + 1);
        if (current != null)
        {
            list.Add(current.productId ?? current.name);
        }
        if (queue != null)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                // Falls Controller queue[0] == current enthält, wird das hier mit aufgenommen.
                // Das ist okay, denn OnQueueUpdated überspringt es beim Rendern – für den
                // Strukturvergleich ist wichtig, ob sich die Identität/Reihenfolge geändert hat.
                var bp = queue[i];
                list.Add(bp != null ? (bp.productId ?? bp.name) : "<null>");
            }
        }
        return list;
    }

    private static bool SameOrder(List<string> a, List<string> b)
    {
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return false;
        return true;
    }
}
