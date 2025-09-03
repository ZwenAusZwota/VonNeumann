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

    [Tooltip("Minimale visuelle Größe (als Faktor), damit der Asteroid nie auf 0 schrumpft.")]
    [Range(0.05f, 1f)]
    public float minimumScale = 0.2f;

    [Tooltip("Wenn true, verringert sich die visuelle Größe mit abgebauten Einheiten.")]
    public bool visuallyDegrade = true;

    [Header("Mining")]
    [Tooltip("Einheiten pro Sekunde, die beim Mining maximal entnommen werden.")]
    public float maxExtractPerSecond = 20f;

    [Tooltip("Optionaler Partikeleffekt beim Mining.")]
    public ParticleSystem miningEffect;

    [Tooltip("Optionaler Sound beim Mining.")]
    public AudioClip miningSound;

    // Zustand
    private float unitsRemaining;
    private Vector3 startScale;
    private bool isMining;
    private AudioSource audioSource;

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

        ApplyRenderMaterial(); // Zentrale Materialzuweisung
        // AudioSource für Mining-Sounds
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && miningSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D Sound
        }
    }

    /// <summary>Setzt den Asteroiden auf Ausgangswerte zurück (für Object Pool)</summary>
    public void ResetToStartValues()
    {
        unitsRemaining = startUnits;

        // Mining stoppen
        isMining = false;
        if (miningEffect != null && miningEffect.isPlaying) miningEffect.Stop();
        if (audioSource != null && audioSource.isPlaying) audioSource.Stop();

        UpdateVisualScale();
        OnUnitsRemainChanged?.Invoke(unitsRemaining);
    }

    /// <summary>Prozentualer Restbestand (0…1).</summary>
    public float RemainingPercentage => Mathf.Approximately(startUnits, 0f) ? 0f : Mathf.Clamp01(unitsRemaining / startUnits);

    /// <summary>Ob der Asteroid vollständig abgebaut ist.</summary>
    public bool IsFullyMined => unitsRemaining <= 0f;

    // ─── NEU: öffentliche Read-only Properties für Fremdcode (z. B. ProbeMiner) ───
    public float UnitsRemaining => unitsRemaining;
    public float StartUnits => startUnits;
    public string MaterialId => materialId;

    /// <summary>(Re-)Konfiguration über den Pool/Spawner.</summary>
    public void Configure(string newMaterialId, float newStartUnits)
    {
        materialId = newMaterialId;
        startUnits = newStartUnits;
        ResetToStartValues();

        ApplyRenderMaterial(); // Material nach neuer ID anwenden
    }

    /// <summary>Liefert 0 … requested Einheiten, je nachdem was noch übrig ist.</summary>
    public float RemoveUnits(float requested)
    {
        if (requested <= 0f || IsFullyMined) return 0f;

        float granted = Mathf.Min(requested, unitsRemaining);
        float previous = unitsRemaining;
        unitsRemaining -= granted;

        if (!Mathf.Approximately(previous, unitsRemaining))
        {
            UpdateVisualScale();
            OnUnitsRemainChanged?.Invoke(unitsRemaining);
        }

        if (IsFullyMined)
        {
            OnFullyMined?.Invoke();
            StopMining();
        }

        OnUnitsMined?.Invoke(granted);
        return granted;
    }

    /// <summary>Startet den Mining-Prozess (Effekte/Sounds)</summary>
    public void StartMining()
    {
        if (IsFullyMined) return;
        if (isMining) return;

        isMining = true;
        OnStartMining?.Invoke();

        if (miningEffect != null && !miningEffect.isPlaying)
            miningEffect.Play();

        if (audioSource != null && miningSound != null && !audioSource.isPlaying)
        {
            audioSource.clip = miningSound;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    /// <summary>Beendet den Mining-Prozess</summary>
    public void StopMining()
    {
        OnStopMining?.Invoke();

        if (miningEffect != null && miningEffect.isPlaying)
            miningEffect.Stop();

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();

        isMining = false;
    }

    /// <summary>Aktualisiert die visuelle Skalierung basierend auf verbleibenden Einheiten</summary>
    void UpdateVisualScale()
    {
        if (!visuallyDegrade) return;

        float scaleFactor = Mathf.Max(RemainingPercentage, minimumScale);

        // Skaliere Radius = ∛(Volumen-Faktor) für realistische Volumendarstellung
        float volumeScale = Mathf.Pow(scaleFactor, 1f / 3f);
        transform.localScale = startScale * volumeScale;
    }

    void Update()
    {
        if (!isMining || IsFullyMined) return;

        float extractThisFrame = maxExtractPerSecond * Time.deltaTime;
        RemoveUnits(extractThisFrame);
    }

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

    // --- Zentrale Render-Materialzuweisung aus MaterialDatabase ---
    private void ApplyRenderMaterial()
    {
        var mat = MaterialDatabase.GetRenderMaterial(materialId);
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r != null) r.sharedMaterial = mat;
        }
    }
}
