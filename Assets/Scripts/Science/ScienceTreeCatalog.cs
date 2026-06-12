using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Beispiel-Forschungsbaum im Stil von Bobiverse / Von-Neumann-Sonden.
/// </summary>
public static class ScienceTreeCatalog
{
    public static IReadOnlyList<ScienceTechDefinition> All { get; } = Build();

    private static List<ScienceTechDefinition> Build()
    {
        return new List<ScienceTechDefinition>
        {
            Tech("von_neumann_core", "Von-Neumann-Kern", "Selbstreplizierende Sonde — Grundlage aller Bob-Technologie.",
                "Kern", 0, 0f),

            Tech("solar_fabricator", "Solar-Fabrik", "Solarenergie in Legierungen und Grundbauteile umwandeln.",
                "Produktion", 1, 45f, "von_neumann_core"),
            Tech("ore_scanner", "Erzscanner", "Spektrale Erzsignaturen im Nahscan auflösen.",
                "Sensorik", 1, 40f, "von_neumann_core"),
            Tech("basic_ion_drive", "Ionenantrieb I", "Sparsamer Antrieb für System-Innenraumflüge.",
                "Antrieb", 1, 50f, "von_neumann_core"),
            Tech("vr_simulation", "VR-Simulationskammer", "Missionsprofile und Manöver in virtueller Realität testen.",
                "Kern", 1, 35f, "von_neumann_core"),

            Tech("auto_mining", "Autonomer Abbau", "Sonde erntet Erz ohne Dauerüberwachung.",
                "Produktion", 2, 90f, "ore_scanner"),
            Tech("cargo_expansion", "Cargo-Optimierung", "Verdichtete Lagerung und schnellere Umladung.",
                "Produktion", 2, 75f, "solar_fabricator"),
            Tech("replication_matrix", "Replikationsmatrix", "Neue Von-Neumann-Sonde aus Rohmaterial replizieren.",
                "Kern", 2, 120f, "von_neumann_core", "solar_fabricator"),
            Tech("gamma_spectrometer", "Gamma-Spektrometer", "Seltene Isotope und Spurenelemente erkennen.",
                "Sensorik", 2, 80f, "ore_scanner"),
            Tech("nav_computer", "Navigationsrechner", "Präzise Bahnberechnung zwischen Himmelskörpern.",
                "Antrieb", 2, 70f, "basic_ion_drive"),

            Tech("heumann_reactor", "Heumann-Reaktor", "Kompakter Hochleistungsreaktor für Fabrikschiffe.",
                "Energie", 3, 180f, "replication_matrix", "cargo_expansion"),
            Tech("orbital_foundry", "Orbital-Fabrik", "Großstrukturen im Orbit statt an der Oberfläche bauen.",
                "Produktion", 3, 150f, "solar_fabricator", "replication_matrix"),
            Tech("hive_coordination", "Schwarm-Koordination", "Mehrere Sonden teilen Aufgaben und Scan-Daten.",
                "Kern", 3, 160f, "nav_computer", "replication_matrix"),
            Tech("antimatter_tap", "Antimaterie-Ausleitung", "Geringe AM-Mengen für Übersondenantriebe gewinnen.",
                "Antrieb", 3, 210f, "heumann_reactor"),
            Tech("dyson_segment", "Dyson-Segment", "Erstes Energie-Sammelmodul um einen Stern.",
                "Megastruktur", 3, 240f, "orbital_foundry", "heumann_reactor"),

            Tech("stellar_highway", "Sternenautobahn-Kartographie", "Gravitative Transitkorridore zwischen Systemen kartieren.",
                "Navigation", 4, 300f, "hive_coordination", "gamma_spectrometer"),
            Tech("gate_theory", "Portalkerntheorie", "Theoretische Basis für interstellare Portale (noch nicht baubar).",
                "Megastruktur", 4, 360f, "antimatter_tap", "dyson_segment"),
        };
    }

    private static ScienceTechDefinition Tech(
        string id,
        string title,
        string description,
        string branch,
        int tier,
        float durationSeconds,
        params string[] prerequisites)
    {
        return new ScienceTechDefinition
        {
            Id = id,
            Title = title,
            Description = description,
            Branch = branch,
            Tier = tier,
            DurationSeconds = durationSeconds,
            StartsUnlocked = durationSeconds <= 0f,
            Prerequisites = prerequisites ?? System.Array.Empty<string>()
        };
    }

    public static ScienceTechDefinition Get(string id) =>
        All.FirstOrDefault(t => t.Id == id);
}
