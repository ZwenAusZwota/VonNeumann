// Assets/Scripts/Common/MaterialDatabase.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public class MaterialDatabase : MonoBehaviour
{
    [Header("Quelle (optional)")]
    [Tooltip("Wenn leer, werden alle MaterialSO aus Resources/Materials geladen.")]
    [SerializeField] private List<MaterialSO> materials = new List<MaterialSO>();

    // --- Statischer Zustand ---
    private static readonly Dictionary<string, MaterialSO> byId = new Dictionary<string, MaterialSO>();
    private static MaterialSO[] weightedPool = Array.Empty<MaterialSO>();
    private static bool _initialized;
    private static Material _fallbackMat;

    // Sinnvoller Default, falls keine Daten vorhanden sind
    private const string DEFAULT_ID = "Iron";

    /// <summary>Alle registrierten Materialien (ReadOnly-View).</summary>
    public static IReadOnlyCollection<MaterialSO> All
    {
        get { Initialize(); return byId.Values; }
    }

    // ---------------------------------------------------------
    // Lebenszyklus
    // ---------------------------------------------------------
    private void Awake()
    {
        Debug.Log("Initializing Material Database...");
        Initialize();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Bei Änderungen im Editor sicher neu initialisieren
        _initialized = false;
        Initialize();
    }
#endif

    // ---------------------------------------------------------
    // Initialisierung / Aufbau von Lookup und Weighted-Pool
    // ---------------------------------------------------------
    // Ergänzungen in MaterialDatabase.cs

    public static void Initialize()
    {
        if (_initialized) return;

        var db = FindFirstObjectByType<MaterialDatabase>();
        List<MaterialSO> list;
        if (db != null && db.materials != null && db.materials.Count > 0)
            list = db.materials;
        else
            list = Resources.LoadAll<MaterialSO>("Materials").ToList();

        byId.Clear();
        int skipped = 0;
        foreach (var m in list)
        {
            if (m == null || string.IsNullOrWhiteSpace(m.id)) { skipped++; continue; }
            byId[m.id] = m;
        }

        var pool = new List<MaterialSO>();
        foreach (var m in byId.Values)
            for (int i = 0; i < Mathf.Max(1, m.weight); i++) pool.Add(m);
        weightedPool = pool.ToArray();

        if (list.Count == 0)
            Debug.LogWarning("[MaterialDatabase] Keine Materials gefunden (Resources/Materials oder Inspector-Liste leer).");
        if (skipped > 0)
            Debug.LogWarning($"[MaterialDatabase] {skipped} MaterialSO ohne ID übersprungen.");

        _initialized = true;
    }

    public static string GetRandomId()
    {
        Initialize();
        if (weightedPool == null || weightedPool.Length == 0)
        {
            Debug.LogWarning("[MaterialDatabase] weightedPool leer – liefere 'Iron' als Fallback.");
            return "Iron";
        }
        var pick = GetRandom();
        return string.IsNullOrWhiteSpace(pick.id) ? "Iron" : pick.id;
    }


    // ---------------------------------------------------------
    // Abfragen / API
    // ---------------------------------------------------------
    /// <summary>Prüft, ob eine Material-ID existiert.</summary>
    public static bool Has(string id)
    {
        Initialize();
        return !string.IsNullOrWhiteSpace(id) && byId.ContainsKey(id);
    }

    /// <summary>Versucht, ein Material zu holen (ohne Exception).</summary>
    public static bool TryGet(string id, out MaterialSO so)
    {
        Initialize();
        if (string.IsNullOrWhiteSpace(id))
        {
            so = null;
            return false;
        }
        return byId.TryGetValue(id, out so);
    }

    /// <summary>Holt ein Material oder wirft eine Exception, wenn die ID unbekannt ist.</summary>
    public static MaterialSO Get(string id)
    {
        Initialize();
        if (!byId.TryGetValue(id, out var so))
            throw new KeyNotFoundException($"[MaterialDatabase] Unbekannte Material-ID '{id}'.");
        return so;
    }

    /// <summary>Gibt ein zufälliges Material anhand der Gewichte zurück – oder null, wenn none.</summary>
    public static MaterialSO GetRandom()
    {
        Initialize();
        if (weightedPool == null || weightedPool.Length == 0)
            return null;

        int idx = UnityEngine.Random.Range(0, weightedPool.Length);
        return weightedPool[idx];
    }


    /// <summary>Optional: Zufall mit Filter (z. B. Metalle nur innen, Eis außen). Fällt auf Default-ID zurück.</summary>
    public static string GetRandomId(Func<MaterialSO, bool> predicate)
    {
        Initialize();
        if (predicate == null)
            return GetRandomId();

        // Leichte, aber robuste Implementierung: filtere aktuellen Pool ad hoc.
        // Bei Performancebedarf kannst du Pool-Caches pro Filterklasse anlegen.
        var filtered = weightedPool?.Where(predicate).ToArray();
        if (filtered == null || filtered.Length == 0)
            return DEFAULT_ID;

        var pick = filtered[UnityEngine.Random.Range(0, filtered.Length)];
        return string.IsNullOrWhiteSpace(pick.id) ? DEFAULT_ID : pick.id;
    }

    public static Material GetRenderMaterial(string id)
    {
        if (!TryGet(id, out var so) || so.renderMaterial == null)
        {
            Debug.LogWarning($"[MaterialDatabase] Kein Render-Material für '{id}'. Fallback grau.");
            return _fallbackMat ??= new Material(Shader.Find(
                GraphicsSettings.currentRenderPipeline == null
                    ? "Standard"
                    : "Universal Render Pipeline/Lit" // ggf. "HDRP/Lit"
            ));
        }
        return so.renderMaterial;
    }

#if UNITY_EDITOR
    // Kleines Test-Tool im Kontextmenü
    [ContextMenu("Test Random 10")]
    private void _TestRandom()
    {
        Initialize();
        for (int i = 0; i < 10; i++)
            Debug.Log($"[MaterialDatabase] Pick {i + 1}: {GetRandomId()}");
    }
#endif
}
