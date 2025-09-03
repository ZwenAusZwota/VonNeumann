// Assets/Scripts/Core/PlanetRegistry.cs
using System.Collections.Generic;
using UnityEngine;

public class PlanetRegistry : MonoBehaviour
{
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
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
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


}
