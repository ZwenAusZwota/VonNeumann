// Assets/Scripts/World/PooledAsteroid.cs
using UnityEngine;

/// <summary>
/// Komponente für gepoolte/verwaltete Asteroiden mit Low/High-LOD-Visuals.
/// Berechnet nach LOD-Erzeugung einen sicheren Nav-Radius über alle Kinder-Renderer/Collider.
/// </summary>
public class PooledAsteroid : MonoBehaviour
{
    public AsteroidPool Pool { get; set; } // optional

    [Header("LOD Visuals (runtime)")]
    public GameObject lowPolyPrefab;
    public GameObject detailedPrefab;

    [Tooltip("Instanz des Low-Poly-Kindobjekts")]
    public GameObject lowInstance;
    [Tooltip("Instanz des High-Detail-Kindobjekts (deaktiviert bis Swap)")]
    public GameObject highInstance;

    [Tooltip("Zieltransform (z. B. Sonde) für den Distanz-Swap")]
    public Transform proximityTarget;

    [Tooltip("Abstand (Unity-Units), ab dem High angezeigt wird")]
    public float swapDistanceUU = 150f;

    [Tooltip("Hysterese (Unity-Units) – zurück zu Low, wenn Distanz > swap + hysteresis")]
    public float swapHysteresisUU = 25f;

    [Tooltip("Detail-Instanz bereits bei Spawn erzeugen (deaktiviert)?")]
    public bool preInstantiateHigh = true;

    private Renderer[] renderers;
    private Collider[] colliders;
    private bool isVisible = true;
    private bool highActive = false;
    private float nextCheckTime;

    void Awake()
    {
        RebuildCaches();
    }

    /// <summary>Erzeugt/konfiguriert die Visuals. Kann mehrfach für Reuse gerufen werden.</summary>
    public void InitializeLOD(
        GameObject lowPrefab,
        GameObject highPrefab,
        Transform target,
        float swapDistUU,
        float hysteresisUU,
        bool preInstantiate,
        float primitiveCubeChanceFallback = 0.15f)
    {
        // Bestehende Kinder bereinigen
        if (lowInstance) Destroy(lowInstance);
        if (highInstance) Destroy(highInstance);

        lowPolyPrefab = lowPrefab;
        detailedPrefab = highPrefab;
        proximityTarget = target;
        swapDistanceUU = Mathf.Max(0.01f, swapDistUU);
        swapHysteresisUU = Mathf.Max(0f, hysteresisUU);
        preInstantiateHigh = preInstantiate;

        // Low-Poly sofort instanzieren
        if (lowPolyPrefab != null)
        {
            lowInstance = Instantiate(lowPolyPrefab, transform);
        }
        else
        {
            // Fallback: Primitive als Low-Poly (Chance: Cube vs Sphere)
            var type = Random.value < primitiveCubeChanceFallback ? PrimitiveType.Cube : PrimitiveType.Sphere;
            lowInstance = GameObject.CreatePrimitive(type);
            lowInstance.transform.SetParent(transform, false);
        }
        // Low-Layer: Tag + Collider sicherstellen
        SetTagRecursively(lowInstance, "Asteroid");
        EnsureAnyCollider(lowInstance);

        // High ggf. vorab instanzieren (deaktiviert)
        if (preInstantiateHigh && detailedPrefab != null)
        {
            highInstance = Instantiate(detailedPrefab, transform);
            SetTagRecursively(highInstance, "Asteroid");
            EnsureAnyCollider(highInstance);
            highInstance.SetActive(false);
        }
        else
        {
            highInstance = null; // wird bei Bedarf on-demand instanziert
        }

        // Start: Low sichtbar
        highActive = false;
        if (lowInstance) lowInstance.SetActive(true);

        // Renderer/Collider der Kinder erfassen
        RebuildCaches();

        // >>> NEU: sicheren Navigations-Radius aus gesamter Hierarchie berechnen
        var mine = GetComponent<MineableAsteroid>();
        if (mine != null) mine.RecalculateNavSphereRadiusFromHierarchy(1.1f);
    }

