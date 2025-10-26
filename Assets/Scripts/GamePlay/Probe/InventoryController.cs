// Assets/Scripts/Probe/InventoryController.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Volumenbasiertes Inventar – **headless** (keine direkte UI).
/// Meldet Änderungen via Events an Panels.
/// </summary>
public class InventoryController : MonoBehaviour
{
    [Header("Capacity")]
    [Tooltip("Maximales Füllvolumen in m³")]
    public float maxVolume = 500f;

    private float usedVolume;

    // Lager
    private readonly Dictionary<MaterialSO, float> store = new();     // Material → Einheiten (float)
    private readonly Dictionary<string, int> productStore = new();    // productId → Stückzahl (int)

    // Events
    /// <summary>Füllstand geändert (used, max).</summary>
    public event Action<float, float> CargoChanged;

    /// <summary>Kompletter Inventar-Snapshot für UI.</summary>
    public event Action<IReadOnlyList<InventoryItemView>> InventoryUpdated;

    public float UsedVolume => usedVolume;
    public float FreeVolume => Mathf.Max(0f, maxVolume - usedVolume);

    private void Start()
    {
        ForceRefreshUI();
    }

    /// <summary>Panels können initialen Stand abholen.</summary>
    public void ForceRefreshUI()
    {
        InventoryUpdated?.Invoke(BuildSnapshot());
        CargoChanged?.Invoke(usedVolume, maxVolume);
    }

    // ───────────── Materialien ─────────────

    public float GetMaterialUnits(MaterialSO material)
        => material != null && store.TryGetValue(material, out var v) ? v : 0f;

    public float GetMaterialUnits(string materialId)
        => GetMaterialUnits(MaterialDatabase.Get(materialId));

    /// <summary>Material einlagern; Rückgabe: tatsächlich eingelagerte Einheiten.</summary>
    public float Add(MaterialSO material, float units)
    {
        if (material == null || units <= 0f) return 0f;

        float reqVol = units * material.volumePerUnit;
        if (reqVol > FreeVolume) units = FreeVolume / material.volumePerUnit;
        if (units <= 0f) return 0f;

        usedVolume += units * material.volumePerUnit;
        store[material] = store.TryGetValue(material, out var cur) ? cur + units : units;

        CargoChanged?.Invoke(usedVolume, maxVolume);
        InventoryUpdated?.Invoke(BuildSnapshot());
        return units;
    }

    public float Add(string materialId, float units) => Add(MaterialDatabase.Get(materialId), units);

    /// <summary>Material entnehmen; Rückgabe: tatsächlich entnommene Einheiten.</summary>
    public float Remove(MaterialSO material, float units)
    {
        if (material == null || !store.TryGetValue(material, out var have) || have <= 0f || units <= 0f)
            return 0f;

        float take = Mathf.Min(units, have);
        store[material] = have - take;

        usedVolume -= take * material.volumePerUnit;
        if (usedVolume < 0f) usedVolume = 0f;

        CargoChanged?.Invoke(usedVolume, maxVolume);
        InventoryUpdated?.Invoke(BuildSnapshot());
        return take;
    }

    public float Remove(string materialId, float units) => Remove(MaterialDatabase.Get(materialId), units);

    // ───────────── Produkte ─────────────

    public int GetProductCount(string productId)
        => productStore.TryGetValue(productId, out var n) ? n : 0;

    public int RemoveProduct(string productId, int amount, float assumedVolumePerUnit = 0f)
    {
        if (amount <= 0) return 0;
        if (!productStore.TryGetValue(productId, out var have) || have <= 0) return 0;

        int take = Mathf.Min(have, amount);
        productStore[productId] = have - take;

        if (assumedVolumePerUnit > 0f)
        {
            usedVolume -= take * assumedVolumePerUnit;
            if (usedVolume < 0f) usedVolume = 0f;
            CargoChanged?.Invoke(usedVolume, maxVolume);
        }

        InventoryUpdated?.Invoke(BuildSnapshot());
        return take;
    }

