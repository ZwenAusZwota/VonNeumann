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

    [Header("Orbital Parameters")]
    [Tooltip("Asteroiden bewegen sich in Umlaufbahnen um das Zentrum")]
    public bool enableOrbitalMotion = true;

    [Tooltip("Orbital speed multiplier (higher = faster orbits)")]
    public float orbitalSpeedMultiplier = 1f;

    [Tooltip("Variation in orbital speeds (0 = uniform, 1 = high variation)")]
    [Range(0f, 1f)]
    public float speedVariation = 0.3f;

    [Header("Belt Structure")]
    [Tooltip("Vertical thickness of the belt (Y-axis spread)")]
    public float beltThickness = 0.1f;

    [Tooltip("Density variation across the belt (inner vs outer)")]
    public AnimationCurve densityDistribution = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.5f);

    [Header("Asteroid Population")]
    [Tooltip("Total number of asteroids to spawn.")]
    public int asteroidCount = 1000;

    [Header("Object Pool Settings")]
    [Tooltip("Use Object Pooling for better performance?")]
    public bool useObjectPooling = true;

    [Tooltip("Pre-spawn this many asteroids in pool")]
    public int preSpawnCount = 100;

    [Header("Prefabs & Primitives")]
    [Tooltip("Optional list of prefabs (rocks, low‑poly meshes etc.). If empty, fallback primitives are used.")]
    public List<GameObject> asteroidPrefabs = new();

    [Tooltip("If using fallback primitives, relative chance to spawn a cube instead of a sphere (0–1).")]
    [Range(0f, 1f)]
    public float cubeChance = 0.2f;

    [Tooltip("Min & max uniform scale applied to spawned asteroids.")]
    public Vector2 sizeRange = new(0.4f, 2f);

    [Header("Visibility & Performance")]
    [Tooltip("Asteroids render only when the main camera is within this distance from the belt center.")]
    public float visibilityDistance = 500f;

    [Header("LOD Settings")]
    [Tooltip("Enable Level of Detail for performance?")]
    public bool enableLOD = true;

    [Tooltip("Distance ranges for different LOD levels")]
    public float[] lodDistances = { 50f, 150f, 300f };

    [Tooltip("Asteroid density multipliers for each LOD level")]
    [Range(0f, 1f)]
    public float[] lodDensityMultipliers = { 1f, 0.5f, 0.25f };

    [Header("Rendering")]
    [Tooltip("Optional Liste von Materialien, aus denen jeder Asteroid zufällig eines bekommt.")]
    public List<Material> asteroidMaterials = new();

    /*──────────────────────── Internals */
    readonly List<PooledAsteroid> spawnedAsteroids = new();
    readonly List<AsteroidOrbitData> asteroidOrbits = new(); // Für orbital motion
    readonly List<Renderer> asteroidRenderers = new();
    bool spawned;
    AsteroidPool pool;
    Camera mainCamera;

    // Struct für Orbital-Daten
    [System.Serializable]
    public struct AsteroidOrbitData
    {
        public float radius;          // Entfernung vom Zentrum
        public float currentAngle;    // Aktuelle Position im Orbit (Grad)
        public float orbitalSpeed;    // Grad pro Sekunde
        public float verticalOffset;  // Y-Offset für 3D-Struktur
        public Transform asteroidTransform;

        public AsteroidOrbitData(float r, float angle, float speed, float yOffset, Transform t)
        {
            radius = r;
            currentAngle = angle;
            orbitalSpeed = speed;
            verticalOffset = yOffset;
            asteroidTransform = t;
        }
    }

    /*──────────────────────── Public API */
    public void Init(float innerUnits, float outerUnits, int count = -1)
    {
        innerRadius = innerUnits;
        outerRadius = outerUnits;
        if (count > 0) asteroidCount = count;

        // Sicherstellen dass outer > inner
        if (outerRadius <= innerRadius)
            outerRadius = innerRadius + 0.1f;

        if (!spawned)
        {
            SpawnAsteroids();
            spawned = true;
        }
    }

    /*──────────────────────── Unity Lifecycle */
    void Start()
    {
        mainCamera = Camera.main;

        if (useObjectPooling)
        {
            SetupObjectPool();
        }

        if (!spawned)
        {
            SpawnAsteroids();
            spawned = true;
        }
        PlanetRegistry.Instance?.RegisterAsteroidBelt(transform);
    }

    void Update()
    {
        // Reduziere Update-Frequenz
        if (Time.fixedTime % 0.1f < Time.fixedDeltaTime) // Alle 0.1s
        {
            UpdateVisibility();
            if (enableLOD) UpdateLOD();
        }

        if (enableOrbitalMotion)
            UpdateOrbitalMotion(); // Jedes Frame für smooth movement
    }

    /*──────────────────────── Object Pool Setup */
    void SetupObjectPool()
    {
        pool = AsteroidPool.Instance;

        if (pool == null)
        {
            GameObject poolGO = new GameObject("AsteroidPool");
            pool = poolGO.AddComponent<AsteroidPool>();

            pool.initialPoolSize = preSpawnCount;
            pool.asteroidPrefabs = asteroidPrefabs;
            pool.asteroidMaterials = asteroidMaterials;
            pool.cubeChance = cubeChance;
        }
    }

    /*──────────────────────── Improved Spawning */
    void SpawnAsteroids()
    {
        if (outerRadius <= innerRadius)
            outerRadius = innerRadius + 1f;

        // LOD-angepasste Asteroid-Anzahl
        int actualAsteroidCount = GetLODAdjustedCount();

        // Clear existing data
        asteroidOrbits.Clear();

        for (int i = 0; i < actualAsteroidCount; i++)
        {
            // Realistische Ring-Verteilung
            var orbitData = GenerateRealisticOrbitPosition();

            if (useObjectPooling && pool != null)
            {
                SpawnPooledAsteroid(orbitData);
            }
            else
            {
                SpawnTraditionalAsteroid(orbitData);
            }
        }

        Debug.Log($"AsteroidBelt '{name}' spawned {actualAsteroidCount} asteroids in ring " +
                 $"(Inner: {innerRadius:F1}, Outer: {outerRadius:F1} Units, Pool: {useObjectPooling})");
    }

    AsteroidOrbitData GenerateRealisticOrbitPosition()
    {
        // Radius basierend auf Dichteverteilung
        float normalizedRadius = SampleFromDensityDistribution();
        float radius = Mathf.Lerp(innerRadius, outerRadius, normalizedRadius);

        // Zufälliger Winkel für gleichmäßige Verteilung
        float angle = Random.Range(0f, 360f);

        // Orbital-Geschwindigkeit (Kepler: v ∝ 1/√r)
        float baseSpeed = CalculateOrbitalSpeed(radius);
        float speedVar = Random.Range(1f - speedVariation, 1f + speedVariation);
        float orbitalSpeed = baseSpeed * speedVar * orbitalSpeedMultiplier;

        // Vertikale Streuung für 3D-Struktur
        float yOffset = Random.Range(-beltThickness, beltThickness);

        return new AsteroidOrbitData(radius, angle, orbitalSpeed, yOffset, null);
    }

    float SampleFromDensityDistribution()
    {
        // Verwende Rejection Sampling für realistische Verteilung
        float maxDensity = 1f;

        for (int attempts = 0; attempts < 20; attempts++)
        {
            float r = Random.Range(0f, 1f);
            float density = densityDistribution.Evaluate(r);

            if (Random.Range(0f, maxDensity) <= density)
                return r;
        }

        return Random.Range(0f, 1f); // Fallback
    }

    float CalculateOrbitalSpeed(float radius)
    {
        // Vereinfachte Kepler-Geschwindigkeit: v ∝ 1/√r
        // Normalisiert für sinnvolle Geschwindigkeiten in Unity
        float baseRadius = (innerRadius + outerRadius) * 0.5f;
        return 10f / Mathf.Sqrt(radius / baseRadius); // Grad pro Sekunde
    }

    int GetLODAdjustedCount()
    {
        if (!enableLOD || mainCamera == null)
            return asteroidCount;

        float distance = Vector3.Distance(mainCamera.transform.position, transform.position);
        float densityMultiplier = GetLODDensityMultiplier(distance);
        return Mathf.RoundToInt(asteroidCount * densityMultiplier);
    }

    /*──────────────────────── Orbital Motion */
    void UpdateOrbitalMotion()
    {
        for (int i = 0; i < asteroidOrbits.Count; i++)
        {
            var orbit = asteroidOrbits[i];
            if (orbit.asteroidTransform == null) continue;

            // Update angle
            orbit.currentAngle += orbit.orbitalSpeed * Time.deltaTime;
            if (orbit.currentAngle >= 360f) orbit.currentAngle -= 360f;

            // Calculate new position
            float rad = orbit.currentAngle * Mathf.Deg2Rad;
            Vector3 newPos = new Vector3(
                Mathf.Cos(rad) * orbit.radius,
                orbit.verticalOffset,
                Mathf.Sin(rad) * orbit.radius
            );

            orbit.asteroidTransform.position = transform.position + newPos;

            // Update orbit data
            asteroidOrbits[i] = orbit;
        }
    }

    /*──────────────────────── Spawning Methods */
    void SpawnPooledAsteroid(AsteroidOrbitData orbitData)
    {
        PooledAsteroid pooledAsteroid = pool.GetAsteroid();
        if (pooledAsteroid == null)
        {
            Debug.LogWarning("Could not get asteroid from pool, falling back to traditional spawn");
            SpawnTraditionalAsteroid(orbitData);
            return;
        }

        // Calculate initial world position
        float rad = orbitData.currentAngle * Mathf.Deg2Rad;
        Vector3 localPos = new Vector3(
            Mathf.Cos(rad) * orbitData.radius,
            orbitData.verticalOffset,
            Mathf.Sin(rad) * orbitData.radius
        );
        Vector3 worldPos = transform.position + localPos;

        // Configure asteroid
        pool.ConfigureAsteroid(pooledAsteroid, worldPos, sizeRange, transform);

        // Store orbit data
        var orbitWithTransform = new AsteroidOrbitData(
            orbitData.radius, orbitData.currentAngle, orbitData.orbitalSpeed,
            orbitData.verticalOffset, pooledAsteroid.transform
        );
        asteroidOrbits.Add(orbitWithTransform);

        spawnedAsteroids.Add(pooledAsteroid);

        var renderers = pooledAsteroid.GetComponentsInChildren<Renderer>();
        asteroidRenderers.AddRange(renderers);
    }

    void SpawnTraditionalAsteroid(AsteroidOrbitData orbitData)
    {
        Material chosenMat = asteroidMaterials != null && asteroidMaterials.Count > 0
            ? asteroidMaterials[Random.Range(0, asteroidMaterials.Count)]
            : null;

        // Choose prefab or fallback primitive
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

            var rend = go.GetComponent<MeshRenderer>();
            if (chosenMat != null)
                rend.material = chosenMat;
            else
            {
                var mat = new Material(Shader.Find("Standard")) { color = new Color32(120, 120, 120, 255) };
                rend.material = mat;
            }
        }

        // Calculate initial position
        float rad = orbitData.currentAngle * Mathf.Deg2Rad;
        Vector3 localPos = new Vector3(
            Mathf.Cos(rad) * orbitData.radius,
            orbitData.verticalOffset,
            Mathf.Sin(rad) * orbitData.radius
        );

        // Parent & transform
        go.transform.SetParent(transform, true);
        go.transform.position = transform.position + localPos;
        go.transform.localRotation = Random.rotation;
        float size = Random.Range(sizeRange.x, sizeRange.y);
        go.transform.localScale = Vector3.one * size;

        // Collider & tag
        if (!go.TryGetComponent(out Collider col))
            col = go.AddComponent<MeshCollider>();
        col.isTrigger = false;
        go.tag = "Asteroid";

        // MineableAsteroid component
        var mine = go.GetComponent<MineableAsteroid>();
        if (mine == null)
            mine = go.AddComponent<MineableAsteroid>();
        mine.materialId = MaterialRegistry.GetRandomId();
        mine.startUnits = Random.Range(800, 2000);

        // Store orbit data
        var orbitWithTransform = new AsteroidOrbitData(
            orbitData.radius, orbitData.currentAngle, orbitData.orbitalSpeed,
            orbitData.verticalOffset, go.transform
        );
        asteroidOrbits.Add(orbitWithTransform);

        // Cache renderers for visibility toggling
        foreach (var rend in go.GetComponentsInChildren<Renderer>())
            asteroidRenderers.Add(rend);
    }

    /*──────────────────────── LOD System */
    float GetLODDensityMultiplier(float distance)
    {
        for (int i = 0; i < lodDistances.Length; i++)
        {
            if (distance <= lodDistances[i])
            {
                return i < lodDensityMultipliers.Length ? lodDensityMultipliers[i] : 1f;
            }
        }

        return lodDensityMultipliers.Length > 0 ? lodDensityMultipliers[lodDensityMultipliers.Length - 1] : 0.1f;
    }

    void UpdateLOD()
    {
        if (mainCamera == null) return;

        float distance = Vector3.Distance(mainCamera.transform.position, transform.position);
        float targetDensity = GetLODDensityMultiplier(distance);
        int targetCount = Mathf.RoundToInt(asteroidCount * targetDensity);

        if (useObjectPooling)
        {
            AdjustPooledAsteroidCount(targetCount);
        }
    }

    void AdjustPooledAsteroidCount(int targetCount)
    {
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
                var asteroid = spawnedAsteroids[spawnedAsteroids.Count - 1];
                spawnedAsteroids.RemoveAt(spawnedAsteroids.Count - 1);

                // Remove from orbit data
                asteroidOrbits.RemoveAll(orbit => orbit.asteroidTransform == asteroid.transform);

                var renderers = asteroid.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    asteroidRenderers.Remove(renderer);
                }

                pool.ReturnAsteroid(asteroid);
            }
        }
    }

    /*──────────────────────── Visibility Management */
    void UpdateVisibility()
    {
        if (mainCamera == null) return;

        float dist = Vector3.Distance(mainCamera.transform.position, transform.position);
        bool shouldRender = dist <= visibilityDistance;

        foreach (var asteroid in spawnedAsteroids)
        {
            if (asteroid != null)
                asteroid.SetVisible(shouldRender);
        }

        foreach (var rend in asteroidRenderers)
        {
            if (rend && rend.enabled != shouldRender)
                rend.enabled = shouldRender;
        }
    }

    /*──────────────────────── Cleanup */
    public void ClearAllAsteroids()
    {
        if (useObjectPooling && pool != null)
        {
            foreach (var asteroid in spawnedAsteroids)
            {
                if (asteroid != null)
                    pool.ReturnAsteroid(asteroid);
            }
            spawnedAsteroids.Clear();
        }
        else
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.CompareTag("Asteroid"))
                    DestroyImmediate(child.gameObject);
            }
        }

        asteroidRenderers.Clear();
        asteroidOrbits.Clear();
        spawned = false;
    }

    void OnDestroy()
    {
        ClearAllAsteroids();
    }

    /*──────────────────────── Debug & Stats */
    public int GetActiveAsteroidCount()
    {
        return useObjectPooling ? spawnedAsteroids.Count : asteroidRenderers.Count;
    }

    public AsteroidBeltStats GetStats()
    {
        var stats = new AsteroidBeltStats(
            GetActiveAsteroidCount(),
            GetVisibleAsteroidCount(), // Verwende die neue Methode
            useObjectPooling,
            innerRadius,
            outerRadius,
            visibilityDistance,
            enableOrbitalMotion
        );

        // Erweiterte Ressourcen-Stats für Von-Neumann-Sonden-Gameplay
        var mineableAsteroids = GetAllMineableAsteroids();
        stats.CalculateResourceStats(mineableAsteroids);

        return stats;
    }

    public int GetVisibleAsteroidCount()
    {
        if (useObjectPooling)
        {
            return spawnedAsteroids.Count(asteroid =>
                asteroid != null && asteroid.IsVisible);
        }
        else
        {
            return asteroidRenderers.Count(renderer =>
                renderer != null && renderer.enabled);
        }
    }

    public List<MineableAsteroid> GetAllMineableAsteroids()
    {
        var mineableAsteroids = new List<MineableAsteroid>();

        if (useObjectPooling)
        {
            foreach (var pooledAsteroid in spawnedAsteroids)
            {
                if (pooledAsteroid != null)
                {
                    var mineable = pooledAsteroid.GetComponent<MineableAsteroid>();
                    if (mineable != null)
                    {
                        mineableAsteroids.Add(mineable);
                    }
                }
            }
        }
        else
        {
            // Für traditionelle Asteroiden
            foreach (var mineable in GetComponentsInChildren<MineableAsteroid>())
            {
                if (mineable != null)
                {
                    mineableAsteroids.Add(mineable);
                }
            }
        }

        return mineableAsteroids;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Gürtel-Grenzen zeichnen
        Gizmos.color = Color.yellow;
        DrawCircle(transform.position, innerRadius, Vector3.up);
        DrawCircle(transform.position, outerRadius, Vector3.up);
        
        // Gürtel-Dicke visualisieren
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        DrawCircle(transform.position + Vector3.up * beltThickness, innerRadius, Vector3.up);
        DrawCircle(transform.position + Vector3.up * beltThickness, outerRadius, Vector3.up);
        DrawCircle(transform.position - Vector3.up * beltThickness, innerRadius, Vector3.up);
        DrawCircle(transform.position - Vector3.up * beltThickness, outerRadius, Vector3.up);
        
        // LOD-Distanzen anzeigen
        if (enableLOD)
        {
            Gizmos.color = Color.green;
            foreach (float distance in lodDistances)
            {
                DrawCircle(transform.position, distance, Vector3.up);
            }
        }
        
        // Sichtbarkeits-Distanz
        Gizmos.color = Color.cyan;
        DrawCircle(transform.position, visibilityDistance, Vector3.up);
    }
    
    void DrawCircle(Vector3 center, float radius, Vector3 normal, int segments = 64)
    {
        Vector3 prevPoint = center + Vector3.Cross(normal, Vector3.forward).normalized * radius;
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = (float)i / segments * 2f * Mathf.PI;
            Vector3 newPoint = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }

    //void OnGUI()
    //{
    //    if (!Debug.isDebugBuild || !Application.isPlaying) return;
        
    //    var stats = GetStats();
    //    GUILayout.BeginArea(new Rect(10, 200, 300, 150));
    //    GUILayout.BeginVertical("box");
    //    GUILayout.Label($"Asteroid Belt: {name}");
    //    GUILayout.Label($"Total Asteroids: {stats.TotalAsteroids}");
    //    GUILayout.Label($"Visible: {stats.VisibleAsteroids}");
    //    GUILayout.Label($"Pooling: {stats.UsePooling}");
    //    GUILayout.Label($"Orbital Motion: {stats.OrbitalMotion}");
    //    GUILayout.Label($"Ring: {stats.InnerRadius:F1} - {stats.OuterRadius:F1} Units");
    //    if (mainCamera != null)
    //    {
    //        float dist = Vector3.Distance(mainCamera.transform.position, transform.position);
    //        GUILayout.Label($"Camera Distance: {dist:F1}");
    //    }
    //    GUILayout.EndVertical();
    //    GUILayout.EndArea();
    //}
#endif
}