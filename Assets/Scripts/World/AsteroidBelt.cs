// Assets/Scripts/World/AsteroidBelt.cs
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Transform))]
public class AsteroidBelt : MonoBehaviour
{
    /*──────────────────────── Units & Template */
    public enum DistanceUnit { UnityUnits, Kilometers, AstronomicalUnits }

    [Header("Template / Units")]
    [Tooltip("Wenn true, spawnt dieser Belt NICHT selbst. Er dient nur als Vorlage – der Seeder liest die Werte aus.")]
    public bool templateOnly = true;

    [Tooltip("Welche Einheit für die eingegebenen Distanzen verwendet wird.")]
    public DistanceUnit unit = DistanceUnit.Kilometers;

    [Tooltip("Wieviele Kilometer entsprechen 1 Unity-Unit? (Beispiel: 1f => 1 Unit = 1 km; 1000f => 1 Unit = 1000 km)")]
    public float kilometersPerUnityUnit = 1f;

    const float KM_PER_AU = 149_597_870.7f;

    /*──────────────────────── Ring-Konfiguration (Eingabe in 'unit') */
    [Header("Ring Geometry (in gewählter Einheit)")]
    public float innerRadius = 100_000f;
    public float outerRadius = 200_000f;
    public float beltThickness = 2_000f;

    [Header("Population")]
    public int baseAsteroidCount = 500;

    [Tooltip("Visuelle Größe (Durchmesser) der Asteroiden in Unity-Units.")]
    public Vector2 sizeRange = new Vector2(5f, 30f);

    /*──────────────────────── Prefabs & LOD-Swap */
    [Header("Prefabs & LOD")]
    [Tooltip("Das Low-Poly-Prefab, das initial angezeigt wird.")]
    public GameObject lowPolyPrefab;

    [Tooltip("Detail-Prefab-Pool; eines wird pro Asteroid zufällig gewählt.")]
    public List<GameObject> asteroidPrefabs = new List<GameObject>();

    [Tooltip("Wenn keine Prefabs gesetzt sind: Zufallswürfel-Wahrscheinlichkeit beim Primitive-Fallback.")]
    [Range(0f, 1f)] public float cubeChance = 0.15f;

    [Tooltip("Soll die Detail-Variante direkt mit-instanzieren (deaktiviert), um Ruckler beim Umschalten zu minimieren?")]
    public bool preInstantiateDetailed = true;

    [Tooltip("Entfernung (in gewählter Einheit), unter der die Detail-Variante aktiviert wird.")]
    public float detailSwapDistance = 1_000f;

    [Tooltip("Hysterese (in gewählter Einheit), um ständiges Umschalten zu vermeiden (Detail -> Low, wenn Distanz > Swap+Hysterese).")]
    public float detailSwapHysteresis = 200f;

    [Tooltip("Optionales Ziel (z.B. deine Sonde). Wenn leer, wird zur Laufzeit nach einem Transform mit Tag 'Probe' gesucht.")]
    public Transform proximityTarget;

    [Header("Orbit")]
    public bool enableOrbit = false;
    public float innerOrbitalSpeed = 8f;
    public float outerOrbitalSpeed = 2f;
    public float orbitalSpeedJitter = 1.5f;

    [Header("LOD / Visibility (in UNITY-UNITS)")]
    public float[] lodDistances = new float[] { 50f, 100f, 200f, 400f };
    public float[] lodDensityMultipliers = new float[] { 1f, 0.75f, 0.5f, 0.25f };
    public float cullDistance = 800f;
    public int absoluteMax = 2000;

    [Header("Scene References")]
    public Camera referenceCamera;

    /*──────────────────────── Intern: aufgelöste (Unity-)Werte */
    [System.Serializable]
    public struct ResolvedConfig
    {
        public float innerRadiusUU;
        public float outerRadiusUU;
        public float beltThicknessUU;

        public float detailSwapDistanceUU;
        public float detailSwapHysteresisUU;

        public int baseAsteroidCount;
        public Vector2 sizeRangeUU;
        public bool enableOrbit;
        public float innerOrbitalSpeed;
        public float outerOrbitalSpeed;
        public float orbitalSpeedJitter;

