using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Verwaltet in die Sonde eingebaute Ausrüstung (In-Memory).
/// </summary>
public class InstalledEquipmentService
{
    private static InstalledEquipmentService _instance;
    public static InstalledEquipmentService I => _instance ??= new InstalledEquipmentService();

    public event Action Changed;

    private readonly List<InstalledEquipmentEntry> _installed = new();

    public IReadOnlyList<InstalledEquipmentEntry> GetAll() => _installed;

    public bool IsInstalled(string productId) =>
        _installed.Exists(e => e.ProductId == productId);

    public bool TryInstall(ProductBlueprint blueprint, GameObject probeRoot)
    {
        if (blueprint == null || probeRoot == null) return false;
        if (blueprint.category != ProductBlueprint.ProductCategory.Equipment) return false;
        if (IsInstalled(blueprint.productId)) return false;

        _installed.Add(new InstalledEquipmentEntry
        {
            ProductId = blueprint.productId,
            DisplayName = blueprint.displayName,
            Description = blueprint.description
        });

        ApplyEffect(blueprint, probeRoot);
        Changed?.Invoke();
        GameEvents.PostHUDMessage($"Modul eingebaut: {blueprint.displayName}");
        return true;
    }

    private static void ApplyEffect(ProductBlueprint blueprint, GameObject probeRoot)
    {
        var inventory = probeRoot.GetComponentInChildren<InventoryController>(true);
        switch (blueprint.productId)
        {
            case "cargo_expansion_mk1":
                if (inventory != null)
                {
                    inventory.maxVolume += 100f;
                    inventory.ForceRefreshUI();
                }
                break;
        }
    }

    public float GetBonusGenerationKw()
    {
        float bonus = 0f;
        foreach (var entry in _installed)
        {
            if (entry.ProductId == "nuclear_battery_mk1") bonus += 80f;
        }
        return bonus;
    }
}

public sealed class InstalledEquipmentEntry
{
    public string ProductId;
    public string DisplayName;
    public string Description;
}
