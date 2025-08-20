// Assets/Scripts/UI/FabricatorPanelController.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FabricatorPanelController : MonoBehaviour
{
    [Header("UI Targets")]
    [SerializeField] private Transform blueprintPanel;
    [SerializeField] private Transform descriptionPanel;
    [SerializeField] private Transform queuePanel;
    [SerializeField] private GameObject templateButtonPrefab;
    [SerializeField] private GameObject queueItemPrefab;

    [Header("HUD Layout")]
    public int X_START = 0;
    public int Y_START = 0;
    public int X_SPACE_BETWEEN_ITEMS = 55;
    public int Y_SPACE_BETWEEN_ITEMS = 55;
    public int NUMBER_OF_COLUMNS = 4;

    private FabricatorController boundFab;

    private readonly List<GameObject> templateItems = new();
    private readonly List<GameObject> queueItems = new();

    private void Awake()
    {
        if (blueprintPanel == null)
        {
            var root = GameObject.Find("FabricatorPanel");
            if (root != null)
            {
                var t = root.transform.Find("BluprintPanel");
                if (t != null) blueprintPanel = t;
            }
        }
    }

    private void OnEnable()
    {
        HUDBindingService.ActiveFabricatorChanged += OnActiveFabricatorChanged;
        HUDBindingService.Reannounce();
    }

    private void OnDisable()
    {
        HUDBindingService.ActiveFabricatorChanged -= OnActiveFabricatorChanged;
        UnbindCurrent();
    }

    private void OnActiveFabricatorChanged(FabricatorController fab)
    {
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
            ClearQueue();
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
        ClearQueue();
    }

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
            if (btn) btn.onClick.AddListener(() => showDescription(bp));
        }
    }

    private void showDescription(ProductBlueprint bp)
    {
        if (descriptionPanel == null) return;

        var t = descriptionPanel.Find("txtDescr");
        if (t == null) return;

        var txt = t.GetComponent<TextMeshProUGUI>();
        if (txt == null) return;

        txt.text = bp.description;
    }

    private void OnQueueUpdated(ProductBlueprint current, float timeRemaining, IReadOnlyList<ProductBlueprint> queue)
    {
        if (queuePanel == null || queueItemPrefab == null) return;

        ClearQueue();

        if (current != null)
        {
            var go = Instantiate(queueItemPrefab, queuePanel);
            queueItems.Add(go);

            var nameText = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (nameText) nameText.text = $"{current.displayName}";

            var slider = go.GetComponentInChildren<Slider>(true);
            if (slider)
            {
                float total = Mathf.Max(0.0001f, current.buildTime);
                slider.minValue = 0f;
                slider.maxValue = total;
                slider.value = total - Mathf.Clamp(timeRemaining, 0f, total);
            }
        }

        if (queue != null)
        {
            foreach (var bp in queue)
            {
                var go = Instantiate(queueItemPrefab, queuePanel);
                queueItems.Add(go);

                var nameText = go.GetComponentInChildren<TextMeshProUGUI>(true);
                if (nameText) nameText.text = bp.displayName;

                var slider = go.GetComponentInChildren<Slider>(true);
                if (slider)
                {
                    slider.minValue = 0f;
                    slider.maxValue = 1f;
                    slider.value = 0f;
                }
            }
        }
    }

    private void OnProductionStarted(ProductBlueprint bp) { }
    private void OnProductionCompleted(ProductBlueprint bp, bool stored) { }
    private void OnWaitingForResources(ProductBlueprint bp) { }

    private void ClearTemplates()
    {
        foreach (var go in templateItems) if (go) Destroy(go);
        templateItems.Clear();
    }

    private void ClearQueue()
    {
        foreach (var go in queueItems) if (go) Destroy(go);
        queueItems.Clear();
    }

    private Vector3 GetPosition(int i)
    {
        return new Vector3(
            X_START + (X_SPACE_BETWEEN_ITEMS * (i % NUMBER_OF_COLUMNS)),
            Y_START + (-(Y_SPACE_BETWEEN_ITEMS * (i / NUMBER_OF_COLUMNS))),
            0f
        );
    }
}
