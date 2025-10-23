using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(InventoryController))]
public class FabricatorController : MonoBehaviour
{
    [Header("Fabricator")]
    public ProductBlueprint.FabricatorType fabricatorType;

    [Header("Katalog (optional, ersetzt Templates)")]
    [SerializeField] private FabricatorCatalog catalog; // optionaler Scriptable-Katalog (falls vorhanden)

    // Laufzeit-Status
    [SerializeField] private ProductBlueprint currentProduct;
    [SerializeField] private float timeRemaining;

    private readonly List<ProductBlueprint> queue = new();
    private readonly List<ProductBlueprint> queueMirror = new(); // nur für UI

    private InventoryController inv;

    /* ---------- Events für UI ---------- */
    public event Action<IReadOnlyList<ProductBlueprint>> TemplatesUpdated;
    public event Action<ProductBlueprint, float, IReadOnlyList<ProductBlueprint>> QueueUpdated;
    public event Action<ProductBlueprint> ProductionStarted;
    public event Action<ProductBlueprint, bool> ProductionCompleted;
    public event Action<ProductBlueprint> ProductionWaitingForResources;

    /* ---------- Initialisierung ---------- */
    private void Awake()
    {
        inv = GetComponent<InventoryController>();
    }

    private void Start()
    {
        RaiseTemplatesUpdated();
        RaiseQueueUpdated();
    }

    /* ---------- Zugriff auf Templates ---------- */
    // Ersetze die gesamte Property durch diese Version:
    private IReadOnlyList<ProductBlueprint> TemplatesOrCatalog
    {
        get
        {
            if (catalog != null)
            {
                var list = catalog.GetFor(fabricatorType); // List<ProductBlueprint> oder null
                if (list != null) return list;
            }
            return System.Array.Empty<ProductBlueprint>(); // ProductBlueprint[]
        }
    }


    /* ---------- Öffentliche API ---------- */

    /// <summary>Ein Blueprint hinten anstellen.</summary>
    public void Enqueue(ProductBlueprint bp)
    {
        if (bp == null) return;
        queue.Add(bp);
        SyncMirrorAndRaise();
        TryStartNextIfIdle();
    }

    /// <summary>Queue-Element an Position index löschen.</summary>
    public void RemoveAt(int index)
    {
        if (index < 0 || index >= queue.Count) return;

        // Wenn der Benutzer den aktuell laufenden Job löschen will, abbrechen und direkt weiter
        if (index == 0 && currentProduct != null && queue.Count > 0 && queue[0] == currentProduct)
        {
            // Abbruch → einfach als "nicht gespeichert" melden
            var aborted = currentProduct;
            currentProduct = null;
            timeRemaining = 0f;
            queue.RemoveAt(0);
            ProductionCompleted?.Invoke(aborted, false);
            SyncMirrorAndRaise();
            TryStartNextIfIdle();
            return;
        }

        queue.RemoveAt(index);
        SyncMirrorAndRaise();
    }

    /// <summary>Element von Position fromIndex auf toIndex verschieben.</summary>
    public void MoveItem(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= queue.Count) return;
        if (toIndex < 0) toIndex = 0;
        if (toIndex >= queue.Count) toIndex = queue.Count - 1;
        if (fromIndex == toIndex) return;

        var item = queue[fromIndex];
        queue.RemoveAt(fromIndex);
        queue.Insert(toIndex, item);
        SyncMirrorAndRaise();
    }

    /// <summary>Alle Jobs löschen (inkl. aktuell laufendem).</summary>
    public void ClearQueue()
    {
        var wasRunning = currentProduct != null;
        currentProduct = null;
        timeRemaining = 0f;
        queue.Clear();
        SyncMirrorAndRaise();
        if (wasRunning) ProductionCompleted?.Invoke(null, false);
    }

    public void ForceRefreshUI() => RaiseQueueUpdated();

    /* ---------- Produktionsschleife ---------- */
    private void Update()
    {
        if (currentProduct == null)
        {
            TryStartNextIfIdle();
            return;
        }

        // Laufender Job
        timeRemaining -= Time.deltaTime;
        if (timeRemaining < 0f) timeRemaining = 0f;
        RaiseQueueUpdated();

        if (timeRemaining <= 0f)
        {
            FinishCurrentAndStartNext();
        }
    }

    private void TryStartNextIfIdle()
    {
        if (currentProduct != null) return;
        if (queue.Count == 0) { RaiseQueueUpdated(); return; }

        // Nimm das erste Element der Queue
        currentProduct = queue[0];
        timeRemaining = Mathf.Max(0f, currentProduct.buildTime);

        // (optional) Ressourcenprüfung; wenn gewünscht, hier prüfen:
        // bool hasAll = InventoryHasCosts(currentProduct);
        // if (!hasAll) { ProductionWaitingForResources?.Invoke(currentProduct); return; }

        ProductionStarted?.Invoke(currentProduct);
        RaiseQueueUpdated();
    }

    private void FinishCurrentAndStartNext()
    {
        var finished = currentProduct;

        // Ergebnis "einlagern" oder spawnen; hier als Beispiel: Einlagern als ItemKey == productId
        if (inv != null)
        {
            if (inv.TryAddProduct(finished))
                ProductionCompleted?.Invoke(finished, true);
            else
                ProductionCompleted?.Invoke(finished, false);
        }
        else
        {
            ProductionCompleted?.Invoke(finished, false);
        }

        // Entferne das erste Queue-Element (das gerade fertig wurde)
        if (queue.Count > 0) queue.RemoveAt(0);

        currentProduct = null;
        timeRemaining = 0f;
        SyncMirrorAndRaise();

        // Weiter geht's mit dem nächsten
        TryStartNextIfIdle();
    }

    /* ---------- Hilfsfunktionen ---------- */
    private void SyncMirrorAndRaise()
    {
        queueMirror.Clear();
        queueMirror.AddRange(queue);
        RaiseQueueUpdated();
    }

    private void RaiseTemplatesUpdated()
    {
        ProductIndex.Register(TemplatesOrCatalog); // falls du eine zentrale Indexierung nutzt
        TemplatesUpdated?.Invoke(TemplatesOrCatalog);
    }

    private void RaiseQueueUpdated()
    {
        // (optional) Templates bei jedem Tick aktualisieren (falls Katalog dynamisch)
        TemplatesUpdated?.Invoke(TemplatesOrCatalog);
        QueueUpdated?.Invoke(currentProduct, timeRemaining, queueMirror);
    }

    // Beispiel: Ressourcenprüfung (optional – hier Dummy immer true)
    private bool InventoryHasCosts(ProductBlueprint bp) => true;

}