    /// <summary>Wird vom Belt für Culling aufgerufen.</summary>
    public void SetVisible(bool visible)
    {
        if (isVisible == visible) return;
        isVisible = visible;

        // Renderer schalten (Kinder enthalten)
        if (renderers != null)
            foreach (var r in renderers) if (r) r.enabled = visible;

        // Collider optional mit schalten
        if (colliders != null)
            foreach (var c in colliders) if (c) c.enabled = visible;
    }

    /// <summary>Rebuild von Renderer/Collider-Listen nach LOD-Änderungen.</summary>
    public void RebuildCaches()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
    }

    void Update()
    {
        // Alle 0.2s prüfen
        if (Time.time < nextCheckTime) return;
        nextCheckTime = Time.time + 0.2f;

        // Wenn kein Ziel, versuchen eines zu finden (Tag "Probe")
        if (proximityTarget == null)
        {
            var probe = GameObject.FindGameObjectWithTag("Probe");
            if (probe) proximityTarget = probe.transform;
            if (proximityTarget == null) return; // noch kein Ziel, bleib Low
        }

        float d = Vector3.Distance(proximityTarget.position, transform.position);

        if (!highActive && d <= swapDistanceUU)
        {
            // auf HIGH wechseln
            EnsureHighInstance();
            if (highInstance) highInstance.SetActive(true);
            if (lowInstance) lowInstance.SetActive(false);
            highActive = true;
            // Renderer neu einsammeln (damit Belt-Culling weiterhin greift)
            RebuildCaches();
            return;
        }

        if (highActive && d > (swapDistanceUU + swapHysteresisUU))
        {
            // zurück auf LOW
            if (highInstance) highInstance.SetActive(false);
            if (lowInstance) lowInstance.SetActive(true);
            highActive = false;
            RebuildCaches();
        }
    }

    private void EnsureHighInstance()
    {
        if (highInstance != null) return;
        if (detailedPrefab == null) return;

        highInstance = Instantiate(detailedPrefab, transform);
        SetTagRecursively(highInstance, "Asteroid");
        EnsureAnyCollider(highInstance);
        highInstance.SetActive(false);

        // >>> NEU: Nachladen von High kann Größe ändern -> Radius neu bestimmen
        var mine = GetComponent<MineableAsteroid>();
        if (mine != null) mine.RecalculateNavSphereRadiusFromHierarchy(1.1f);
    }

    /// <summary>Für Pool-Reuse oder manuelles Zurücksetzen.</summary>
    public void ResetAsteroid()
    {
        // Sichtbarkeit zurücksetzen
        SetVisible(true);

        // LOD-Instanzen entfernen (wir wollen saubere Neu-Initialisierung)
        if (lowInstance) Destroy(lowInstance);
        if (highInstance) Destroy(highInstance);
        lowInstance = null;
        highInstance = null;

        highActive = false;
        nextCheckTime = 0f;

        // wieder frisches Caching, aktuell nur Parent
        RebuildCaches();

        // Rücksetzen von Transform-Basiswerten
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;
    }

    // Getter
    public bool IsVisible => isVisible;

    // Hilfsfunktionen (Tagging/Collider für LOD-Kinder)
    private static void SetTagRecursively(GameObject go, string tag)
    {
        if (go == null) return;
        go.tag = tag;
        foreach (Transform child in go.transform)
            if (child) SetTagRecursively(child.gameObject, tag);
    }

    private static void EnsureAnyCollider(GameObject go)
    {
        if (go == null) return;
        // Wenn im gesamten Hierarchiebaum kein Collider existiert, füge einen SphereCollider am Root hinzu.
        if (go.GetComponentInChildren<Collider>(true) == null)
        {
            var sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = false; // für Scan/Annäherung ausreichend
            sc.radius = 0.5f;     // skaliert mit Transform.localScale
        }
    }
}
