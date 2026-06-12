using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Beispiel-Baupläne für den Sonden-Fabrikator (Runtime + vorhandene ScriptableObjects).
/// </summary>
public static class FabricatorBlueprintRegistry
{
    private static bool _initialized;
    private static readonly List<ProductBlueprint> _all = new();

    public static IReadOnlyList<ProductBlueprint> GetFor(ProductBlueprint.FabricatorType type)
    {
        EnsureInitialized();
        return _all.Where(p => p != null && p.allowedFabricators != null && p.allowedFabricators.Contains(type)).ToList();
    }

    public static ProductBlueprint Get(string productId)
    {
        EnsureInitialized();
        return _all.FirstOrDefault(p => p.productId == productId);
    }

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        var minerAsset = Resources.Load<ProductBlueprint>("Fabricator/MinerMK1");
        if (minerAsset != null)
        {
            minerAsset.category = ProductBlueprint.ProductCategory.ExternalUnit;
            _all.Add(minerAsset);
        }

        Bp("cargo_expansion_mk1", "Lagererweiterung MK1", ProductBlueprint.ProductCategory.Equipment,
            "Internes Modul: +100 m³ Frachtraum. Wird direkt in die Sonde eingebaut.",
            45f, ProductBlueprint.FabricatorType.Probe);
        Bp("nuclear_battery_mk1", "Nuklearbatterie MK1", ProductBlueprint.ProductCategory.Equipment,
            "Kompakter Heumann-Zellenverbund für autarke Tiefraum-Einsätze. Erhöht die Energiebilanz deutlich.",
            90f, ProductBlueprint.FabricatorType.Probe);
        Bp("advanced_scanner_mk1", "Erweiterter Scanner", ProductBlueprint.ProductCategory.Equipment,
            "Multispektral-Sensorpaket für präzisere Nah- und Fernscans.",
            60f, ProductBlueprint.FabricatorType.Probe);
        Bp("ion_drive_mk2", "Ionenantrieb MK2", ProductBlueprint.ProductCategory.Equipment,
            "Verbesserter Antriebsblock mit geringerem Verbrauch und höherer Schubkraft.",
            75f, ProductBlueprint.FabricatorType.Probe);
        Bp("vr_core_mk1", "VR-Kernmodul", ProductBlueprint.ProductCategory.Equipment,
            "Simulationskern für Missionsplanung und autonome Manöverproben in Bobiverse-Manier.",
            55f, ProductBlueprint.FabricatorType.Probe);

        Bp("miner_mk1", "Miner MK1", ProductBlueprint.ProductCategory.ExternalUnit,
            "Kleine autonome Miningsonde mit Solarversorgung, Ionenantrieb und begrenztem Lager.",
            120f, ProductBlueprint.FabricatorType.Probe);
        Bp("miner_mk2", "Miner MK2", ProductBlueprint.ProductCategory.ExternalUnit,
            "Verbesserter Miner mit größerem Erzspeicher und effizienterem Strahlabbau.",
            180f, ProductBlueprint.FabricatorType.Probe);
        Bp("scout_drone_mk1", "Aufklärer MK1", ProductBlueprint.ProductCategory.ExternalUnit,
            "Schnelle Erkundungsdrohne für System-Scans und Zielmarkierung.",
            70f, ProductBlueprint.FabricatorType.Probe);
        Bp("courier_drone_mk1", "Kurier MK1", ProductBlueprint.ProductCategory.ExternalUnit,
            "Frachtdrohne für Materialtransfer zwischen Sonde, Minern und Orbital-Hubs.",
            95f, ProductBlueprint.FabricatorType.Probe);
        Bp("fab_drone_mk1", "Fabrik-Drohne MK1", ProductBlueprint.ProductCategory.ExternalUnit,
            "Mobile Mikrofabrik für Vor-Ort-Produktion in entlegenen Gürteln.",
            150f, ProductBlueprint.FabricatorType.Probe);
        Bp("relay_sat_mk1", "Relais-Satellit MK1", ProductBlueprint.ProductCategory.ExternalUnit,
            "Kleiner Kommunikationssatellit für erweiterte Schwarm-Koordination.",
            80f, ProductBlueprint.FabricatorType.Probe);

        ProductIndex.Register(_all);
    }

    private static ProductBlueprint Bp(
        string id,
        string title,
        ProductBlueprint.ProductCategory category,
        string description,
        float buildTime,
        ProductBlueprint.FabricatorType fabricator)
    {
        if (_all.Any(p => p.productId == id))
            return _all.First(p => p.productId == id);

        var bp = ScriptableObject.CreateInstance<ProductBlueprint>();
        bp.productId = id;
        bp.displayName = title;
        bp.category = category;
        bp.description = description;
        bp.buildTime = buildTime;
        bp.allowedFabricators = new List<ProductBlueprint.FabricatorType> { fabricator };
        bp.resourceCosts = new List<ProductBlueprint.ResourceCost>();
        bp.componentCosts = new List<ProductBlueprint.ComponentCost>();
        _all.Add(bp);
        return bp;
    }
}
