using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FabricatorBay : MonoBehaviour
{
    [Tooltip("Welche Blueprints können hier gefertigt werden?")]
    public List<ProductBlueprint> unlocked = new();

    [Tooltip("Max. Distanz, damit die Sonde als 'in Reichweite' gilt")]
    public float commRange = 50f;

    public Transform spawnPoint;                               // Wo fertige Objekte entstehen
    public event Action<ProductBlueprint, float> OnJobQueued;    // UI-Hook
    public event Action<ProductBlueprint> OnJobFinished;

    readonly Queue<(ProductBlueprint, float end)> queue = new();

    ProbeController probe;     // wird z.B. im Start() gesucht (FindWithTag)

    /* ---------- Public API ---------- */
    public bool CanInteract() => probe && Vector3.Distance(probe.transform.position, transform.position) <= commRange;

    /*public bool HasResources(ProductBlueprint bp) =>
        bp.buildCost.All(c => probe.Player.Cargo.Get(c.res) >= c.amt);*/

    public void QueueBuild(ProductBlueprint bp)
    {
        //if (!CanInteract() || !HasResources(bp)) return;
        //foreach (var c in bp.buildCost) probe.Player.Cargo.Sub(c.res, c.amt);

        float endTime = Time.time + bp.buildTime;
        queue.Enqueue((bp, endTime));
        OnJobQueued?.Invoke(bp, endTime);
    }

    public bool TryUpgrade(UpgradableProduct target)
    {
        var src = target.Blueprint;
        if (!src.upgradeable || !src.successor) return false;
        //if (!CanInteract() || !src.upgradeCost.All(c => probe.Player.Cargo.Get(c.res) >= c.amt)) return false;

        //foreach (var c in src.upgradeCost) probe.Player.Cargo.Sub(c.res, c.amt);

        // Direktes Upgrade → altes Objekt entfernen, neues instanziieren
        Instantiate(src.successor.prefab, target.transform.position, target.transform.rotation);
        Destroy(target.gameObject);
        return true;
    }

    /* ---------- Loop ---------- */
    void Update()
    {
        if (queue.Count == 0) return;

        var (bp, end) = queue.Peek();
        if (Time.time < end) return;

        Instantiate(bp.prefab, spawnPoint.position, spawnPoint.rotation);
        OnJobFinished?.Invoke(bp);
        queue.Dequeue();
    }
}
