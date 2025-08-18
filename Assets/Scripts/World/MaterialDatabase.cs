using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class MaterialDatabase : MonoBehaviour
{

    [Header("Quelle")]
    [Tooltip("Falls leer, werden alle Materials aus Resources/Materials geladen.")]
    public List<MaterialSO> materials = new();

    // Lookup-Tabellen
    private static readonly Dictionary<string, MaterialSO> byId = new();
    private static MaterialSO[] weightedPool;

    private static bool _initialized;

    private void Awake()
    {
        Initialize();
    }

    public static void Initialize()
    {
        if (_initialized) return;

        var db = FindFirstObjectByType<MaterialDatabase>();
        List<MaterialSO> list;
        if (db != null && db.materials != null && db.materials.Count > 0)
        {
            list = db.materials;
        }
        else
        {
            // Fallback: alles aus Resources/Materials laden
            list = Resources.LoadAll<MaterialSO>("Materials").ToList();
        }

        byId.Clear();
        foreach (var m in list)
        {
            if (m == null || string.IsNullOrWhiteSpace(m.id)) continue;
            byId[m.id] = m;
        }

        // Weighted-Pool vorbereiten
        var pool = new List<MaterialSO>();
        foreach (var m in byId.Values)
        {
            var w = Mathf.Max(1, m.weight);
            for (int i = 0; i < w; i++) pool.Add(m);
        }
        weightedPool = pool.ToArray();

        _initialized = true;
    }

    public static MaterialSO Get(string id)
    {
        Initialize();
        return byId[id];
    }

    public static MaterialSO GetRandom()
    {
        Initialize();
        return weightedPool[Random.Range(0, weightedPool.Length)];
    }

    public static string GetRandomId() => GetRandom().id;
}
