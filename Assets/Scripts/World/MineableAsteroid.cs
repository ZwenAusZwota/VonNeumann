// Assets/Scripts/World/MineableAsteroid.cs
using UnityEngine;
using System;

/// <summary>
/// Verschiedene Modi für die visuelle Schrumpfung von Asteroiden
/// </summary>
public enum ShrinkingMode
{
    /// <summary>Lineare Schrumpfung - gleichmäßige Größenänderung (realistischer)</summary>
    Linear,
    /// <summary>Volumen-basierte Schrumpfung - mathematisch korrekte Kubikwurzel-Skalierung</summary>
    Volume,
    /// <summary>Quadratische Schrumpfung - Kompromiss zwischen Linear und Volume</summary>
    Quadratic
}

[RequireComponent(typeof(Transform))]
public class MineableAsteroid : MonoBehaviour
{
    [Header("Material Properties")]
    public string materialId = "Iron";
    public float startUnits = 1_000f;     // Gesamtvorrat beim Spawnen

    [Header("Navigation")]
    [Tooltip("Sphärischer Navigationsradius in Unity-Units (Durchmesser = 2*r). Welt-Radius!")]
    public float navSphereRadiusUU = 0f;

    [Tooltip("Unsichtbare Nav-Sphäre als Trigger-Collider (wird automatisch erzeugt/aktualisiert).")]
    public SphereCollider navSphereCollider;

    [Header("Visual Degradation")]
    [Tooltip("Optional: Wenn gesetzt, wird NUR diese Transform für die visuelle Größe skaliert (z. B. dein Mesh-Root). Ist sie null, wird das GameObject selbst skaliert.")]
    public Transform visualRoot;

    [Tooltip("Minimale visuelle Größe (als Faktor), damit der Asteroid nie auf 0 schrumpft, bis er entfernt wird.")]
    [Range(0.05f, 1f)] public float minimumScale = 0.2f;

    [Tooltip("Wenn true, verringert sich die visuelle Größe mit abgebauten Einheiten.")]
    public bool visuallyDegrade = true;

    [Header("Shrinking Behavior")]
    [Tooltip("Art der Schrumpfung: Linear = gleichmäßige Größenänderung, Volume = mathematisch korrekte Volumen-Skalierung")]
    public ShrinkingMode shrinkingMode = ShrinkingMode.Linear;

    [Tooltip("Geschwindigkeit der visuellen Schrumpfung (1.0 = sofort, 0.1 = sehr langsam)")]
    [Range(0.1f, 2.0f)] public float shrinkingSpeed = 1.0f;

    [Header("Mining")]
    [Tooltip("Einheiten pro Sekunde, die beim Mining maximal entnommen werden.")]
    public float maxExtractPerSecond = 20f;

    [Tooltip("Optionales Partikeleffekt beim Mining.")]
    public ParticleSystem miningEffect;

    [Tooltip("Optionaler Sound beim Mining.")]
    public AudioClip miningSound;

    // Zustand
    private float unitsRemaining;
    private Vector3 startScaleRoot;
    private Vector3 startScaleVisual;
    private bool isMining;
    private AudioSource audioSource;
    private Vector3 targetScale; // Ziel-Skalierung für sanfte Übergänge

    // Events
    public event Action<float> OnUnitsMined;           // Einheiten die abgebaut wurden
    public event Action<float> OnUnitsRemainChanged;   // Verbleibende Einheiten geändert
    public event Action OnFullyMined;                  // Vollständig abgebaut
    public event Action OnStartMining;                 // Mining begonnen
    public event Action OnStopMining;                  // Mining beendet

