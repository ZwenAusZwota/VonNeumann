using UnityEngine;
using System;
using Random = UnityEngine.Random;

/// <summary>
/// Erweiterte Version von MineableAsteroid mit Pool-Unterstützung.
/// Enthält Material-Vorrat und kann Einheiten abgeben.
/// Skaliert dabei physisch, Volumen ∝ r³.
/// </summary>
[RequireComponent(typeof(Transform))]
public class MineableAsteroid : MonoBehaviour
{
    [Header("Material Properties")]
    public string materialId = "Iron";
    public float startUnits = 1_000f;     // Gesamtvorrat beim Spawnen

    [Header("Visual Settings")]
    [Tooltip("Minimum-Skalierung bevor der Asteroid als 'leer' gilt")]
    [Range(0.01f, 0.3f)]
    public float minimumScale = 0.1f;

    [Tooltip("Soll der Asteroid visuell schrumpfen beim Abbauen?")]
    public bool visuallyDegrade = true;

    [Header("Effects")]
    [Tooltip("Partikel-Effekt beim Abbauen (optional)")]
    public ParticleSystem miningEffect;

    [Tooltip("Sound beim Abbauen (optional)")]
    public AudioClip miningSound;

    // Private Felder
    private float unitsRemaining;
    private Vector3 startScale;
    private AudioSource audioSource;

    // Properties
    public float UnitsRemaining => unitsRemaining;
    public float StartUnits => startUnits;
    public bool IsFullyMined => unitsRemaining <= 0f;
    public float RemainingPercentage => startUnits > 0 ? unitsRemaining / startUnits : 0f;

    // Events
    public event Action<float> OnUnitsMined;           // Einheiten die abgebaut wurden
    public event Action<float> OnUnitsRemainChanged;   // Verbleibende Einheiten geändert
    public event Action OnFullyMined;                  // Vollständig abgebaut
    public event Action OnStartMining;                 // Mining begonnen
    public event Action OnStopMining;                  // Mining beendet

    void Awake()
    {
        ResetToStartValues();
        startScale = transform.localScale;

        // AudioSource für Mining-Sounds
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && miningSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D Sound
        }
    }

    /// <summary>
    /// Setzt den Asteroiden auf Ausgangswerte zurück (für Object Pool)
    /// </summary>
    public void ResetToStartValues()
    {
        unitsRemaining = startUnits;

        // Scale zurücksetzen falls bereits gesetzt
        if (startScale != Vector3.zero)
            transform.localScale = startScale;
    }

    /// <summary>
    /// Konfiguriert den Asteroiden mit neuen Werten
    /// </summary>
    public void Configure(string newMaterialId, float newStartUnits)
    {
        materialId = newMaterialId;
        startUnits = newStartUnits;
        ResetToStartValues();
    }

    /// <summary>
    /// Liefert 0 … requested Einheiten, je nachdem was noch übrig ist.
    /// </summary>
    public float RemoveUnits(float requested)
    {
        if (requested <= 0f || IsFullyMined) return 0f;

        float granted = Mathf.Min(requested, unitsRemaining);
        float previousUnits = unitsRemaining;
        unitsRemaining -= granted;

        // Events auslösen
        OnUnitsMined?.Invoke(granted);
        OnUnitsRemainChanged?.Invoke(unitsRemaining);

        // Visuelles Update
        if (visuallyDegrade)
            UpdateVisualScale();

        // Effekte abspielen
        PlayMiningEffects();

        // Check if fully mined
        if (IsFullyMined && previousUnits > 0f)
        {
            OnFullyMined?.Invoke();
            HandleFullyMined();
        }

        return granted;
    }

    /// <summary>
    /// Beginnt den Mining-Prozess (für kontinuierliches Mining)
    /// </summary>
    public void StartMining()
    {
        OnStartMining?.Invoke();

        // Mining-Effekte starten
        if (miningEffect != null && !miningEffect.isPlaying)
            miningEffect.Play();
    }

    /// <summary>
    /// Beendet den Mining-Prozess
    /// </summary>
    public void StopMining()
    {
        OnStopMining?.Invoke();

        // Mining-Effekte stoppen
        if (miningEffect != null && miningEffect.isPlaying)
            miningEffect.Stop();
    }

    /// <summary>
    /// Aktualisiert die visuelle Skalierung basierend auf verbleibenden Einheiten
    /// </summary>
    void UpdateVisualScale()
    {
        if (!visuallyDegrade) return;

        float scaleFactor = Mathf.Max(RemainingPercentage, minimumScale);

        // Skaliere Radius = ∛(Volumen-Faktor) für realistische Volumendarstellung
        float volumeScale = Mathf.Pow(scaleFactor, 1f / 3f);
        transform.localScale = startScale * volumeScale;
    }

    /// <summary>
    /// Spielt Mining-Effekte ab
    /// </summary>
    void PlayMiningEffects()
    {
        // Partikel-Effekt
        if (miningEffect != null && !miningEffect.isPlaying)
        {
            miningEffect.Play();
        }

        // Sound-Effekt
        if (audioSource != null && miningSound != null)
        {
            audioSource.pitch = Random.Range(0.8f, 1.2f); // Variation
            audioSource.PlayOneShot(miningSound);
        }
    }

    /// <summary>
    /// Behandelt vollständiges Abbauen
    /// </summary>
    void HandleFullyMined()
    {
        // Effekte stoppen
        StopMining();

        // Für Object Pool: Nicht direkt zerstören
        var pooledComponent = GetComponent<PooledAsteroid>();
        if (pooledComponent != null)
        {
            // Pool-System übernimmt
            return;
        }

        // Fallback: Traditionelles Zerstören nach kurzer Verzögerung
        Invoke(nameof(DestroyAsteroid), 1f);
    }

    /// <summary>
    /// Zerstört den Asteroiden (Fallback wenn kein Pool verwendet wird)
    /// </summary>
    void DestroyAsteroid()
    {
        Destroy(gameObject);
    }

    /// <summary>
    /// Gibt geschätzte Mining-Zeit zurück
    /// </summary>
    public float GetEstimatedMiningTime(float miningRate)
    {
        return miningRate > 0 ? unitsRemaining / miningRate : float.MaxValue;
    }

    /// <summary>
    /// Prüft ob genügend Material für bestimmte Menge vorhanden ist
    /// </summary>
    public bool HasSufficientMaterial(float requiredAmount)
    {
        return unitsRemaining >= requiredAmount;
    }

    /// <summary>
    /// Debug-Anzeige
    /// </summary>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        // Material-Info anzeigen
#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2f,
            $"{materialId}: {unitsRemaining:F0}/{startUnits:F0}"
        );
#endif
    }

    // Für Mining-Animation/Feedback
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("MiningBeam"))
        {
            StartMining();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("MiningBeam"))
        {
            StopMining();
        }
    }
}