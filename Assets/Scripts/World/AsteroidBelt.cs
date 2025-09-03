// Assets/Scripts/World/AsteroidBelt.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Transform))]
public class AsteroidBelt : MonoBehaviour
{
    /*──────────────────────── Inspector Settings */
    [Header("Ring Geometry (Unity Units)")]
    [Tooltip("Inner radius of the belt.")]
    public float innerRadius = 1f;

    [Tooltip("Outer radius of the belt.")]
    public float outerRadius = 150f;

    [Tooltip("Vertical thickness of the belt (Y-axis spread).")]
    public float beltThickness = 5f; // erhöht für stärkere Streuung

    [Header("Population")]
    [Tooltip("Base number of asteroids at full density.")]
    public int baseAsteroidCount = 500;

    [Tooltip("Curve over distance to star to vary density (0..1).")]
    public AnimationCurve densityCurve = AnimationCurve.Linear(0, 1, 1, 1);

    [Tooltip("Size range for spawned asteroids.")]
    public Vector2 sizeRange = new Vector2(0.2f, 2.5f);

    [Header("Prefabs & Materials (optional)")]
    [Tooltip("Optional asteroid prefabs to spawn. If empty, fallback to primitive sphere/cube.")]
    public List<GameObject> asteroidPrefabs = new List<GameObject>();

    [Tooltip("Chance to spawn cube when using primitive fallback.")]
    [Range(0f, 1f)] public float cubeChance = 0.15f;

    [Header("Orbit")]
    [Tooltip("Base orbital speed (deg/sec) at inner radius.")]
    public float innerOrbitalSpeed = 8f;

    [Tooltip("Base orbital speed (deg/sec) at outer radius.")]
    public float outerOrbitalSpeed = 2f;

    [Tooltip("Random variation added/subtracted to orbital speed.")]
    public float orbitalSpeedJitter = 1.5f;

    [Header("Scene References")]
    [Tooltip("Optional: A camera to compute LOD/visibility. If null, Camera.main is used.")]
    public Camera referenceCamera;

    [Header("LOD / Visibility")]
    [Tooltip("Distance thresholds for LOD/density adjustments.")]
    public float[] lodDistances = new float[] { 50f, 100f, 200f, 400f };

    [Tooltip("Density multipliers per LOD level.")]
    public float[] lodDensityMultipliers = new float[] { 1f, 0.75f, 0.5f, 0.25f };

    [Tooltip("Distance beyond which asteroids are hidden (renderers disabled).")]
    public float cullDistance = 800f;

    [Tooltip("Max instances overall (hard cap).")]
    public int absoluteMax = 2000;

    /*──────────────────────── Runtime State */
    struct AsteroidOrbitData
    {
        public float radius;
        public float currentAngle;    // degrees
        public float orbitalSpeed;    // deg/sec
        public float verticalOffset;  // local Y
        public Transform asteroidTransform;

        public AsteroidOrbitData(float r, float angle, float speed, float yOffset, Transform t)
        {
            radius = r;
            currentAngle = angle;
            orbitalSpeed = speed; // Bugfix: nicht 0 setzen
            verticalOffset = yOffset;
            asteroidTransform = t;
        }
    }

    readonly List<AsteroidOrbitData> asteroidOrbits = new List<AsteroidOrbitData>();
    readonly List<Renderer> asteroidRenderers = new List<Renderer>();
    readonly List<GameObject> spawnedAsteroids = new List<GameObject>();

    void Start()
    {
        if (referenceCamera == null)
            referenceCamera = Camera.main;

        // Initial population
        int initialCount = Mathf.Min(baseAsteroidCount, absoluteMax);
        for (int i = 0; i < initialCount; i++)
        {
            var orbitData = GenerateRealisticOrbitPosition();
            SpawnPooledAsteroid(orbitData);
        }
    }

