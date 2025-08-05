using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Verwaltet einen Pool von Asteroiden-GameObjects für bessere Performance.
/// Reduziert Garbage Collection und Instantiate/Destroy-Aufrufe.
/// </summary>
public class AsteroidPool : MonoBehaviour
{
    [Header("Pool Settings")]
    [Tooltip("Anzahl der Asteroiden, die beim Start vorbereitet werden")]
    public int initialPoolSize = 100;

    [Tooltip("Maximale Pool-Größe (verhindert unbegrenztes Wachstum)")]
    public int maxPoolSize = 1000;

    [Header("Prefabs")]
    [Tooltip("Liste der Asteroiden-Prefabs (falls verwendet)")]
    public List<GameObject> asteroidPrefabs = new();

    [Tooltip("Fallback-Materialien für Primitive")]
    public List<Material> asteroidMaterials = new();

    [Tooltip("Wahrscheinlichkeit für Würfel statt Kugel (0-1)")]
    [Range(0f, 1f)]
    public float cubeChance = 0.2f;

    // Pool-Verwaltung
    private readonly Queue<PooledAsteroid> availableAsteroids = new();
    private readonly HashSet<PooledAsteroid> activeAsteroids = new();

    // Singleton für einfachen Zugriff
    public static AsteroidPool Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Pool initialisieren
        InitializePool();
    }

    /// <summary>
    /// Erstellt die anfängliche Anzahl von Asteroiden im Pool
    /// </summary>
    void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreatePooledAsteroid();
        }

        Debug.Log($"AsteroidPool initialisiert mit {initialPoolSize} Asteroiden");
    }

    /// <summary>
    /// Erstellt einen neuen Asteroiden für den Pool
    /// </summary>
    PooledAsteroid CreatePooledAsteroid()
    {
        GameObject asteroidGO;

        // Prefab oder Primitive verwenden
        if (asteroidPrefabs != null && asteroidPrefabs.Count > 0)
        {
            int prefabIndex = Random.Range(0, asteroidPrefabs.Count);
            asteroidGO = Instantiate(asteroidPrefabs[prefabIndex]);
        }
        else
        {
            // Fallback: Primitive erstellen
            PrimitiveType primitiveType = Random.value < cubeChance ? PrimitiveType.Cube : PrimitiveType.Sphere;
            asteroidGO = GameObject.CreatePrimitive(primitiveType);
        }

        // PooledAsteroid-Komponente hinzufügen
        PooledAsteroid pooledAsteroid = asteroidGO.GetComponent<PooledAsteroid>();
        if (pooledAsteroid == null)
            pooledAsteroid = asteroidGO.AddComponent<PooledAsteroid>();

        // MineableAsteroid-Komponente sicherstellen
        MineableAsteroid mineableAsteroid = asteroidGO.GetComponent<MineableAsteroid>();
        if (mineableAsteroid == null)
            mineableAsteroid = asteroidGO.AddComponent<MineableAsteroid>();

        // Collider sicherstellen
        if (!asteroidGO.TryGetComponent<Collider>(out _))
            asteroidGO.AddComponent<MeshCollider>();

        // Tag setzen
        asteroidGO.tag = "Asteroid";

        // Pool-Referenz setzen
        pooledAsteroid.SetPool(this);

        // Deaktivieren und in Pool einreihen
        asteroidGO.SetActive(false);
        availableAsteroids.Enqueue(pooledAsteroid);

        return pooledAsteroid;
    }

    /// <summary>
    /// Holt einen Asteroiden aus dem Pool
    /// </summary>
    public PooledAsteroid GetAsteroid()
    {
        PooledAsteroid asteroid;

        if (availableAsteroids.Count > 0)
        {
            asteroid = availableAsteroids.Dequeue();
        }
        else if (activeAsteroids.Count + availableAsteroids.Count < maxPoolSize)
        {
            // Pool erweitern falls nötig und unter Maximum
            asteroid = CreatePooledAsteroid();
            availableAsteroids.Dequeue(); // Wieder aus der Queue nehmen
        }
        else
        {
            Debug.LogWarning("AsteroidPool: Maximale Pool-Größe erreicht!");
            return null;
        }

        // Asteroid aktivieren
        asteroid.gameObject.SetActive(true);
        activeAsteroids.Add(asteroid);

        return asteroid;
    }

    /// <summary>
    /// Gibt einen Asteroiden an den Pool zurück
    /// </summary>
    public void ReturnAsteroid(PooledAsteroid asteroid)
    {
        if (asteroid == null) return;

        if (activeAsteroids.Contains(asteroid))
        {
            activeAsteroids.Remove(asteroid);
            asteroid.ResetAsteroid();
            asteroid.gameObject.SetActive(false);
            availableAsteroids.Enqueue(asteroid);
        }
    }

    /// <summary>
    /// Konfiguriert einen Asteroiden mit zufälligen Eigenschaften
    /// </summary>
    public void ConfigureAsteroid(PooledAsteroid asteroid, Vector3 position, Vector2 sizeRange, Transform parent = null)
    {
        if (asteroid == null) return;

        var go = asteroid.gameObject;
        var mineableComponent = go.GetComponent<MineableAsteroid>();

        // Position und Rotation
        go.transform.position = position;
        go.transform.rotation = Random.rotation;

        // Parent setzen
        if (parent != null)
            go.transform.SetParent(parent, true);

        // Größe
        float size = Random.Range(sizeRange.x, sizeRange.y);
        go.transform.localScale = Vector3.one * size;

        // Material zuweisen
        if (asteroidMaterials != null && asteroidMaterials.Count > 0)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material chosenMaterial = asteroidMaterials[Random.Range(0, asteroidMaterials.Count)];
                renderer.material = chosenMaterial;
            }
        }

        // MineableAsteroid konfigurieren
        if (mineableComponent != null)
        {
            mineableComponent.materialId = MaterialRegistry.GetRandomId();
            mineableComponent.startUnits = Random.Range(800, 2000);
            mineableComponent.ResetToStartValues(); // Neue Methode (siehe unten)
        }

        // Collider aktivieren
        var collider = go.GetComponent<Collider>();
        if (collider != null)
            collider.isTrigger = false;
    }

    /// <summary>
    /// Räumt alle aktiven Asteroiden auf (z.B. beim Szenenwechsel)
    /// </summary>
    public void ClearAllAsteroids()
    {
        // Alle aktiven Asteroiden zurückgeben
        var activeList = new List<PooledAsteroid>(activeAsteroids);
        foreach (var asteroid in activeList)
        {
            ReturnAsteroid(asteroid);
        }
    }

    /// <summary>
    /// Debug-Informationen
    /// </summary>
    void OnGUI()
    {
        if (!Debug.isDebugBuild) return;

        //GUILayout.BeginArea(new Rect(10, 100, 200, 100));
        //GUILayout.Label($"Pool Verfügbar: {availableAsteroids.Count}");
        //GUILayout.Label($"Pool Aktiv: {activeAsteroids.Count}");
        //GUILayout.Label($"Pool Gesamt: {availableAsteroids.Count + activeAsteroids.Count}");
        //GUILayout.EndArea();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}