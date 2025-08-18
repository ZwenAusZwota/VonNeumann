using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(InventoryController))]
public class FabricatorController : MonoBehaviour
{
    [Header("Fabricator")]
    public ProductBlueprint.FabricatorType fabricatorType;

    [Header("State (read-only)")]
    [SerializeField] private ProductBlueprint currentProduct;
    [SerializeField] private float timeRemaining;
    [SerializeField] private List<ProductBlueprint> _queueDebugView = new(); // nur zur Inspektor-Ansicht

    private readonly Queue<ProductBlueprint> productionQueue = new();
    private InventoryController inventory; // Muss am selben GameObject hängen!

    /* ─────────────────────────────────────────────── Lifecycle ─────────────────────────────────────────────── */

    private void Awake()
    {
        inventory = GetComponent<InventoryController>();
        if (inventory == null)
        {
            Debug.LogError($"[{nameof(FabricatorController)}] Kein {nameof(InventoryController)} am selben GameObject gefunden. " +
                           $"Bitte einen {nameof(InventoryController)} auf '{gameObject.name}' hinzufügen.");
            enabled = false; // Hart stoppen, weil ohne Inventar das Konzept nicht funktioniert
            return;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Editor-Hilfe: sicherstellen, dass im Editor beim Hinzufügen alles korrekt ist
        if (inventory == null)
        {
            inventory = GetComponent<InventoryController>();
        }
    }
#endif

    private void Update()
    {
        if (!enabled) return;

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
        }
    }

    /* ─────────────────────────────────────────────── Public API ─────────────────────────────────────────────── */

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
        SyncDebugList();
        return true;
    }

    /// <summary>Bricht die aktuelle Produktion ab und gibt zuvor reservierte Ressourcen optional zurück (wenn du das so willst).</summary>
    public void AbortCurrent(bool refundResources = false)
    {
        if (currentProduct == null) return;

        if (refundResources)
        {
            RefundCosts(currentProduct);
        }

        currentProduct = null;
        timeRemaining = 0f;
    }

    /// <summary>Entfernt das erste Element aus der Queue (nicht die laufende Produktion).</summary>
    public ProductBlueprint DequeueFirstQueued()
    {
        if (productionQueue.Count == 0) return null;
        var p = productionQueue.Dequeue();
        SyncDebugList();
        return p;
    }

    /// <summary>Gibt eine Kopie der aktuellen Queue (nur lesend) zurück.</summary>
    public IReadOnlyCollection<ProductBlueprint> GetQueueSnapshot() => _queueDebugView.AsReadOnly();

    /* ─────────────────────────────────────────────── Internals ─────────────────────────────────────────────── */

    private void TryStartNextProduct()
    {
        if (productionQueue.Count == 0) return;

        var next = productionQueue.Peek();

        // 1) Ressourcenprüfung
        if (!inventory.HasResourcesFor(next))
        {
            // Kein Start: Queue bleibt stehen bis Spieler Ressourcen bringt.
            // Optional: Event/Tooltip/UI-Hinweis auslösen
            return;
        }

        // 2) Ressourcen abziehen (Reservierung)
        if (!inventory.ConsumeResources(next))
        {
            // Sollte wegen HasResourcesFor eigentlich nicht passieren – Fail-safe
            Debug.LogWarning($"[{nameof(FabricatorController)}] ConsumeResources fehlgeschlagen für '{next.displayName}'.");
            return;
        }

        currentProduct = next;
        timeRemaining = Mathf.Max(0f, next.buildTime);
    }

    private void TryCompleteProduct()
    {
        if (currentProduct == null) return;

        // Hier legst du fest, was "ins Inventar legen" bedeutet.
        // Variante A: Das Inventar verwaltet Produkte direkt (Stack/Slots auf Blueprint-Basis).
        // Variante B: Es wird eine deploybare Instanz in der Welt benötigt (Prefab).
        bool added = inventory.TryAddProduct(currentProduct);

        if (added)
        {
            productionQueue.Dequeue();
            currentProduct = null;
            timeRemaining = 0f;
            SyncDebugList();
        }
        else
        {
            // Kein Platz: blockiert – Spieler muss händisch Platz schaffen oder deployen.
            // currentProduct bleibt gesetzt, timeRemaining bleibt bei 0 => keine weitere Produktion.
            Debug.Log($"[{nameof(FabricatorController)}] Kein Platz im Inventar – '{currentProduct.displayName}' blockiert die Queue.");
        }
    }

    private void RefundCosts(ProductBlueprint product)
    {
        if (product == null) return;
        // Implementiere hier deine Rückerstattungslogik (ganz/teilweise, je nach Design).
        // z. B.: inventory.RefundResources(product);
    }

    private void SyncDebugList()
    {
        _queueDebugView.Clear();
        _queueDebugView.AddRange(productionQueue);
    }
}
