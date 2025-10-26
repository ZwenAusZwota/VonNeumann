// Assets/Scripts/Core/PlanetRegistry.cs
using System.Collections.Generic;
using UnityEngine;
using System;

public class PlanetRegistry : MonoBehaviour
{
    [System.Obsolete("Use ServiceContainer.Instance.Get<PlanetRegistry>() instead")]
    public static PlanetRegistry Instance { get; private set; }

    public Transform Star { get; private set; }
    public readonly List<Transform> Objects = new();      // nach Entfernung sortiert
    public Transform AsteroidBelt { get; private set; }

    /* ✨ NEU: komplette Navigations-Reihenfolge ----------------------- */
    public List<Transform> NavTargets
    {
        get
        {
            var list = new List<Transform>();
            if (Star) list.Add(Star);    // Index 0  → Numpad 0
            list.AddRange(Objects);               // Index 1-…→ Numpad 1-…
            if (AsteroidBelt) list.Add(AsteroidBelt);
            return list;
        }
    }

    /* --------------------------------------------------------------- */
    void Awake()
    {
        // Service Container Registrierung
        if (ServiceContainer.Instance != null)
        {
            ServiceContainer.Instance.RegisterSingleton<PlanetRegistry>(this);
        }

        // Singleton-Absicherung (für Rückwärtskompatibilität)
        var existingInstance = GetExistingInstance();
        if (existingInstance != null && existingInstance != this) 
        { 
            Destroy(gameObject); 
            return; 
        }
        SetInstance(this);
        DontDestroyOnLoad(gameObject);
    }

    /* ---------- Registrieren ---------- */
    public void RegisterStar(Transform star)
    {
        Star = star;
        if (Objects.Count > 0) SortObjects();
    }

    public void RegisterPlanet(Transform planet)
    {
        Objects.Add(planet);
        if (Star != null) SortObjects();
    }

    // public void RegisterAsteroidBelt(Transform belt) => AsteroidBelt = belt;
    public void RegisterAsteroidBelt(Transform belt)
    {
        Objects.Add( belt);
        if (Star != null) SortObjects();
    }

    /* ---------- Hilfs-Sortierung ---------- */
    void SortObjects() =>
        Objects.Sort((a, b) =>
            (a.position - Star.position).sqrMagnitude
            .CompareTo((b.position - Star.position).sqrMagnitude));

    // Assets/Scripts/Core/PlanetRegistry.cs
    public void RegisterProbe(Transform probe)
    {
        Objects.Add(probe);
        if (Star != null) SortObjects();
    }

    // Hilfsmethoden zur Vermeidung der Warnung
    private static PlanetRegistry GetExistingInstance()
    {
        return ServiceContainer.Instance?.Get<PlanetRegistry>();
    }

    private static void SetInstance(PlanetRegistry instance)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        Instance = instance;
#pragma warning restore CS0618
    }
}
