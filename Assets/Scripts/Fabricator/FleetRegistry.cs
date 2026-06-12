using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Übersicht autonomer Einheiten im Feld (In-Memory + WorldRegistry-Scan).
/// </summary>
public class FleetRegistry
{
    private static FleetRegistry _instance;
    public static FleetRegistry I => _instance ??= new FleetRegistry();

    public event Action Changed;

    private readonly List<FleetAssetEntry> _deployed = new();
    private int _deployCounter;

    public IReadOnlyList<FleetAssetEntry> GetAll()
    {
        SyncFromWorld();
        return _deployed;
    }

    public void DeployFromProduction(ProductBlueprint blueprint, Vector3 origin)
    {
        if (blueprint == null) return;

        _deployCounter++;
        var offset = UnityEngine.Random.insideUnitSphere * 80f;
        offset.y *= 0.2f;

        _deployed.Add(new FleetAssetEntry
        {
            Id = $"fleet_{blueprint.productId}_{_deployCounter}",
            DisplayName = blueprint.displayName,
            ProductId = blueprint.productId,
            Status = "Im Einsatz",
            Task = DefaultTask(blueprint.productId),
            LastPosition = origin + offset,
            Speed = DefaultSpeed(blueprint.productId)
        });

        Changed?.Invoke();
        GameEvents.PostHUDMessage($"{blueprint.displayName} im Feld eingesetzt.");
    }

    private void SyncFromWorld()
    {
        var world = ServiceContainer.Instance?.Get<WorldRegistry>();
        if (world == null) return;

        foreach (var entity in world.All)
        {
            if (entity == null) continue;
            var typeId = entity.TypeId ?? "";
            if (!IsFleetType(typeId)) continue;

            var payload = entity.GetHUDPayload();
            var id = entity.Guid.Value.ToString();
            var existing = _deployed.Find(e => e.WorldGuid == id);
            if (existing != null)
            {
                existing.LastPosition = payload.Position;
                existing.DisplayName = string.IsNullOrWhiteSpace(payload.Name) ? existing.DisplayName : payload.Name;
                continue;
            }

            _deployed.Add(new FleetAssetEntry
            {
                Id = id,
                WorldGuid = id,
                DisplayName = string.IsNullOrWhiteSpace(payload.Name) ? typeId : payload.Name,
                ProductId = typeId,
                Status = "Aktiv",
                Task = "Autonom",
                LastPosition = payload.Position,
                Speed = 0f
            });
        }
    }

    private static bool IsFleetType(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId)) return false;
        var lower = typeId.ToLowerInvariant();
        return lower.Contains("miner") || lower.Contains("drone") || lower.Contains("scout")
               || lower.Contains("courier") || lower.Contains("relay");
    }

    private static string DefaultTask(string productId) => productId switch
    {
        "miner_mk1" or "miner_mk2" => "Erzabbau im Gürtel",
        "scout_drone_mk1" => "Systemaufklärung",
        "courier_drone_mk1" => "Materialtransfer",
        "fab_drone_mk1" => "Vor-Ort-Produktion",
        "relay_sat_mk1" => "Relaisbetrieb",
        _ => "Autonomer Einsatz"
    };

    private static float DefaultSpeed(string productId) => productId switch
    {
        "scout_drone_mk1" => 24f,
        "courier_drone_mk1" => 12f,
        "miner_mk1" => 6f,
        "miner_mk2" => 8f,
        _ => 10f
    };
}

public sealed class FleetAssetEntry
{
    public string Id;
    public string WorldGuid;
    public string DisplayName;
    public string ProductId;
    public string Status;
    public string Task;
    public Vector3 LastPosition;
    public float Speed;
}
