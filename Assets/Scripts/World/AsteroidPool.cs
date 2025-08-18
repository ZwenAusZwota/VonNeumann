// Assets/Scripts/World/AsteroidPool.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Object Pool f�r Asteroiden zur Performance-Optimierung
/// </summary>
public class AsteroidPool : MonoBehaviour
{
    public static AsteroidPool Instance { get; private set; }

    [Header("Pool Configuration")]
    public int initialPoolSize = 100;
    public int maxPoolSize = 1000;
    public bool expandPool = true;

    [Header("Prefab Settings")]
    public List<GameObject> asteroidPrefabs = new();
    public List<Material> asteroidMaterials = new();
    [Range(0f, 1f)]
    public float cubeChance = 0.2f;

    // Pool Management
    private Queue<PooledAsteroid> availableAsteroids = new();
    private HashSet<PooledAsteroid> activeAsteroids = new();
    private Transform poolParent;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Create pool parent object
        poolParent = new GameObject("Asteroid Pool").transform;
        poolParent.SetParent(transform);

        InitializePool();
    }

    void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewAsteroid();
        }

        Debug.Log($"AsteroidPool initialized with {initialPoolSize} asteroids");
    }

    PooledAsteroid CreateNewAsteroid()
    {
        GameObject asteroidGO = CreateAsteroidGameObject();

        // Add pooled component
        var pooledAsteroid = asteroidGO.AddComponent<PooledAsteroid>();
        pooledAsteroid.Pool = this;

        // Add mineable component
        var mineableComponent = asteroidGO.GetComponent<MineableAsteroid>();
        if (mineableComponent == null)
        {
            mineableComponent = asteroidGO.AddComponent<MineableAsteroid>();
        }

        // Initially inactive
        asteroidGO.SetActive(false);
        asteroidGO.transform.SetParent(poolParent);
        asteroidGO.tag = "Asteroid";

        availableAsteroids.Enqueue(pooledAsteroid);
        return pooledAsteroid;
    }

    GameObject CreateAsteroidGameObject()
    {
        GameObject go;

        // Use prefab or create primitive
        if (asteroidPrefabs != null && asteroidPrefabs.Count > 0)
        {
            var prefab = asteroidPrefabs[Random.Range(0, asteroidPrefabs.Count)];
            go = Instantiate(prefab);
        }
        else
        {
            // Fallback to primitive
            PrimitiveType type = Random.value < cubeChance ? PrimitiveType.Cube : PrimitiveType.Sphere;
            go = GameObject.CreatePrimitive(type);

            // Apply material
            var renderer = go.GetComponent<MeshRenderer>();
            if (asteroidMaterials != null && asteroidMaterials.Count > 0)
            {
                renderer.material = asteroidMaterials[Random.Range(0, asteroidMaterials.Count)];
            }
            else
            {
                // Default gray material
                var mat = new Material(Shader.Find("Standard"));
                mat.color = new Color32(120, 120, 120, 255);
                renderer.material = mat;
            }
        }

        // Ensure collider
        if (!go.GetComponent<Collider>())
        {
            go.AddComponent<MeshCollider>();
        }

        go.tag = "Asteroid";
        return go;
    }

    public PooledAsteroid GetAsteroid()
    {
        PooledAsteroid asteroid;

        if (availableAsteroids.Count > 0)
        {
            asteroid = availableAsteroids.Dequeue();
        }
        else if (expandPool && activeAsteroids.Count < maxPoolSize)
        {
            asteroid = CreateNewAsteroid();
            availableAsteroids.Dequeue(); // Remove from available since we're using it
        }
        else
        {
            Debug.LogWarning("AsteroidPool: No asteroids available and pool at max capacity");
            return null;
        }

        activeAsteroids.Add(asteroid);
        asteroid.gameObject.SetActive(true);
        return asteroid;
    }

    public void ReturnAsteroid(PooledAsteroid asteroid)
    {
        if (asteroid == null) return;

        if (activeAsteroids.Remove(asteroid))
        {
            asteroid.gameObject.SetActive(false);
            asteroid.transform.SetParent(poolParent);
            asteroid.ResetAsteroid();
            availableAsteroids.Enqueue(asteroid);
        }
    }

    public void ConfigureAsteroid(PooledAsteroid asteroid, Vector3 position, Vector2 sizeRange, Transform parent)
    {
        if (asteroid == null) return;

        // Position and scale
        asteroid.transform.position = position;
        asteroid.transform.rotation = Random.rotation;

        float size = Random.Range(sizeRange.x, sizeRange.y);
        asteroid.transform.localScale = Vector3.one * size;

        // Configure mineable component
        var mineable = asteroid.GetComponent<MineableAsteroid>();
        if (mineable != null)
        {
            string materialId = MaterialDatabase.GetRandomId();
            float startUnits = Random.Range(800f, 2000f);
            mineable.Configure(materialId, startUnits);
        }

        // Set parent (but keep world position)
        asteroid.transform.SetParent(parent, true);
    }

    // Pool statistics
    public int AvailableCount => availableAsteroids.Count;
    public int ActiveCount => activeAsteroids.Count;
    public int TotalCount => AvailableCount + ActiveCount;

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

#if UNITY_EDITOR
    //void OnGUI()
    //{
    //    if (!Debug.isDebugBuild) return;
        
    //    GUILayout.BeginArea(new Rect(Screen.width - 200, 10, 190, 100));
    //    GUILayout.BeginVertical("box");
    //    GUILayout.Label("Asteroid Pool");
    //    GUILayout.Label($"Available: {AvailableCount}");
    //    GUILayout.Label($"Active: {ActiveCount}");
    //    GUILayout.Label($"Total: {TotalCount}");
    //    GUILayout.EndVertical();
    //    GUILayout.EndArea();
    //}
#endif
}