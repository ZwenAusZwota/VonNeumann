using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(InventoryController))]
public class FabricatorController : MonoBehaviour
{
    [Header("Fabricator")]
    public ProductBlueprint.FabricatorType fabricatorType;

    [Header("Katalog (optional, ersetzt Templates)")]
    [SerializeField] private FabricatorCatalog catalog;

    // Queue & Laufzustand
    [SerializeField] private ProductBlueprint currentProduct;
    [SerializeField] private float timeRemaining;

    private readonly Queue<ProductBlueprint> productionQueue = new();
    private readonly List<ProductBlueprint> queueMirror = new(); // nur für Events/Snapshots
    private InventoryController inventory;

    // ───────────────────────── Events ─────────────────────────
    /// <summary>Alle verfügbaren Templates (aus Katalog, gefiltert nach Fabrikator-Typ).</summary>
    public event Action<IReadOnlyList<ProductBlueprint>> TemplatesUpdated;

    /// <summary>Änderungen an der Fertigungs-Warteschlange (inkl. aktuellem Auftrag & Restzeit).</summary>
    public event Action<ProductBlueprint, float, IReadOnlyList<ProductBlueprint>> QueueUpdated;

    /// <summary>Wird ausgelöst, wenn ein Auftrag fertig ist (true=erfolgreich eingelagert / false=blockiert mangels Platz).</summary>
    public event Action<ProductBlueprint, bool> ProductionCompleted;

    /// <summary>Wird ausgelöst, wenn eine Produktion startet.</summary>
    public event Action<ProductBlueprint> ProductionStarted;

    /// <summary>Wird ausgelöst, wenn das Starten einer Produktion an Ressourcen scheitert.</summary>
    public event Action<ProductBlueprint> ProductionWaitingForResources;

    // Öffentliche Sicht auf Templates (Katalog kann fehlen)
    public IReadOnlyList<ProductBlueprint> TemplatesOrCatalog
    {
        get
        {
            if (catalog == null) return Array.Empty<ProductBlueprint>();
            return catalog.GetFor(fabricatorType);
        }
    }


    private void Awake()
    {
        inventory = GetComponent<InventoryController>();
        if (inventory == null)
        {
            Debug.LogError($"[{nameof(FabricatorController)}] Kein {nameof(InventoryController)} am selben GameObject.");
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        // Beim Start sofort den aktuellen Template-Katalog publizieren
        RaiseTemplatesUpdated();
        RaiseQueueUpdated();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (inventory == null) inventory = GetComponent<InventoryController>();
    }
#endif

    private void Update()
    {
        if (!enabled) return;

        // Laufende Produktion fortschreiben
        if (currentProduct == null && productionQueue.Count > 0)
        {
            TryStartNextProduct();
        }

        if (currentProduct != null && timeRemaining > 0f)
        {
            timeRemaining -= Time.deltaTime;
            if (timeRemaining <= 0f)
            {
                TryCompleteProduct();
            }
            else
            {
                // periodisch Fortschritt pushen (leichtes Throttling optional)
                RaiseQueueUpdated();
            }
        }
    }

    // ───────────────────────── Public API ─────────────────────────

    /// <summary>Fügt ein Produkt der Produktions-Queue hinzu (nur wenn dieser Fabrikator-Typ es bauen darf).</summary>
    public bool EnqueueProduct(ProductBlueprint product)
    {
        if (product == null)
        {
            Debug.LogWarning($"[{nameof(FabricatorController)}] EnqueueProduct: product == null");
            return false;
        }
        if (!product.allowedFabricators.Contains(fabricatorType))
        {
            Debug.LogWarning($"[{nameof(FabricatorController)}] {fabricatorType} darf '{product.displayName}' nicht produzieren.");
            return false;
        }

        productionQueue.Enqueue(product);
        MirrorQueue();
        RaiseQueueUpdated();
        return true;
    }

    /// <summary>Entfernt das erste Element aus der Queue (nicht die laufende Produktion).</summary>
    public ProductBlueprint DequeueFirstQueued()
    {
        if (productionQueue.Count == 0) return null;
        var p = productionQueue.Dequeue();
        MirrorQueue();
        RaiseQueueUpdated();
        return p;
    }

    /// <summary>Bricht die aktuelle Produktion ab. Optional: Ressourcen zurückerstatten.</summary>
    public void AbortCurrent(bool refundResources = false)
    {
        if (currentProduct == null) return;

        if (refundResources) inventory.RefundResources(currentProduct);

        currentProduct = null;
        timeRemaining = 0f;
        RaiseQueueUpdated();
    }

    /// <summary>Erzwingt das erneute Senden der Events (z. B. wenn UI neu verbunden wird).</summary>
    public void ForceRefreshUI()
    {
        RaiseTemplatesUpdated();
        RaiseQueueUpdated();
    }

    // ───────────────────────── Internals ─────────────────────────

    private void TryStartNextProduct()
    {
        if (productionQueue.Count == 0) return;

        var next = productionQueue.Peek();

        // Ressourcenprüfung
        if (!inventory.HasResourcesFor(next))
        {
            ProductionWaitingForResources?.Invoke(next);
            return;
        }

        // Ressourcen abziehen (Reservierung)
        if (!inventory.ConsumeResources(next))
        {
            Debug.LogWarning($"[{nameof(FabricatorController)}] ConsumeResources fehlgeschlagen für '{next.displayName}'.");
            return;
        }

        currentProduct = next;
        timeRemaining = Mathf.Max(0f, next.buildTime);
        ProductionStarted?.Invoke(currentProduct);
        RaiseQueueUpdated();
    }

    private void TryCompleteProduct()
    {
        if (currentProduct == null) return;

        bool added = inventory.TryAddProduct(currentProduct);
        if (added)
        {
            // Erfolg: vom Queue-Kopf entfernen
            productionQueue.Dequeue();
            currentProduct = null;
            timeRemaining = 0f;

            MirrorQueue();
            RaiseQueueUpdated();
            ProductionCompleted?.Invoke(currentProduct, true);
        }
        else
        {
            // Kein Platz: Job bleibt „fertig“ liegen und blockiert
            timeRemaining = 0f; // bleibt 0 – blockiert weitere Starts
            RaiseQueueUpdated();
            ProductionCompleted?.Invoke(currentProduct, false);
        }
    }

    private void MirrorQueue()
    {
        queueMirror.Clear();
        foreach (var p in productionQueue) queueMirror.Add(p);
    }

    private void RaiseTemplatesUpdated()
    {
        ProductIndex.Register(TemplatesOrCatalog);
        TemplatesUpdated?.Invoke(TemplatesOrCatalog);
    }

    private void RaiseQueueUpdated()
    {
        TemplatesUpdated?.Invoke(TemplatesOrCatalog); // optional: Templates bei jedem Tick aktualisieren (falls Katalog dynamisch)
        TemplatesUpdated?.Invoke(TemplatesOrCatalog);
        QueueUpdated?.Invoke(currentProduct, timeRemaining, queueMirror);
    }
}