    void Update()
    {
        UpdateOrbits(Time.deltaTime);
        UpdateLODAndCulling();

        // (Optional): Dynamik – Anzahl anhand LOD nachziehen
        int targetCount = GetTargetCountForCurrentLOD();
        targetCount = Mathf.Clamp(targetCount, 0, absoluteMax);

        int currentCount = spawnedAsteroids.Count;

        if (currentCount < targetCount)
        {
            int toSpawn = Mathf.Min(targetCount - currentCount, 50); // Limit spawns per frame
            for (int i = 0; i < toSpawn; i++)
            {
                var orbitData = GenerateRealisticOrbitPosition();
                SpawnPooledAsteroid(orbitData);
            }
        }
        else if (currentCount > targetCount)
        {
            int toRemove = Mathf.Min(currentCount - targetCount, 20); // Limit removals per frame
            for (int i = 0; i < toRemove && spawnedAsteroids.Count > 0; i++)
            {
                // Simple remove: take last
                var go = spawnedAsteroids[spawnedAsteroids.Count - 1];
                spawnedAsteroids.RemoveAt(spawnedAsteroids.Count - 1);

                // Remove orbit and renderers
                asteroidOrbits.RemoveAll(a => a.asteroidTransform == go.transform);

                foreach (var rend in go.GetComponentsInChildren<Renderer>())
                    asteroidRenderers.Remove(rend);

                Destroy(go);
            }
        }
    }

    /*──────────────────────── Orbit & Spawn Helpers */
    AsteroidOrbitData GenerateRealisticOrbitPosition()
    {
        // Radius mit leichter Bias-Verteilung (mehr Vorkommen in der Mitte)
        float r01 = Random.value;
        float radius = Mathf.Lerp(innerRadius, outerRadius, Mathf.SmoothStep(0f, 1f, r01));

        // Winkel zufällig
        float angle = Random.Range(0f, 360f);

        // Vertikale Streuung per (geclampter) Normalverteilung
        float yOffset = Mathf.Clamp(RandomGaussian(0f, beltThickness * 0.5f), -beltThickness, beltThickness);

        // Orbitalgeschwindigkeit je nach Radius (langsamer außen), plus Jitter
        float t = Mathf.InverseLerp(innerRadius, outerRadius, radius);
        float speed = Mathf.Lerp(innerOrbitalSpeed, outerOrbitalSpeed, t) + Random.Range(-orbitalSpeedJitter, orbitalSpeedJitter);

        return new AsteroidOrbitData(radius, angle, speed, yOffset, null);
    }

