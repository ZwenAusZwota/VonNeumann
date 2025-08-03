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

    [Header("Visibility")]
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
    readonly List<Renderer> asteroidRenderers = new();  // Fallback für non-pooled
    bool spawned;
    AsteroidPool pool;
    Camera mainCamera;

    /*──────────────────────── Public API */
    /// <summary>
    /// Allows run‑time configuration before first Update.
    /// </summary>
    public void Init(float innerUnits, float outerUnits, int count = -1)
    {
        innerRadius = innerUnits;
        outerRadius = outerUnits;
        if (count > 0) asteroidCount = count;

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

        // Object Pool setup
        if (useObjectPooling)
        {
            SetupObjectPool();
        }

        if (!spawned)
        {
            SpawnAsteroids();
            spawned = true;
        }
    }

    void Update()
    {
        UpdateVisibility();

        if (enableLOD)
            UpdateLOD();
    }

    /*──────────────────────── Object Pool Setup */
    void SetupObjectPool()
    {
        // Pool suchen oder erstellen
        pool = AsteroidPool.Instance;

        if (pool == null)
        {
            // Pool erstellen falls nicht vorhanden
            GameObject poolGO = new GameObject("AsteroidPool");
            pool = poolGO.AddComponent<AsteroidPool>();

            // Pool-Einstellungen übertragen
            pool.initialPoolSize = preSpawnCount;
            pool.asteroidPrefabs = asteroidPrefabs;
            pool.asteroidMaterials = asteroidMaterials;
            pool.cubeChance = cubeChance;
        }
    }

    /*──────────────────────── Implementation */
    void SpawnAsteroids()
    {
        if (outerRadius <= innerRadius)
            outerRadius = innerRadius + 1f;

        // LOD-Anpassung der Asteroid-Anzahl
        int actualAsteroidCount = asteroidCount;
        if (enableLOD && mainCamera != null)
        {
            float distance = Vector3.Distance(mainCamera.transform.position, transform.position);
            float densityMultiplier = GetLODDensityMultiplier(distance);
            actualAsteroidCount = Mathf.RoundToInt(asteroidCount * densityMultiplier);
        }

        for (int i = 0; i < actualAsteroidCount; i++)
        {
            // Position in ring (XZ plane) with slight vertical spread
            float angleDeg = Random.Range(0f, 360f);
            float radius = Random.Range(innerRadius, outerRadius);
            Vector3 localPos = new Vector3(
                Mathf.Cos(angleDeg * Mathf.Deg2Rad),
                Random.Range(-2f, 2f),
                Mathf.Sin(angleDeg * Mathf.Deg2Rad)
            ) * radius;

            if (useObjectPooling && pool != null)
            {
                SpawnPooledAsteroid(localPos);
            }
            else
            {
                SpawnTraditionalAsteroid(localPos);
            }
        }

        Debug.Log($"AsteroidBelt spawned {actualAsteroidCount} asteroids (Pool: {useObjectPooling})");
    }

    /*──────────────────────── Spawning Methods */
    void SpawnPooledAsteroid(Vector3 localPos)
    {
        PooledAsteroid pooledAsteroid = pool.GetAsteroid();
        if (pooledAsteroid == null)
        {
            Debug.LogWarning("Could not get asteroid from pool, falling back to traditional spawn");
            SpawnTraditionalAsteroid(localPos);
            return;
        }

        // Position setzen
        Vector3 worldPos = transform.TransformPoint(localPos);

        // Pool-Asteroid konfigurieren
        pool.ConfigureAsteroid(pooledAsteroid, worldPos, sizeRange, transform);

        // Zur Liste hinzufügen
        spawnedAsteroids.Add(pooledAsteroid);

        // Renderer für Visibility-Management
        var renderers = pooledAsteroid.GetComponentsInChildren<Renderer>();
        asteroidRenderers.AddRange(renderers);
    }

    void SpawnTraditionalAsteroid(Vector3 localPos)
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

        // Parent & transform
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
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

        // Fallback für sehr große Distanzen
        return lodDensityMultipliers.Length > 0 ? lodDensityMultipliers[lodDensityMultipliers.Length - 1] : 0.1f;
    }

    void UpdateLOD()
    {
        if (mainCamera == null) return;

        float distance = Vector3.Distance(mainCamera.transform.position, transform.position);
        float targetDensity = GetLODDensityMultiplier(distance);
        int targetCount = Mathf.RoundToInt(asteroidCount * targetDensity);

        // Asteroids hinzufügen oder entfernen basierend auf LOD
        if (useObjectPooling)
        {
            AdjustPooledAsteroidCount(targetCount);
        }
        // Für traditional spawning würde hier eine ähnliche Logik stehen
    }

    void AdjustPooledAsteroidCount(int targetCount)
    {
        int currentCount = spawnedAsteroids.Count;

        if (currentCount < targetCount)
        {
            // Mehr Asteroiden spawnen
            int toSpawn = targetCount - currentCount;
            for (int i = 0; i < toSpawn; i++)
            {
                float angleDeg = Random.Range(0f, 360f);
                float radius = Random.Range(innerRadius, outerRadius);
                Vector3 localPos = new Vector3(
                    Mathf.Cos(angleDeg * Mathf.Deg2Rad),
                    Random.Range(-2f, 2f),
                    Mathf.Sin(angleDeg * Mathf.Deg2Rad)
                ) * radius;

                SpawnPooledAsteroid(localPos);
            }
        }
        else if (currentCount > targetCount)
        {
            // Überschüssige Asteroiden entfernen
            int toRemove = currentCount - targetCount;
            for (int i = 0; i < toRemove && spawnedAsteroids.Count > 0; i++)
            {
                var asteroid = spawnedAsteroids[spawnedAsteroids.Count - 1];
                spawnedAsteroids.RemoveAt(spawnedAsteroids.Count - 1);

                // Renderer aus Liste entfernen
                var renderers = asteroid.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    asteroidRenderers.Remove(renderer);
                }

                // Zum Pool zurückgeben
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

        // Für gepoolte Asteroiden
        foreach (var asteroid in spawnedAsteroids)
        {
            if (asteroid != null)
                asteroid.SetVisible(shouldRender);
        }

        // Für traditionelle Asteroiden (Fallback)
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
            // Alle gepoolten Asteroiden zurückgeben
            foreach (var asteroid in spawnedAsteroids)
            {
                if (asteroid != null)
                    pool.ReturnAsteroid(asteroid);
            }
            spawnedAsteroids.Clear();
        }
        else
        {
            // Traditionelle Asteroiden zerstören
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.CompareTag("Asteroid"))
                    DestroyImmediate(child.gameObject);
            }
        }

        asteroidRenderers.Clear();
        spawned = false;
    }

    void OnDestroy()
    {
        ClearAllAsteroids();
    }

    /*──────────────────────── Debug & Editor */
    public int GetActiveAsteroidCount()
    {
        return useObjectPooling ? spawnedAsteroids.Count : asteroidRenderers.Count;
    }

    public AsteroidBeltStats GetStats()
    {
        return new AsteroidBeltStats
        {
            TotalAsteroids = GetActiveAsteroidCount(),
            VisibleAsteroids = asteroidRenderers.Count(r => r != null && r.enabled),
            UsePooling = useObjectPooling,
            InnerRadius = innerRadius,
            OuterRadius = outerRadius,
            VisibilityDistance = visibilityDistance
        };
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, innerRadius);
        Gizmos.DrawWireSphere(transform.position, outerRadius);
        
        // LOD-Distanzen anzeigen
        if (enableLOD)
        {
            Gizmos.color = Color.green;
            foreach (float distance in lodDistances)
            {
                Gizmos.DrawWireSphere(transform.position, distance);
            }
        }
    }

    void OnGUI()
    {
        if (!Debug.isDebugBuild) return;
        
        var stats = GetStats();
        GUILayout.BeginArea(new Rect(220, 100, 200, 120));
        GUILayout.Label($"Belt: {name}");
        GUILayout.Label($"Total: {stats.TotalAsteroids}");
        GUILayout.Label($"Visible: {stats.VisibleAsteroids}");
        GUILayout.Label($"Pooling: {stats.UsePooling}");
        GUILayout.EndArea();
    }
#endif
}

/// <summary>
/// Statistik-Struktur für das AsteroidBelt
/// </summary>
[System.Serializable]
public struct AsteroidBeltStats
{
    public int TotalAsteroids;
    public int VisibleAsteroids;
    public bool UsePooling;
    public float InnerRadius;
    public float OuterRadius;
    public float VisibilityDistance;
}