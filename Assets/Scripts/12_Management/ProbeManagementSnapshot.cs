using System.Collections.Generic;

public sealed class ProbeManagementSnapshot
{
    public bool HasProbe;
    public string ProbeName = "—";
    public string ProbeId = "—";
    public float Speed;
    public string NavTarget = "—";
    public bool AutopilotActive;
    public bool IsMining;
    public string MiningMode = "—";
    public float CargoUsed;
    public float CargoMax;
    public List<ProbeModuleLine> Modules = new();
    public List<InventoryItemView> Inventory = new();
    public List<InstalledEquipmentEntry> InstalledEquipment = new();
    public List<FleetAssetEntry> FleetAssets = new();
    public ProbePowerSnapshot Power = new();
    public ProbeResearchSnapshot Research = new();
}

public sealed class ProbeModuleLine
{
    public string Name;
    public bool Active;
    public string Detail;
}

public sealed class ProbePowerSnapshot
{
    public float GenerationKw;
    public float ConsumptionKw;
    public float StoragePercent;
    public string PrimarySource = "Solar";
}

public sealed class ProbeResearchSnapshot
{
    public string ActiveTitle;
    public float RemainingSeconds;
    public bool HasActiveResearch;
    public int ResearchedCount;
    public int TotalCount;
}
