using UnityEngine;
using System;

public class WorldRoot : MonoBehaviour
{
    [System.Obsolete("Use ServiceContainer.Instance.Get<WorldRoot>() instead")]
    public static WorldRoot Instance { get; private set; }

    [Header("Buckets")]
    public Transform starRoot;
    public Transform planetsRoot;
    public Transform beltsRoot;

    public enum Category { Star, Planet, Belt }

    void Awake()
    {
        // Service Container Registrierung
        if (ServiceContainer.Instance != null)
        {
            ServiceContainer.Instance.RegisterSingleton<WorldRoot>(this);
        }

        // Singleton-Absicherung (für Rückwärtskompatibilität)
        var existingInstance = GetExistingInstance();
        if (existingInstance != null && existingInstance != this)
        {
            // Optional: Kinder in bestehende Instance migrieren
            MoveChildren(transform, existingInstance.transform);
            Destroy(gameObject);
            return;
        }
        SetInstance(this);
        DontDestroyOnLoad(gameObject);

        // Buckets sicherstellen
        if (!starRoot) starRoot = EnsureChild("Stars");
        if (!planetsRoot) planetsRoot = EnsureChild("Planets");
        if (!beltsRoot) beltsRoot = EnsureChild("Belts");
    }

    static Transform EnsureChild(string name)
    {
        var instance = GetInstance();
        if (instance == null) return null;
        var t = new GameObject(name).transform;
        t.SetParent(instance.transform, false);
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

    // Optionaler Helfer frs Bootstrap:
    public static void Ensure()
    {
        if (GetInstance() != null) return;
        var existing = FindFirstObjectByType<WorldRoot>();
        if (existing != null) 
        { 
            SetInstance(existing); 
            return; 
        }

        var go = new GameObject("World");
        go.AddComponent<WorldRoot>();
    }

    // Hilfsmethoden zur Vermeidung der Warnung
    private static WorldRoot GetExistingInstance()
    {
        return ServiceContainer.Instance?.Get<WorldRoot>();
    }

    private static WorldRoot GetInstance()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return Instance;
#pragma warning restore CS0618
    }

    private static void SetInstance(WorldRoot instance)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        Instance = instance;
#pragma warning restore CS0618
    }
}