        public float[] lodDistancesUU;
        public float[] lodDensityMultipliers;
        public float cullDistanceUU;
        public int absoluteMax;

        public GameObject lowPolyPrefab;
        public List<GameObject> detailedPrefabs;
        public float cubeChance;
        public bool preInstantiateDetailed;
        public Transform proximityTarget;
    }

    /*──────────────────────── Runtime State (nur wenn templateOnly = false) */
    struct AsteroidOrbitData
    {
        public float radiusUU;
        public float currentAngle;    // degrees
        public float orbitalSpeed;    // deg/sec
        public float verticalOffsetUU;
        public Transform asteroidTransform;

        public AsteroidOrbitData(float rUU, float angle, float speed, float yUU, Transform t)
        {
            radiusUU = rUU;
            currentAngle = angle;
            orbitalSpeed = speed;
            verticalOffsetUU = yUU;
            asteroidTransform = t;
        }
    }

    readonly List<AsteroidOrbitData> asteroidOrbits = new();
    readonly List<PooledAsteroid> asteroidComps = new();

    float _nextProbeSearch;

    /*──────────────────────── Lifecycle */
    void Start()
    {
        referenceCamera ??= Camera.main;

        if (templateOnly)
        {
            Debug.Log($"[AsteroidBelt Template] Bereit. Inner/Outer (input {unit}): {innerRadius} / {outerRadius}. UU: {ToUU(innerRadius):F1} / {ToUU(outerRadius):F1}");
            return;
        }

        var cfg = ToResolvedConfig();
        int initialCount = Mathf.Min(cfg.baseAsteroidCount, cfg.absoluteMax);
        for (int i = 0; i < initialCount; i++)
        {
            var orbitData = GenerateOrbitPosition(cfg);
            SpawnOne(cfg, orbitData);
        }
    }

    void Update()
    {
        if (templateOnly) return;

        if (proximityTarget == null && Time.time >= _nextProbeSearch)
        {
            _nextProbeSearch = Time.time + 1f;
            var probe = GameObject.FindGameObjectWithTag("Probe");
            if (probe) proximityTarget = probe.transform;
            foreach (var comp in asteroidComps)
                if (comp && comp.proximityTarget == null) comp.proximityTarget = proximityTarget;
        }

        if (enableOrbit) UpdateOrbits(Time.deltaTime);
        UpdateLODAndCulling();

        int targetCount = GetTargetCountForCurrentLOD();
        targetCount = Mathf.Clamp(targetCount, 0, absoluteMax);

        int currentCount = asteroidComps.Count;

        if (currentCount < targetCount)
        {
            int toSpawn = Mathf.Min(targetCount - currentCount, 50);
            var cfg = ToResolvedConfig();
            for (int i = 0; i < toSpawn; i++)
            {
                var orbitData = GenerateOrbitPosition(cfg);
                SpawnOne(cfg, orbitData);
            }
        }
        else if (currentCount > targetCount)
        {
            int toRemove = Mathf.Min(currentCount - targetCount, 20);
            for (int i = 0; i < toRemove && asteroidComps.Count > 0; i++)
            {
                var comp = asteroidComps[asteroidComps.Count - 1];
                asteroidComps.RemoveAt(asteroidComps.Count - 1);
                if (comp)
                {
                    asteroidOrbits.RemoveAll(a => a.asteroidTransform == comp.transform);
                    Destroy(comp.gameObject);
                }
            }
        }
    }

