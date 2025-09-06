using UnityEngine;

public class WorldRoot : MonoBehaviour
{
    public static WorldRoot Instance { get; private set; }

    [Header("Buckets")]
    public Transform starRoot;
    public Transform planetsRoot;
    public Transform beltsRoot;

    public enum Category { Star, Planet, Belt }

    void Awake()
    {
        // Singleton-Absicherung
        if (Instance != null && Instance != this)
        {
            // Optional: Kinder in bestehende Instance migrieren
            MoveChildren(transform, Instance.transform);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Buckets sicherstellen
        if (!starRoot) starRoot = EnsureChild("Stars");
        if (!planetsRoot) planetsRoot = EnsureChild("Planets");
        if (!beltsRoot) beltsRoot = EnsureChild("Belts");
    }

    static Transform EnsureChild(string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(Instance.transform, false);
        return t;
    }

    static void MoveChildren(Transform from, Transform to)
    {
        var tmp = new Transform[from.childCount];
        for (int i = 0; i < tmp.Length; i++) tmp[i] = from.GetChild(i);
        foreach (var c in tmp) c.SetParent(to, true);
    }

    public void Attach(Transform t, Category cat, bool worldPos = true)
    {
        var bucket = cat == Category.Star ? starRoot :
                     cat == Category.Planet ? planetsRoot : beltsRoot;
        t.SetParent(bucket, worldPos);
    }

    // Optionaler Helfer fürs Bootstrap:
    public static void Ensure()
    {
        if (Instance) return;
        var existing = FindFirstObjectByType<WorldRoot>();
        if (existing) { Instance = existing; return; }

        var go = new GameObject("World");
        go.AddComponent<WorldRoot>();
    }
}
