using UnityEngine;

/// <summary>
/// Wrapper-Klasse für Asteroiden im Object Pool.
/// Verwaltet die Rückgabe an den Pool und das Zurücksetzen der Eigenschaften.
/// </summary>
[RequireComponent(typeof(MineableAsteroid))]
public class PooledAsteroid : MonoBehaviour
{
    private AsteroidPool pool;
    private MineableAsteroid mineableComponent;
    private Renderer[] renderers;
    private Vector3 originalScale;

    [Header("Pool Settings")]
    [Tooltip("Automatisch zum Pool zurückkehren wenn zerstört?")]
    public bool autoReturnOnDestroy = true;

    [Tooltip("Automatisch zum Pool zurückkehren wenn leer abgebaut?")]
    public bool autoReturnOnDepleted = true;

    void Awake()
    {
        mineableComponent = GetComponent<MineableAsteroid>();
        renderers = GetComponentsInChildren<Renderer>();
        originalScale = transform.localScale;
    }

    void OnEnable()
    {
        // Beim Aktivieren aus dem Pool Event-Handler registrieren
        if (mineableComponent != null)
        {
            // Falls MineableAsteroid ein Event für "vollständig abgebaut" hat
            // mineableComponent.OnFullyMined += HandleFullyMined;
        }
    }

    void OnDisable()
    {
        // Event-Handler deregistrieren
        if (mineableComponent != null)
        {
            // mineableComponent.OnFullyMined -= HandleFullyMined;
        }
    }

    /// <summary>
    /// Setzt die Pool-Referenz
    /// </summary>
    public void SetPool(AsteroidPool asteroidPool)
    {
        pool = asteroidPool;
    }

    /// <summary>
    /// Setzt den Asteroiden auf Ausgangszustand zurück
    /// </summary>
    public void ResetAsteroid()
    {
        // Transform zurücksetzen
        transform.localScale = originalScale;
        transform.rotation = Quaternion.identity;
        transform.SetParent(null);

        // Renderer aktivieren (falls deaktiviert)
        foreach (var renderer in renderers)
        {
            if (renderer != null)
                renderer.enabled = true;
        }

        // MineableAsteroid zurücksetzen
        if (mineableComponent != null)
        {
            mineableComponent.ResetToStartValues();
        }

        // Collider reaktivieren
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = true;
        }
    }

    /// <summary>
    /// Gibt den Asteroiden manuell an den Pool zurück
    /// </summary>
    public void ReturnToPool()
    {
        if (pool != null)
        {
            pool.ReturnAsteroid(this);
        }
        else
        {
            // Fallback: normales Destroy falls kein Pool verfügbar
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Behandelt vollständiges Abbauen des Asteroiden
    /// </summary>
    void HandleFullyMined()
    {
        if (autoReturnOnDepleted)
        {
            // Kurze Verzögerung für visuelle Effekte
            Invoke(nameof(ReturnToPool), 0.1f);
        }
    }

    /// <summary>
    /// Update-Überprüfung ob Asteroid vollständig abgebaut wurde
    /// </summary>
    void Update()
    {
        // Überprüfen ob der Asteroid vollständig abgebaut wurde
        if (autoReturnOnDepleted && mineableComponent != null &&
            mineableComponent.UnitsRemaining <= 0f)
        {
            HandleFullyMined();
        }
    }

    /// <summary>
    /// Überschreibt Destroy-Verhalten für Pool-Rückgabe
    /// </summary>
    void OnDestroy()
    {
        // Nur wenn das GameObject wirklich zerstört wird (nicht nur deaktiviert)
        if (autoReturnOnDestroy && pool != null && gameObject.activeInHierarchy)
        {
            // Stattdessen zum Pool zurückgeben
            ReturnToPool();
        }
    }

    /// <summary>
    /// Setzt sichtbare Eigenschaften (für visuelle Effekte)
    /// </summary>
    public void SetVisible(bool visible)
    {
        foreach (var renderer in renderers)
        {
            if (renderer != null)
                renderer.enabled = visible;
        }
    }

    /// <summary>
    /// Aktiviert/deaktiviert Kollision
    /// </summary>
    public void SetCollisionEnabled(bool enabled)
    {
        var collider = GetComponent<Collider>();
        if (collider != null)
            collider.enabled = enabled;
    }

    /// <summary>
    /// Gibt Informationen über den Asteroid zurück
    /// </summary>
    public AsteroidInfo GetInfo()
    {
        return new AsteroidInfo
        {
            MaterialId = mineableComponent?.materialId ?? "Unknown",
            UnitsRemaining = mineableComponent?.UnitsRemaining ?? 0f,
            StartUnits = mineableComponent?.startUnits ?? 0f,
            Position = transform.position,
            Scale = transform.localScale.x,
            IsActive = gameObject.activeInHierarchy
        };
    }
}

/// <summary>
/// Hilfsstruktur für Asteroid-Informationen
/// </summary>
[System.Serializable]
public struct AsteroidInfo
{
    public string MaterialId;
    public float UnitsRemaining;
    public float StartUnits;
    public Vector3 Position;
    public float Scale;
    public bool IsActive;

    public float DepletionPercentage => StartUnits > 0 ? (StartUnits - UnitsRemaining) / StartUnits : 0f;
}