    /*──────────────────────── Public API für Seeder & Autopilot */
    public ResolvedConfig ToResolvedConfig()
    {
        var cfg = new ResolvedConfig
        {
            innerRadiusUU = ToUU(innerRadius),
            outerRadiusUU = ToUU(outerRadius),
            beltThicknessUU = ToUU(beltThickness),

            detailSwapDistanceUU = ToUU(detailSwapDistance),
            detailSwapHysteresisUU = ToUU(detailSwapHysteresis),

            baseAsteroidCount = baseAsteroidCount,
            sizeRangeUU = sizeRange,
            enableOrbit = enableOrbit,
            innerOrbitalSpeed = innerOrbitalSpeed,
            outerOrbitalSpeed = outerOrbitalSpeed,
            orbitalSpeedJitter = orbitalSpeedJitter,

            lodDistancesUU = (float[])lodDistances.Clone(),
            lodDensityMultipliers = (float[])lodDensityMultipliers.Clone(),
            cullDistanceUU = cullDistance,
            absoluteMax = absoluteMax,

            lowPolyPrefab = lowPolyPrefab,
            detailedPrefabs = asteroidPrefabs,
            cubeChance = cubeChance,
            preInstantiateDetailed = preInstantiateDetailed,
            proximityTarget = proximityTarget
        };

        if (cfg.outerRadiusUU < cfg.innerRadiusUU)
        {
            (cfg.innerRadiusUU, cfg.outerRadiusUU) = (cfg.outerRadiusUU, cfg.innerRadiusUU);
        }
        return cfg;
    }

    /// <summary>Gibt den Transform des nächstgelegenen Asteroiden zurück (oder null, wenn keiner existiert).</summary>
    public Transform GetClosestAsteroid(Vector3 probePosition)
    {
        Transform closest = null;
        float minDist = float.MaxValue;

        // Primär: bekannte Komponentenliste
        foreach (var comp in asteroidComps)
        {
            if (comp == null) continue;
            float d = Vector3.Distance(probePosition, comp.transform.position);
            if (d < minDist)
            {
                minDist = d;
                closest = comp.transform;
            }
        }

        // Fallback: nach Kindern mit Tag "Asteroid" suchen (falls Liste leer ist)
        if (closest == null)
        {
            foreach (Transform child in transform)
            {
                if (child != null && child.CompareTag("Asteroid"))
                {
                    float d = Vector3.Distance(probePosition, child.position);
                    if (d < minDist)
                    {
                        minDist = d;
                        closest = child;
                    }
                }
            }
        }

        return closest;
    }

    /*──────────────────────── Helpers: Konvertierung */
    float ToUU(float value)
    {
        switch (unit)
        {
            case DistanceUnit.Kilometers:
                return value / Mathf.Max(1e-6f, kilometersPerUnityUnit);
            case DistanceUnit.AstronomicalUnits:
                float km = value * KM_PER_AU;
                return km / Mathf.Max(1e-6f, kilometersPerUnityUnit);
            default:
                return value; // UnityUnits
        }
    }

    /*──────────────────────── Orbit & Spawn */
    AsteroidOrbitData GenerateOrbitPosition(ResolvedConfig cfg)
    {
        float r01 = Random.value;
        float radiusUU = Mathf.Lerp(cfg.innerRadiusUU, cfg.outerRadiusUU, Mathf.SmoothStep(0f, 1f, r01));
        float angle = Random.Range(0f, 360f);
        float yUU = Mathf.Clamp(RandomGaussian(0f, cfg.beltThicknessUU * 0.5f), -cfg.beltThicknessUU, cfg.beltThicknessUU);

        float t = Mathf.InverseLerp(cfg.innerRadiusUU, cfg.outerRadiusUU, radiusUU);
        float speed = Mathf.Lerp(cfg.innerOrbitalSpeed, cfg.outerOrbitalSpeed, t) + Random.Range(-cfg.orbitalSpeedJitter, cfg.orbitalSpeedJitter);

        return new AsteroidOrbitData(radiusUU, angle, speed, yUU, null);
    }

    float RandomGaussian(float mean, float stdDev)
    {
        float u1 = Mathf.Max(1e-6f, Random.value);
        float u2 = Random.value;
        float z0 = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        return mean + z0 * stdDev;
    }