    // Box–Muller
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
                Mathf.Cos(rad) * a.radius,
                a.verticalOffset,
                Mathf.Sin(rad) * a.radius
            );

            if (a.asteroidTransform != null)
            {
                a.asteroidTransform.position = transform.position + localPos;
            }

            asteroidOrbits[i] = a;
        }
    }

    void UpdateLODAndCulling()
    {
        if (referenceCamera == null) return;

        float distance = Vector3.Distance(referenceCamera.transform.position, transform.position);

        // Culling
        bool visible = distance <= cullDistance;
        SetVisible(visible);

        // Optional: hier könnte man Renderer-Qualität/Shader LOD umschalten
    }

    void SetVisible(bool visible)
    {
        foreach (var r in asteroidRenderers)
        {
            if (r != null) r.enabled = visible;
        }
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
        return lodDensityMultipliers.Length > 0 ? lodDensityMultipliers[lodDensityMultipliers.Length - 1] : 1f;
    }

    /*──────────────────────── Spawn Implementierungen */

    void SpawnPooledAsteroid(AsteroidOrbitData orbitData)
    {
        // Wähle Prefab oder Fallback-Primitive
        GameObject go;
        if (asteroidPrefabs != null && asteroidPrefabs.Count > 0)
        {
            int idx = Random.Range(0, asteroidPrefabs.Count);
            go = Instantiate(asteroidPrefabs[idx]);
        }
        else
        {
            PrimitiveType prim = Random.value < cubeChance ? PrimitiveType.Cube : PrimitiveType.Sphere;
            go = GameObject.CreatePrimitive(prim);
        }

        // Position/Rotation/Scale
        float rad = orbitData.currentAngle * Mathf.Deg2Rad;
        Vector3 localPos = new Vector3(
            Mathf.Cos(rad) * orbitData.radius,
            orbitData.verticalOffset,
            Mathf.Sin(rad) * orbitData.radius
        );

        go.transform.SetParent(transform, true);
        go.transform.position = transform.position + localPos;
        go.transform.localRotation = Random.rotation;
        float size = Random.Range(sizeRange.x, sizeRange.y);
        go.transform.localScale = Vector3.one * size;

        // Collider & Tag
        if (!go.TryGetComponent(out Collider col))
            col = go.AddComponent<MeshCollider>();
        col.isTrigger = false;
        go.tag = "Asteroid";

        // MineableAsteroid
        var mine = go.GetComponent<MineableAsteroid>();
        if (mine == null) mine = go.AddComponent<MineableAsteroid>();
        mine.Configure(MaterialDatabase.GetRandomId(), Random.Range(800, 2000));

        // Orbit-Daten + Renderer-Cache
        var orbitWithTransform = new AsteroidOrbitData(
            orbitData.radius, orbitData.currentAngle, orbitData.orbitalSpeed,
            orbitData.verticalOffset, go.transform
        );
        asteroidOrbits.Add(orbitWithTransform);

        foreach (var rend in go.GetComponentsInChildren<Renderer>())
            asteroidRenderers.Add(rend);

        spawnedAsteroids.Add(go);
    }

    // Alternative, falls du „traditionell“ ohne Pool erzeugst:
    void SpawnTraditionalAsteroid(AsteroidOrbitData orbitData)
    {
        // Prefab oder Primitive
        GameObject go;
        if (asteroidPrefabs != null && asteroidPrefabs.Count > 0)
        {
            int idx = Random.Range(0, asteroidPrefabs.Count);
            go = Instantiate(asteroidPrefabs[idx]);
        }
        else
        {
            PrimitiveType prim = Random.value < cubeChance ? PrimitiveType.Cube : PrimitiveType.Sphere;
            go = GameObject.CreatePrimitive(prim);
        }

        // Position/Rotation/Scale
        float rad = orbitData.currentAngle * Mathf.Deg2Rad;
        Vector3 localPos = new Vector3(
            Mathf.Cos(rad) * orbitData.radius,
            orbitData.verticalOffset,
            Mathf.Sin(rad) * orbitData.radius
        );

        go.transform.SetParent(transform, true);
        go.transform.position = transform.position + localPos;
        go.transform.localRotation = Random.rotation;
        float size = Random.Range(sizeRange.x, sizeRange.y);
        go.transform.localScale = Vector3.one * size;

        // Collider & Tag
        if (!go.TryGetComponent(out Collider col))
            col = go.AddComponent<MeshCollider>();
        col.isTrigger = false;
        go.tag = "Asteroid";

        // MineableAsteroid → zentrale Materialzuweisung erfolgt dort
        var mine = go.GetComponent<MineableAsteroid>();
        if (mine == null) mine = go.AddComponent<MineableAsteroid>();
        mine.Configure(MaterialDatabase.GetRandomId(), Random.Range(800, 2000));

        // Orbit & Renderer
        var orbitWithTransform = new AsteroidOrbitData(
            orbitData.radius, orbitData.currentAngle, orbitData.orbitalSpeed,
            orbitData.verticalOffset, go.transform
        );
        asteroidOrbits.Add(orbitWithTransform);

        foreach (var rend in go.GetComponentsInChildren<Renderer>())
            asteroidRenderers.Add(rend);

        spawnedAsteroids.Add(go);
    }
}