    void Awake()
    {
        // Anfangsskalen merken (Root + optional VisualRoot)
        if (startScaleRoot == Vector3.zero) startScaleRoot = transform.localScale;

        if (visualRoot == null)
        {
            visualRoot = transform; // fallback: ganze Instanz skalieren
        }
        if (startScaleVisual == Vector3.zero) startScaleVisual = visualRoot.localScale;

        EnsureNavSphereCollider();   // stellt navSphereCollider bereit
        ApplyNavSphereRadius();      // überträgt navSphereRadiusUU → Collider.radius (lokal)

        ResetToStartValues();
        ApplyRenderMaterial();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && miningSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D Sound
        }
    }

    void OnValidate()
    {
        // Im Editor sicherstellen, dass die Nav-Sphäre angepasst wird
        EnsureNavSphereCollider();
        ApplyNavSphereRadius();
    }

    /// <summary>Setzt den Asteroiden auf Ausgangswerte zurück (für Object Pool)</summary>
    public void ResetToStartValues()
    {
        unitsRemaining = startUnits;

        isMining = false;
        if (miningEffect != null && miningEffect.isPlaying) miningEffect.Stop();
        if (audioSource != null && audioSource.isPlaying) audioSource.Stop();

        // Ursprungs-Skalen herstellen
        transform.localScale = startScaleRoot;
        if (visualRoot != null) visualRoot.localScale = startScaleVisual;
        
        // Ziel-Skalierung auf Anfangswert setzen
        targetScale = (visualRoot != null) ? startScaleVisual : startScaleRoot;

        UpdateVisualScale();
        OnUnitsRemainChanged?.Invoke(unitsRemaining);
    }

    public float RemainingPercentage => Mathf.Approximately(startUnits, 0f) ? 0f : Mathf.Clamp01(unitsRemaining / startUnits);
    public bool IsFullyMined => unitsRemaining <= 0f;

    public float UnitsRemaining => unitsRemaining;
    public float StartUnits => startUnits;
    public string MaterialId => materialId;

    /// <summary>
    /// Ändert den Schrumpfungsmodus zur Laufzeit
    /// </summary>
    public void SetShrinkingMode(ShrinkingMode newMode)
    {
        shrinkingMode = newMode;
        UpdateVisualScale(); // Sofortige Aktualisierung der visuellen Darstellung
    }

    /// <summary>
    /// Ändert die Schrumpfungsgeschwindigkeit zur Laufzeit
    /// </summary>
    public void SetShrinkingSpeed(float newSpeed)
    {
        shrinkingSpeed = Mathf.Clamp(newSpeed, 0.1f, 2.0f);
    }

    /// <summary>(Re-)Konfiguration über den Pool/Spawner.</summary>
    public void Configure(string newMaterialId, float newStartUnits)
    {
        materialId = newMaterialId;
        startUnits = newStartUnits;

        if (transform.localScale != Vector3.zero) startScaleRoot = transform.localScale;
        if (visualRoot == null) visualRoot = transform;
        if (visualRoot.localScale != Vector3.zero) startScaleVisual = visualRoot.localScale;

        // Nav-Sphäre sicherstellen/aktualisieren
        EnsureNavSphereCollider();
        ApplyNavSphereRadius();

        ResetToStartValues();
        ApplyRenderMaterial();
    }

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
            // Reihenfolge: Effekte stoppen -> Event -> entfernen
            StopMining();
            OnFullyMined?.Invoke();
            RemoveAsteroid();
        }

        OnUnitsMined?.Invoke(granted);
        return granted;
    }

    public void StartMining()
    {
        if (IsFullyMined || isMining) return;

        isMining = true;
        OnStartMining?.Invoke();

        if (miningEffect != null && !miningEffect.isPlaying) miningEffect.Play();

        if (audioSource != null && miningSound != null && !audioSource.isPlaying)
        {
            audioSource.clip = miningSound;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    public void StopMining()
    {
        if (!isMining) return;

        OnStopMining?.Invoke();

        if (miningEffect != null && miningEffect.isPlaying) miningEffect.Stop();
        if (audioSource != null && audioSource.isPlaying) audioSource.Stop();

        isMining = false;
    }

    private void UpdateVisualScale()
    {
        if (!visuallyDegrade) return;

        // Berechne die Ziel-Skalierung basierend auf dem Schrumpfungsmodus
        float scaleFactor = CalculateScaleFactor();
        
        // Bestimme die Ziel-Skalierung
        Vector3 baseScale = (visualRoot != null) ? startScaleVisual : startScaleRoot;
        targetScale = baseScale * scaleFactor;

        // Sofortige Anwendung der Skalierung (für sofortige Reaktion)
        ApplyScale(targetScale);

        // Nav-Sphäre mitführen (Welt-Radius konstant halten)
        ApplyNavSphereRadius();
    }

    /// <summary>
    /// Berechnet den Skalierungsfaktor basierend auf dem gewählten Schrumpfungsmodus
    /// </summary>
    private float CalculateScaleFactor()
    {
        float remainingPercentage = Mathf.Max(RemainingPercentage, minimumScale);
        
        switch (shrinkingMode)
        {
            case ShrinkingMode.Linear:
                // Lineare Schrumpfung - gleichmäßige Größenänderung
                return remainingPercentage;
                
            case ShrinkingMode.Volume:
                // Volumen-basierte Schrumpfung - mathematisch korrekte Kubikwurzel
                return Mathf.Pow(remainingPercentage, 1f / 3f);
                
            case ShrinkingMode.Quadratic:
                // Quadratische Schrumpfung - Kompromiss zwischen Linear und Volume
                return Mathf.Pow(remainingPercentage, 1f / 2f);
                
            default:
                return remainingPercentage;
        }
    }

    /// <summary>
    /// Wendet die Skalierung auf das entsprechende Transform an
    /// </summary>
    private void ApplyScale(Vector3 scale)
    {
        if (visualRoot != null)
            visualRoot.localScale = scale;
        else
            transform.localScale = scale;
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

    /* ===================== Nav-Sphäre ===================== */

    /// <summary>Sicherstellen, dass ein unsichtbarer SphereCollider (Trigger) existiert.</summary>
    public void EnsureNavSphereCollider()
    {
        if (navSphereCollider == null)
        {
            navSphereCollider = GetComponent<SphereCollider>();
            if (navSphereCollider == null)
                navSphereCollider = gameObject.AddComponent<SphereCollider>();
        }

        navSphereCollider.isTrigger = true;
        // Optional: auf eigenen Layer legen, falls gewünscht.
    }

    /// <summary>
    /// Überträgt den gewünschten **Welt-Radius** (navSphereRadiusUU) in den **lokalen** Collider-Radius.
    /// </summary>
    public void ApplyNavSphereRadius()
    {
        EnsureNavSphereCollider();

        // Falls noch kein sinnvoller Welt-Radius vergeben wurde, aus aktueller Größe abschätzen:
        if (navSphereRadiusUU <= 0f)
        {
            // Wir nehmen den größeren der Achsen; bei VisualRoot-Skalierung bleibt der Root-Scale unverändert.
            float approxDiameter = Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
            navSphereRadiusUU = Mathf.Max(0.01f, approxDiameter * 0.5f);
        }

        // SphereCollider.radius ist **lokal** → Welt-Radius / maxLossyScale (Root)
        float scale = Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
        float localRadius = Mathf.Max(0.01f, navSphereRadiusUU / Mathf.Max(1e-6f, scale));

        navSphereCollider.center = Vector3.zero;
        navSphereCollider.radius = localRadius;
    }

    /// <summary>
    /// (Für Pool/LOD) Bestimme einen robusten **Welt-Radius** aus der Hierarchie
    /// und synchronisiere anschließend den lokalen Collider-Radius.
    /// inflateFactor >= 1 vergrößert den ermittelten Radius (Sicherheitsmarge).
    /// </summary>
    public void RecalculateNavSphereRadiusFromHierarchy(float inflateFactor = 1f)
    {
        inflateFactor = Mathf.Max(1f, inflateFactor);

        // 1) Render-Bounds bevorzugen
        float radiusWorld = 0f;
        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers != null && renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            radiusWorld = Mathf.Max(radiusWorld, b.extents.magnitude);
        }

        // 2) Fallback: Collider-Bounds
        if (radiusWorld <= 0f)
        {
            var colliders = GetComponentsInChildren<Collider>(true);
            if (colliders != null && colliders.Length > 0)
            {
                Bounds b = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++) b.Encapsulate(colliders[i].bounds);
                radiusWorld = Mathf.Max(radiusWorld, b.extents.magnitude);
            }
        }

        // 3) Fallback: lossyScale-Heuristik
        if (radiusWorld <= 0f)
        {
            float approxDiameter = Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z));
            radiusWorld = Mathf.Max(0.01f, approxDiameter * 0.5f);
        }

        radiusWorld = Mathf.Max(0.01f, radiusWorld * inflateFactor);

        // Welt-Radius setzen (max, damit manuell gesetzte Radien nicht kleiner gemacht werden)
        navSphereRadiusUU = Mathf.Max(navSphereRadiusUU, radiusWorld);

        // Collider auf den Welt-Radius abstimmen
        ApplyNavSphereRadius();
    }

    /* ===================== Entfernen ===================== */

    private void RemoveAsteroid()
    {
        // Safety: Collider & Effekte aus
        if (navSphereCollider != null) navSphereCollider.enabled = false;
        if (miningEffect != null && miningEffect.isPlaying) miningEffect.Stop();
        if (audioSource != null && audioSource.isPlaying) audioSource.Stop();

        // Wenn du später ein Pooling verwendest, kannst du hier stattdessen zurück in den Pool geben.
        // z.B.: var pooled = GetComponent<PooledAsteroid>(); pooled?.Despawn();
        Destroy(gameObject);
    }

    /* ===================== Rendering ===================== */

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