    void UpdateOrbits(float dt)
    {
        for (int i = 0; i < asteroidOrbits.Count; i++)
        {
            var a = asteroidOrbits[i];
            a.currentAngle += a.orbitalSpeed * dt;

            float rad = a.currentAngle * Mathf.Deg2Rad;
            Vector3 localPos = new Vector3(
                Mathf.Cos(rad) * a.radiusUU,
                a.verticalOffsetUU,
                Mathf.Sin(rad) * a.radiusUU
            );

            if (a.asteroidTransform != null)
                a.asteroidTransform.position = transform.position + localPos;

            asteroidOrbits[i] = a;
        }
    }

    void UpdateLODAndCulling()
    {
        if (referenceCamera == null) return;
        float distance = Vector3.Distance(referenceCamera.transform.position, transform.position);
        bool visible = distance <= cullDistance;
        SetVisible(visible);
    }

    void SetVisible(bool visible)
    {
        foreach (var comp in asteroidComps)
            if (comp) comp.SetVisible(visible);
    }

    int GetTargetCountForCurrentLOD()
    {
        if (referenceCamera == null) return baseAsteroidCount;

        float distance = Vector3.Distance(referenceCamera.transform.position, transform.position);
        float mult = GetLODDensityMultiplier(distance);
        int target = Mathf.RoundToInt(baseAsteroidCount * mult);
        return Mathf.Clamp(target, 0, absoluteMax);
    }

    float GetLODDensityMultiplier(float distance)
    {
        for (int i = 0; i < lodDistances.Length; i++)
        {
            if (distance <= lodDistances[i])
            {
                return i < lodDensityMultipliers.Length ? lodDensityMultipliers[i] : 1f;
            }
        }
        return lodDensityMultipliers.Length > 0
            ? lodDensityMultipliers[lodDensityMultipliers.Length - 1]
            : 1f;
    }


    void SpawnOne(ResolvedConfig cfg, AsteroidOrbitData orbitData)
    {
        GameObject go = new GameObject("Asteroid");
        go.tag = "Asteroid";
        go.transform.SetParent(transform, true);

        float rad = orbitData.currentAngle * Mathf.Deg2Rad;
        Vector3 localPos = new Vector3(
            Mathf.Cos(rad) * orbitData.radiusUU,
            orbitData.verticalOffsetUU,
            Mathf.Sin(rad) * orbitData.radiusUU
        );

        go.transform.position = transform.position + localPos;
        go.transform.localRotation = Random.rotation;

        // Größe festlegen
        float size = Random.Range(cfg.sizeRangeUU.x, cfg.sizeRangeUU.y);
        go.transform.localScale = Vector3.one * size;

        // Komponenten
        var mine = go.AddComponent<MineableAsteroid>();

        // >>> WICHTIG: Nav-Sphäre sauber setzen (Durchmesser = size) <<<
        mine.navSphereRadiusUU = Mathf.Max(0.01f, size * 0.5f);
        mine.EnsureNavSphereCollider();
        mine.ApplyNavSphereRadius();

        // LOD / PooledAsteroid wie gehabt ...
        var pooled = go.AddComponent<PooledAsteroid>();
        go.transform.localScale = Vector3.one * size;
        mine.Configure(MaterialDatabase.GetRandomId(), Random.Range(800, 2000));
        

        GameObject detailPrefab = null;
        if (cfg.detailedPrefabs != null && cfg.detailedPrefabs.Count > 0)
        {
            int idx = Random.Range(0, cfg.detailedPrefabs.Count);
            detailPrefab = cfg.detailedPrefabs[idx];
        }

        float primitiveFallbackChance = (cfg.lowPolyPrefab == null) ? 0f : cfg.cubeChance;

        pooled.InitializeLOD(
            cfg.lowPolyPrefab,
            detailPrefab,
            cfg.proximityTarget ?? proximityTarget,
            cfg.detailSwapDistanceUU,
            cfg.detailSwapHysteresisUU,
            cfg.preInstantiateDetailed,
            primitiveFallbackChance
        );

        var orbitWithTransform = new AsteroidOrbitData(
            orbitData.radiusUU, orbitData.currentAngle, orbitData.orbitalSpeed,
            orbitData.verticalOffsetUU, go.transform
        );
        asteroidOrbits.Add(orbitWithTransform);
        asteroidComps.Add(pooled);
    }

}
