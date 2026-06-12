using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Liest Sondendaten für das Management-Overlay aus der aktiven Spielwelt.
/// </summary>
public static class ProbeManagementService
{
    public static ProbeManagementSnapshot GetSnapshot()
    {
        var snapshot = new ProbeManagementSnapshot();
        var probeRoot = ResolveProbeRoot();
        if (probeRoot == null)
            return snapshot;

        snapshot.HasProbe = true;
        FillIdentity(snapshot, probeRoot);
        FillStatus(snapshot, probeRoot);
        FillModules(snapshot, probeRoot);
        FillInventory(snapshot, probeRoot);
        FillInstalledEquipment(snapshot);
        FillFleet(snapshot);
        FillPower(snapshot, probeRoot);
        FillResearch(snapshot);
        return snapshot;
    }

    private static GameObject ResolveProbeRoot()
    {
        var hud = HUDBindingService.I?.SelectedItem;
        if (hud?.Transform != null)
            return hud.Transform.gameObject;

#if UNITY_2023_1_OR_NEWER
        var probe = Object.FindAnyObjectByType<ProbeController>(FindObjectsInactive.Include);
#else
        var probe = Object.FindObjectOfType<ProbeController>();
#endif
        return probe != null ? probe.gameObject : null;
    }

    private static void FillIdentity(ProbeManagementSnapshot snapshot, GameObject probeRoot)
    {
        var hubRegistry = ServiceContainer.Instance?.Get<HubRegistry>();
        if (hubRegistry != null)
        {
            foreach (var hub in hubRegistry.All())
            {
                if (hub.Kind == "Probe")
                {
                    snapshot.ProbeName = hub.DisplayName;
                    snapshot.ProbeId = hub.Id;
                    break;
                }
            }
        }

        var signature = probeRoot.GetComponentInChildren<ScanSignature>(true);
        if (signature != null && !string.IsNullOrWhiteSpace(signature.displayNameOverride))
            snapshot.ProbeName = signature.displayNameOverride;
    }

    private static void FillStatus(ProbeManagementSnapshot snapshot, GameObject probeRoot)
    {
        var flight = probeRoot.GetComponentInChildren<ProbeController>(true);
        if (flight != null)
            snapshot.Speed = flight.CurrentSpeed;

        var autopilot = probeRoot.GetComponentInChildren<ProbeAutopilot>(true);
        if (autopilot != null)
        {
            snapshot.AutopilotActive = autopilot.IsAutopilotActive;
            snapshot.NavTarget = autopilot.NavTarget != null ? autopilot.NavTarget.name : "—";
        }

        var miner = probeRoot.GetComponentInChildren<ProbeMiner>(true);
        if (miner != null)
        {
            snapshot.IsMining = miner.IsMining;
            snapshot.MiningMode = miner.CurrentMiningMode.ToString();
        }

        var inventory = probeRoot.GetComponentInChildren<InventoryController>(true);
        if (inventory != null)
        {
            snapshot.CargoUsed = inventory.UsedVolume;
            snapshot.CargoMax = inventory.maxVolume;
        }
    }

    private static void FillModules(ProbeManagementSnapshot snapshot, GameObject probeRoot)
    {
        AddModule(snapshot, "Manueller Flug", probeRoot.GetComponentInChildren<ProbeController>(true));
        if (HasActiveScanner(probeRoot))
        {
            var near = probeRoot.GetComponentInChildren<NearScannerController>(true);
            var far = probeRoot.GetComponentInChildren<FarScannerController>(true);
            var scanner = near != null ? near as Behaviour : far;
            AddModule(snapshot, "Scanner", scanner);
        }
        AddModule(snapshot, "Inventar", probeRoot.GetComponentInChildren<InventoryController>(true));
        AddModule(snapshot, "Navigation", probeRoot.GetComponentInChildren<ProbeAutopilot>(true));
        AddModule(snapshot, "Mining", probeRoot.GetComponentInChildren<ProbeMiner>(true));
        AddModule(snapshot, "Fabrikator", probeRoot.GetComponentInChildren<FabricatorController>(true));
    }

    private static bool HasActiveScanner(GameObject probeRoot) =>
        probeRoot.GetComponentInChildren<NearScannerController>(true) != null
        || probeRoot.GetComponentInChildren<FarScannerController>(true) != null;

    private static void AddModule(ProbeManagementSnapshot snapshot, string name, Behaviour component)
    {
        if (component == null) return;
        snapshot.Modules.Add(new ProbeModuleLine
        {
            Name = name,
            Active = component.isActiveAndEnabled,
            Detail = component.isActiveAndEnabled ? "Aktiv" : "Inaktiv"
        });
    }

    private static void FillInventory(ProbeManagementSnapshot snapshot, GameObject probeRoot)
    {
        var inventory = probeRoot.GetComponentInChildren<InventoryController>(true);
        if (inventory == null) return;

        snapshot.Inventory.Clear();
        snapshot.Inventory.AddRange(inventory.GetInventorySnapshot());
    }

    private static void FillInstalledEquipment(ProbeManagementSnapshot snapshot)
    {
        snapshot.InstalledEquipment.Clear();
        snapshot.InstalledEquipment.AddRange(InstalledEquipmentService.I.GetAll());
    }

    private static void FillFleet(ProbeManagementSnapshot snapshot)
    {
        snapshot.FleetAssets.Clear();
        snapshot.FleetAssets.AddRange(FleetRegistry.I.GetAll());
    }

    private static void FillPower(ProbeManagementSnapshot snapshot, GameObject probeRoot)
    {
        float generation = 120f + InstalledEquipmentService.I.GetBonusGenerationKw();
        float consumption = 8f;

        if (IsActive<ProbeMiner>(probeRoot)) consumption += 35f;
        if (IsActive<NearScannerController>(probeRoot) || IsActive<FarScannerController>(probeRoot)) consumption += 18f;
        if (IsActive<ProbeAutopilot>(probeRoot)) consumption += 12f;
        if (IsActive<FabricatorController>(probeRoot)) consumption += 25f;

        var fabricator = probeRoot.GetComponentInChildren<FabricatorController>(true);
        if (fabricator != null && fabricator.IsProducing)
            consumption += 40f;

        snapshot.Power.GenerationKw = generation;
        snapshot.Power.ConsumptionKw = consumption;
        snapshot.Power.StoragePercent = Mathf.Clamp01(0.45f + (generation - consumption) / 250f);
        snapshot.Power.PrimarySource = ScienceTreeService.I.IsResearched("heumann_reactor")
            ? "Heumann-Reaktor + Solar"
            : "Solar-Fabrik";
    }

    private static bool IsActive<T>(GameObject root) where T : Behaviour
    {
        var c = root.GetComponentInChildren<T>(true);
        return c != null && c.isActiveAndEnabled;
    }

    private static void FillResearch(ProbeManagementSnapshot snapshot)
    {
        snapshot.Research.TotalCount = ScienceTreeCatalog.All.Count;
        foreach (var tech in ScienceTreeCatalog.All)
        {
            if (ScienceTreeService.I.IsResearched(tech.Id))
                snapshot.Research.ResearchedCount++;
        }

        var activeId = ScienceTreeService.I.ActiveResearchId;
        if (string.IsNullOrEmpty(activeId)) return;

        var active = ScienceTreeCatalog.Get(activeId);
        snapshot.Research.HasActiveResearch = active != null;
        snapshot.Research.ActiveTitle = active?.Title ?? activeId;
        snapshot.Research.RemainingSeconds = ScienceTreeService.I.GetRemainingSeconds();
    }
}
