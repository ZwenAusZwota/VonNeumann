using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FabricatorPanelController : MonoBehaviour
{
    [Header("UI Targets")]
    [SerializeField] private Transform blueprintPanel;             // FabricatorPanel/leftPanel
    [SerializeField] private Transform queuePanel;            // optional: eigener Container für die Queue
    [SerializeField] private GameObject templateButtonPrefab; // Button mit Text/Icon
    [SerializeField] private GameObject queueItemPrefab;      // Element mit Name + Slider

    // aktueller Fabricator
    private FabricatorController boundFab;

    // UI-Caches
    private readonly List<GameObject> templateItems = new();
    private readonly List<GameObject> queueItems = new();

    private void Awake()
    {
        // Fallback-Suche, falls nicht gesetzt
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
        FabricatorBindingService.ActiveFabricatorChanged += OnActiveFabricatorChanged;
        // Falls schon eine Auswahl existiert:
        FabricatorBindingService.Reannounce();
    }

private void OnDisable()
    {
        FabricatorBindingService.ActiveFabricatorChanged -= OnActiveFabricatorChanged;
        UnbindCurrent();
    }

    // ───────────────────────── Binding ─────────────────────────

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

        // Events anhängen
        boundFab.TemplatesUpdated += OnTemplatesUpdated;
        boundFab.QueueUpdated += OnQueueUpdated;
        boundFab.ProductionStarted += OnProductionStarted;
        boundFab.ProductionCompleted += OnProductionCompleted;
        boundFab.ProductionWaitingForResources += OnWaitingForResources;

        // initialen Stand ziehen
        boundFab.ForceRefreshUI();
    }

    private void UnbindCurrent()
    {
        if (boundFab == null) return;

        // Events lösen
        boundFab.TemplatesUpdated -= OnTemplatesUpdated;
        boundFab.QueueUpdated -= OnQueueUpdated;
        boundFab.ProductionStarted -= OnProductionStarted;
        boundFab.ProductionCompleted -= OnProductionCompleted;
        boundFab.ProductionWaitingForResources -= OnWaitingForResources;

        boundFab = null;

        // UI leeren
        ClearTemplates();
        ClearQueue();
    }

    // ───────────────────────── Event-Handler ─────────────────────────

    private void OnTemplatesUpdated(IReadOnlyList<ProductBlueprint> templates)
    {
        if (blueprintPanel == null || templateButtonPrefab == null) return;

        ClearTemplates();

        foreach (var bp in templates)
        {
            var go = Instantiate(templateButtonPrefab, blueprintPanel);
            templateItems.Add(go);

            var txt = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (txt) txt.text = bp.displayName;

            var img = go.GetComponentInChildren<Image>(true);
            if (img && bp.icon) img.sprite = bp.icon;

            var btn = go.GetComponent<Button>();
            if (btn)
            {
                btn.onClick.AddListener(() =>
                {
                    // In Queue übernehmen (nur wenn noch gebunden)
                    if (boundFab != null) boundFab.EnqueueProduct(bp);
                });
            }
        }
    }

    private void OnQueueUpdated(ProductBlueprint current, float timeRemaining, IReadOnlyList<ProductBlueprint> queue)
    {
        if (queuePanel == null || queueItemPrefab == null) return;

        ClearQueue();

        // 1) Aktueller Auftrag
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

        // 2) Rest der Queue
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

    private void OnProductionStarted(ProductBlueprint bp)
    {
        // Optional: FX/Audio/Toast
    }

    private void OnProductionCompleted(ProductBlueprint bp, bool stored)
    {
        // Optional: Hinweis "eingelagert" oder "kein Platz – blockiert"
    }

    private void OnWaitingForResources(ProductBlueprint bp)
    {
        // Optional: Hinweis "fehlende Ressourcen"
    }

    // ───────────────────────── UI Helpers ─────────────────────────
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
}