    public bool TryAddProduct(ProductBlueprint blueprint)
    {
        if (blueprint == null) return false;

        float vol = EstimateProductVolume(blueprint);
        if (vol > FreeVolume) return false;

        usedVolume += vol;
        productStore[blueprint.productId] = GetProductCount(blueprint.productId) + 1;

        CargoChanged?.Invoke(usedVolume, maxVolume);
        InventoryUpdated?.Invoke(BuildSnapshot());
        return true;
    }

    public void RefundResources(ProductBlueprint blueprint)
    {
        if (blueprint == null) return;

        foreach (var rc in blueprint.resourceCosts)
        {
            if (rc.resource == null || rc.amount <= 0) continue;
            Add(rc.resource, rc.amount);
        }

        foreach (var cc in blueprint.componentCosts)
        {
            if (cc.product == null || cc.amount <= 0) continue;
            float v = EstimateProductVolume(cc.product);
            for (int i = 0; i < cc.amount; i++)
            {
                if (v <= FreeVolume)
                {
                    usedVolume += v;
                    productStore[cc.product.productId] = GetProductCount(cc.product.productId) + 1;
                }
            }
        }

        CargoChanged?.Invoke(usedVolume, maxVolume);
        InventoryUpdated?.Invoke(BuildSnapshot());
    }

    // ───────────── Fabricator-Kompatibilität ─────────────

    public bool HasResourcesFor(ProductBlueprint bp)
    {
        if (bp == null) return false;

        foreach (var rc in bp.resourceCosts)
            if (rc.resource == null || GetMaterialUnits(rc.resource) < rc.amount) return false;

        foreach (var cc in bp.componentCosts)
            if (cc.product == null || GetProductCount(cc.product.productId) < cc.amount) return false;

        return true;
    }

    public bool ConsumeResources(ProductBlueprint bp)
    {
        if (!HasResourcesFor(bp)) return false;

        foreach (var rc in bp.resourceCosts)
            if (Remove(rc.resource, rc.amount) < rc.amount) return false;

        foreach (var cc in bp.componentCosts)
        {
            float volPerUnit = EstimateProductVolume(cc.product);
            if (RemoveProduct(cc.product.productId, cc.amount, volPerUnit) < cc.amount) return false;
        }
        return true;
    }

    // ───────────── Helpers ─────────────

    private float EstimateProductVolume(ProductBlueprint bp)
    {
        if (bp == null || bp.resourceCosts == null) return 0f;
        float vol = 0f;
        foreach (var rc in bp.resourceCosts)
            if (rc.resource != null) vol += rc.amount * rc.resource.volumePerUnit;
        return vol;
    }

    private List<InventoryItemView> BuildSnapshot()
    {
        var list = new List<InventoryItemView>(store.Count + productStore.Count);

        // Materialien
        foreach (var kv in store)
        {
            var mat = kv.Key;
            float units = kv.Value;
            if (units <= 0f || mat == null) continue;

            list.Add(new InventoryItemView
            {
                id = mat.id,
                displayName = mat.displayName,
                amount = Mathf.FloorToInt(units),
                icon = mat.icon,
                isProduct = false
            });
        }

        // Produkte mit hübschem Namen/Icon
        foreach (var kv in productStore)
        {
            var pid = kv.Key;
            var count = kv.Value;
            if (count <= 0) continue;

            var bp = ProductIndex.Get(pid);
            list.Add(new InventoryItemView
            {
                id = pid,
                displayName = bp != null ? bp.displayName : pid,
                amount = count,
                icon = bp != null ? bp.icon : null,
                isProduct = true
            });
        }


        return list;
    }
}

public struct InventoryItemView
{
    public string id;
    public string displayName;
    public int amount;
    public Sprite icon;
    public bool isProduct;
}
