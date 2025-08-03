using UnityEngine;

/// <summary>
/// Wrapper-Klasse f�r Asteroiden im Object Pool.
/// Verwaltet die R�ckgabe an den Pool und das Zur�cksetzen der Eigenschaften.
/// </summary>
[RequireComponent(typeof(MineableAsteroid))]
public class PooledAsteroid : MonoBehaviour
{
    private AsteroidPool pool;
    private MineableAsteroid mineableComponent;
    private Renderer[] renderers;
    private Vector3 originalScale;

    [Header("Pool Settings")]
    [Tooltip("Automatisch zum Pool zur�ckkehren wenn zerst�rt?")]
    public bool autoReturnOnDestroy = true;

    [Tooltip("Automatisch zum Pool zur�ckkehren wenn leer abgebaut?")]
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
            // Falls MineableAsteroid ein Event f�r "vollst�ndig abgebaut" hat
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
    /// Setzt den Asteroiden auf Ausgangszustand zur�ck
    /// </summary>
    public void ResetAsteroid()
    {
        // Transform zur�cksetzen
        transform.localScale = originalScale;
        transform.rotation = Quaternion.identity;
        transform.SetParent(null);

        // Renderer aktivieren (falls deaktiviert)
        foreach (var renderer in renderers)
        {
            if (renderer != null)
                renderer.enabled = true;
        }

        // MineableAsteroid zur�cksetzen
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
    /// Gibt den Asteroiden manuell an den Pool zur�ck
    /// </summary>
    public void ReturnToPool()
    {
        if (pool != null)
        {
            pool.ReturnAsteroid(this);
        }
        else
        {
            // Fallback: normales Destroy falls kein Pool verf�gbar
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Behandelt vollst�ndiges Abbauen des Asteroiden
    /// </summary>
    void HandleFullyMined()
    {
        if (autoReturnOnDepleted)
        {
            // Kurze Verz�gerung f�r visuelle Effekte
            Invoke(nameof(ReturnToPool), 0.1f);
        }
    }

    /// <summary>
    /// Update-�berpr�fung ob Asteroid vollst�ndig abgebaut wurde
    /// </summary>
    void Update()
    {
        // �berpr�fen ob der Asteroid vollst�ndig abgebaut wurde
        if (autoReturnOnDepleted && mineableComponent != null &&
            mineableComponent.UnitsRemaining <= 0f)
        {
            HandleFullyMined();
        }
    }

    /// <summary>
    /// �berschreibt Destroy-Verhalten f�r Pool-R�ckgabe
    /// </summary>
    void OnDestroy()
    {
        // Nur wenn das GameObject wirklich zerst�rt wird (nicht nur deaktiviert)
        if (autoReturnOnDestroy && pool != null && gameObject.activeInHierarchy)
        {
            // Stattdessen zum Pool zur�ckgeben
            ReturnToPool();
        }
    }

    /// <summary>
    /// Setzt sichtbare Eigenschaften (f�r visuelle Effekte)
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
    /// Gibt Informationen �ber den Asteroid zur�ck
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
/// Hilfsstruktur f�r Asteroid-Informationen
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