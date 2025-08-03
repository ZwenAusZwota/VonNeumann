using System.Collections.Generic;
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

    // AsteroidBelt.cs  (oben bei den Inspector-Feldern)
    [Header("Rendering")]
    [Tooltip("Optional Liste von Materialien, aus denen jeder Asteroid zufällig eines bekommt.")]
    public List<Material> asteroidMaterials = new();


    /*──────────────────────── Internals */
    readonly List<Renderer> asteroidRenderers = new();
    bool spawned;

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
        if (!spawned)
        {
            SpawnAsteroids();
            spawned = true;
        }
    }

    void Update() => UpdateVisibility();

    /*──────────────────────── Implementation */
    void SpawnAsteroids()
    {
        if (outerRadius <= innerRadius)
            outerRadius = innerRadius + 1f;

        bool hasMats = asteroidMaterials != null && asteroidMaterials.Count > 0;

        for (int i = 0; i < asteroidCount; i++)
        {
            // Position in ring (XZ plane) with slight vertical spread
            float angleDeg = Random.Range(0f, 360f);
            float radius = Random.Range(innerRadius, outerRadius);
            Vector3 localPos = new Vector3(Mathf.Cos(angleDeg * Mathf.Deg2Rad),
                                           Random.Range(-2f, 2f),
                                           Mathf.Sin(angleDeg * Mathf.Deg2Rad)) * radius;
            Material chosenMat = hasMats
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

                // Apply a neutral gray material to primitives only (prefabs provide their own)
                /*var mat = new Material(Shader.Find("Standard"))
                {
                    color = new Color32(120, 120, 120, 255)
                };
                go.GetComponent<MeshRenderer>().material = mat;*/

                var rend = go.GetComponent<MeshRenderer>();
                if (chosenMat != null)
                    rend.material = chosenMat;
                else
                    {
                    /* Fallback-Standard-Grau, falls gar kein Material in der Liste */
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

            // Collider & tag (add collider if prefab lacks one)
            if (!go.TryGetComponent(out Collider col))
                col = go.AddComponent<MeshCollider>();
            col.isTrigger = false;
            go.tag = "Asteroid";
            //if (!go.TryGetComponent<MineableAsteroid>(out _)) { }
            var mine = go.AddComponent<MineableAsteroid>();
            mine.materialId = MaterialRegistry.GetRandomId();
            mine.startUnits = Random.Range(800, 2000);

            // Cache renderers for visibility toggling
            foreach (var rend in go.GetComponentsInChildren<Renderer>())
                asteroidRenderers.Add(rend);
        }
    }

    void UpdateVisibility()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        float dist = Vector3.Distance(cam.transform.position, transform.position);
        bool shouldRender = dist <= visibilityDistance;

        foreach (var rend in asteroidRenderers)
            if (rend && rend.enabled != shouldRender)
                rend.enabled = shouldRender;
    }

    /*──────────────────────── Editor Gizmos */
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, innerRadius);
        Gizmos.DrawWireSphere(transform.position, outerRadius);
    }
#endif
}
