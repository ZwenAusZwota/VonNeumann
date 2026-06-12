using System;
using UnityEngine;

/// <summary>
/// Verschiedene Mining-Modi für Von Neumann-Sonden
/// </summary>
public enum MiningMode
{
    /// <summary>Automatische Auswahl basierend auf Asteroid-Eigenschaften</summary>
    Auto,
    /// <summary>Partikelstrahl-Mining - für kleine/unregelmäßige Asteroiden</summary>
    ParticleBeam,
    /// <summary>Landung auf Asteroid - für große/regelmäßige Asteroiden</summary>
    Landing,
    /// <summary>Hybrid-Modus - Kombination beider Methoden</summary>
    Hybrid
}

/// <summary>
/// Manuelles Mining der Sonde (über GameHotkeys getoggelt).
/// - Verwendet das Nav-Target vom ProbeAutopilot (oder notfalls ProbeController).
/// - Findet bei Belt-Targets automatisch den nächstgelegenen MineableAsteroid.
/// - Lagert in InventoryController ein und meldet HUD-Status über HUDMessageBus.
/// - Bietet StatusUpdated für bestehende ProbeController-Hooks.
/// - Smart Landing: Automatische Auswahl zwischen Landung und Partikelstrahl basierend auf Asteroid-Eigenschaften.
/// </summary>
[RequireComponent(typeof(InventoryController))]
public class ProbeMiner : MonoBehaviour
{
    [Header("Förder-Parameter")]
    [Tooltip("Fallback-Miningrate in Einheiten/Sekunde, falls Material-Definition keine Rate liefert.")]
    public float defaultMineRate = 5f;

    [Header("Smart Landing System")]
    [Tooltip("Mining-Modus: Auto wählt automatisch zwischen Landung und Partikelstrahl")]
    public MiningMode miningMode = MiningMode.Auto;
    
    [Tooltip("Minimale Asteroidengröße für Landung (in Unity-Units)")]
    public float minLandingSize = 2.0f;
    
    [Tooltip("Maximale Asteroidenrotation für Landung (Grad/Sekunde)")]
    public float maxLandingRotation = 30f;
    
    [Tooltip("Effizienz-Multiplikator für Landung (höher = besser)")]
    [Range(1.0f, 3.0f)] public float landingEfficiencyMultiplier = 1.5f;
    
    [Tooltip("Effizienz-Multiplikator für Partikelstrahl")]
    [Range(0.5f, 1.5f)] public float particleBeamEfficiencyMultiplier = 0.8f;

    [Header("Visuelle Effekte")]
    [Tooltip("Partikeleffekt für Partikelstrahl-Mining")]
    public ParticleSystem particleBeamEffect;
    
    [Tooltip("Partikeleffekt für Landung-Mining (Staubwolke)")]
    public ParticleSystem landingDustEffect;
    
    [Tooltip("Laser-Line für Partikelstrahl")]
    public LineRenderer particleBeamLine;

    private InventoryController cargo;
    private MineableAsteroid target;
    private bool isMining;
    private MiningMode currentMiningMode; // Aktuell verwendeter Modus
    private bool isLanded; // Ob die Sonde gelandet ist
    private ProbeAutopilot autopilot;

    public event Action StatusUpdated; // für ProbeController
    
    // Öffentliche Properties für externe Zugriffe
    public MiningMode CurrentMiningMode => currentMiningMode;
    public bool IsLanded => isLanded;
    public bool IsMining => isMining;

    private void Awake()
    {
        cargo = GetComponent<InventoryController>();
        autopilot = GetComponent<ProbeAutopilot>();
        
        // Initialisiere visuelle Effekte
        InitializeVisualEffects();
    }

    /// <summary>
    /// Initialisiert die visuellen Effekte für verschiedene Mining-Modi
    /// </summary>
    private void InitializeVisualEffects()
    {
        // Partikelstrahl-Line initialisieren
        if (particleBeamLine != null)
        {
            particleBeamLine.enabled = false;
            particleBeamLine.positionCount = 2;
            particleBeamLine.startWidth = 0.1f;
            particleBeamLine.endWidth = 0.05f;
            
            // Versuche LaserBeam Material zu laden, sonst verwende Standard-Material
            var laserMaterial = Resources.Load<Material>("Materials/LaserBeam");
            if (laserMaterial == null)
            {
                // Fallback: Erstelle ein einfaches Material für den Laser
                laserMaterial = new Material(Shader.Find("Sprites/Default"));
                laserMaterial.color = Color.cyan;
                laserMaterial.SetFloat("_Mode", 3); // Transparent
                laserMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                laserMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                laserMaterial.SetInt("_ZWrite", 0);
                laserMaterial.DisableKeyword("_ALPHATEST_ON");
                laserMaterial.EnableKeyword("_ALPHABLEND_ON");
                laserMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                laserMaterial.renderQueue = 3000;
            }
            
            particleBeamLine.material = laserMaterial;
        }
        
        // Partikeleffekte initialisieren
        if (particleBeamEffect != null)
        {
            particleBeamEffect.Stop();
        }
        
        if (landingDustEffect != null)
        {
            landingDustEffect.Stop();
        }
    }

    private void Update()
    {
        if (isMining) DoMining();
    }

    public void ToggleMining()
    {
        if (isMining) StopMining();
        else StartMining();
    }

    public void StartMining()
    {
        if (isMining) return;

        var ast = ResolveCurrentAsteroidTarget();
        if (ast == null)
        {
            HUDMessageBus.Post("Kein abbaufähiges Ziel ausgewählt.");
            StatusUpdated?.Invoke();
            return;
        }

        if (ast.IsFullyMined)
        {
            HUDMessageBus.Post("Asteroid ist erschöpft.");
            StatusUpdated?.Invoke();
            return;
        }

        target = ast;
        
        // Smart Landing: Bestimme optimalen Mining-Modus
        currentMiningMode = DetermineOptimalMiningMode(ast);
        
        // Führe Mining basierend auf gewähltem Modus durch
        ExecuteMiningMode(currentMiningMode, ast);

        isMining = true;
        string modeText = GetMiningModeDescription(currentMiningMode);
        HUDMessageBus.Post($"Mining gestartet - {modeText}");
        StatusUpdated?.Invoke();
    }

    /// <summary>
    /// Bestimmt den optimalen Mining-Modus basierend auf Asteroid-Eigenschaften
    /// </summary>
    private MiningMode DetermineOptimalMiningMode(MineableAsteroid asteroid)
    {
        if (miningMode != MiningMode.Auto)
        {
            return miningMode; // Manuell gewählter Modus
        }

        // Automatische Auswahl basierend auf Asteroid-Eigenschaften
        float asteroidSize = asteroid.transform.localScale.magnitude;
        float asteroidRotation = GetAsteroidRotationSpeed(asteroid);
        
        // Entscheidungskriterien
        bool isLargeEnough = asteroidSize >= minLandingSize;
        bool isStableEnough = asteroidRotation <= maxLandingRotation;
        bool isRegularShape = IsAsteroidRegularShape(asteroid);
        
        // Entscheidungslogik
        if (isLargeEnough && isStableEnough && isRegularShape)
        {
            return MiningMode.Landing; // Große, stabile, regelmäßige Asteroiden
        }
        else if (asteroidSize < minLandingSize * 0.5f)
        {
            return MiningMode.ParticleBeam; // Sehr kleine Asteroiden
        }
        else
        {
            return MiningMode.Hybrid; // Mittlere Asteroiden - Hybrid-Modus
        }
    }

    /// <summary>
    /// Führt den Mining-Modus aus
    /// </summary>
    private void ExecuteMiningMode(MiningMode mode, MineableAsteroid asteroid)
    {
        switch (mode)
        {
            case MiningMode.Landing:
                ExecuteLandingMode(asteroid);
                break;
                
            case MiningMode.ParticleBeam:
                ExecuteParticleBeamMode(asteroid);
                break;
                
            case MiningMode.Hybrid:
                ExecuteHybridMode(asteroid);
                break;
                
            default:
                ExecuteLandingMode(asteroid); // Fallback
                break;
        }
    }

    /// <summary>
    /// Führt Landung-Mining aus
    /// </summary>
    private void ExecuteLandingMode(MineableAsteroid asteroid)
    {
        // An Oberfläche "anlegen"
        if (autopilot != null)
        {
            autopilot.SetSurfaceContact(asteroid.transform);
            isLanded = true;
        }
        
        // Staubwolke-Effekt starten
        if (landingDustEffect != null)
        {
            landingDustEffect.Play();
        }
        
        // Asteroid-Mining starten
        try { asteroid.StartMining(); } catch { /* robust bleiben */ }
    }

    /// <summary>
    /// Führt Partikelstrahl-Mining aus
    /// </summary>
    private void ExecuteParticleBeamMode(MineableAsteroid asteroid)
    {
        isLanded = false;
        
        // Partikelstrahl-Effekte starten
        if (particleBeamEffect != null)
        {
            particleBeamEffect.Play();
        }
        
        if (particleBeamLine != null)
        {
            particleBeamLine.enabled = true;
            UpdateParticleBeamLine(asteroid);
        }
        
        // Asteroid-Mining starten
        try { asteroid.StartMining(); } catch { /* robust bleiben */ }
    }

    /// <summary>
    /// Führt Hybrid-Mining aus (Kombination beider Methoden)
    /// </summary>
    private void ExecuteHybridMode(MineableAsteroid asteroid)
    {
        // Beginne mit Partikelstrahl, wechsle später zu Landung
        ExecuteParticleBeamMode(asteroid);
        
        // Nach 30% Abbau zur Landung wechseln
        StartCoroutine(SwitchToLandingAfterDelay(asteroid, 0.3f));
    }

    /// <summary>
    /// Wechselt nach einem bestimmten Abbau-Prozentsatz zur Landung
    /// </summary>
    private System.Collections.IEnumerator SwitchToLandingAfterDelay(MineableAsteroid asteroid, float switchPercentage)
    {
        float initialUnits = asteroid.UnitsRemaining;
        
        while (isMining && !asteroid.IsFullyMined)
        {
            float currentPercentage = asteroid.UnitsRemaining / initialUnits;
            if (currentPercentage <= (1f - switchPercentage))
            {
                // Wechsle zu Landung
                StopVisualEffects();
                ExecuteLandingMode(asteroid);
                HUDMessageBus.Post("Wechsel zu Landung-Mining");
                yield break;
            }
            
            yield return new WaitForSeconds(1f);
        }
    }

    public void StopMining()
    {
        if (!isMining) return;

        try { if (target != null) target.StopMining(); } catch { }

        // Visuelle Effekte stoppen
        StopVisualEffects();
        
        // Landung beenden
        if (isLanded && autopilot != null)
        {
            autopilot.AbortAutopilot(keepMomentum: false);
            isLanded = false;
        }

        isMining = false;
        target = null;
        currentMiningMode = MiningMode.Auto;

        HUDMessageBus.Post("Mining gestoppt");
        StatusUpdated?.Invoke();
    }

    /// <summary>
    /// Stoppt alle visuellen Effekte
    /// </summary>
    private void StopVisualEffects()
    {
        if (particleBeamEffect != null)
        {
            particleBeamEffect.Stop();
        }
        
        if (landingDustEffect != null)
        {
            landingDustEffect.Stop();
        }
        
        if (particleBeamLine != null)
        {
            particleBeamLine.enabled = false;
        }
    }

    /// <summary>
    /// Aktualisiert die Partikelstrahl-Line
    /// </summary>
    private void UpdateParticleBeamLine(MineableAsteroid asteroid)
    {
        if (particleBeamLine == null || asteroid == null) return;
        
        Vector3 startPos = transform.position;
        Vector3 endPos = asteroid.transform.position;
        
        particleBeamLine.SetPosition(0, startPos);
        particleBeamLine.SetPosition(1, endPos);
    }

    /// <summary>
    /// Ermittelt die Rotationsgeschwindigkeit eines Asteroiden
    /// </summary>
    private float GetAsteroidRotationSpeed(MineableAsteroid asteroid)
    {
        // Vereinfachte Implementierung - könnte erweitert werden
        Rigidbody rb = asteroid.GetComponent<Rigidbody>();
        if (rb != null)
        {
            return rb.angularVelocity.magnitude * Mathf.Rad2Deg;
        }
        
        // Fallback: Schätze basierend auf Größe (kleinere Asteroiden rotieren oft schneller)
        float size = asteroid.transform.localScale.magnitude;
        return size < 1f ? 60f : 20f; // Grad/Sekunde
    }

    /// <summary>
    /// Prüft, ob ein Asteroid eine regelmäßige Form hat
    /// </summary>
    private bool IsAsteroidRegularShape(MineableAsteroid asteroid)
    {
        // Vereinfachte Implementierung - könnte erweitert werden
        Vector3 scale = asteroid.transform.localScale;
        float aspectRatio = Mathf.Max(scale.x, scale.y, scale.z) / Mathf.Min(scale.x, scale.y, scale.z);
        
        return aspectRatio < 1.5f; // Regelmäßig wenn Seitenverhältnis < 1.5:1
    }

    /// <summary>
    /// Gibt eine Beschreibung des Mining-Modus zurück
    /// </summary>
    private string GetMiningModeDescription(MiningMode mode)
    {
        switch (mode)
        {
            case MiningMode.Landing:
                return "Landung (Staubwolke)";
            case MiningMode.ParticleBeam:
                return "Partikelstrahl";
            case MiningMode.Hybrid:
                return "Hybrid (Strahl → Landung)";
            case MiningMode.Auto:
                return "Automatisch";
            default:
                return "Unbekannt";
        }
    }

    private void DoMining()
    {
        if (target == null)
        {
            StopMining();
            return;
        }

        var def = MaterialDatabase.Get(target.MaterialId);
        if (def == null)
        {
            HUDMessageBus.Post("Unbekanntes Material – Mining abgebrochen.");
            StopMining();
            return;
        }

        float baseRate = def.mineRate > 0f ? def.mineRate : defaultMineRate;
        
        // Effizienz-Multiplikator basierend auf Mining-Modus anwenden
        float efficiencyMultiplier = GetEfficiencyMultiplier(currentMiningMode);
        float rate = baseRate * efficiencyMultiplier;
        
        float unitsFromRate = rate * Time.deltaTime;

        float freeVol = cargo.FreeVolume;
        if (freeVol <= 0f)
        {
            HUDMessageBus.Post("Inventar voll – Mining gestoppt.");
            StopMining();
            return;
        }

        float unitsByCargo = freeVol / Mathf.Max(1e-6f, def.volumePerUnit);
        float unitsByAsteroid = target.UnitsRemaining;

        float unitsWanted = Mathf.Min(unitsFromRate, unitsByCargo, unitsByAsteroid);
        if (unitsWanted <= 0f)
        {
            StopMining();
            return;
        }

        float removed = target.RemoveUnits(unitsWanted);
        if (removed > 0f)
        {
            cargo.Add(def.id, removed);
            StatusUpdated?.Invoke();
            
            // Partikelstrahl-Line aktualisieren
            if (currentMiningMode == MiningMode.ParticleBeam || currentMiningMode == MiningMode.Hybrid)
            {
                UpdateParticleBeamLine(target);
            }
        }
        else
        {
            //Wahrscheinlich erschöpft
            HUDMessageBus.Post("Asteroid erschöpft.");
            StopMining();
        }

        if (target.IsFullyMined)
        {
            HUDMessageBus.Post("Asteroid erschöpft.");
            StopMining();
        }
    }

    /// <summary>
    /// Gibt den Effizienz-Multiplikator für den aktuellen Mining-Modus zurück
    /// </summary>
    private float GetEfficiencyMultiplier(MiningMode mode)
    {
        switch (mode)
        {
            case MiningMode.Landing:
                return landingEfficiencyMultiplier;
            case MiningMode.ParticleBeam:
                return particleBeamEfficiencyMultiplier;
            case MiningMode.Hybrid:
                // Hybrid verwendet beide Modi, daher Durchschnitt
                return (landingEfficiencyMultiplier + particleBeamEfficiencyMultiplier) * 0.5f;
            default:
                return 1.0f;
        }
    }

    /// <summary>
    /// Ändert den Mining-Modus zur Laufzeit
    /// </summary>
    public void SetMiningMode(MiningMode newMode)
    {
        if (isMining)
        {
            HUDMessageBus.Post("Mining-Modus kann während des Minings nicht geändert werden.");
            return;
        }
        
        miningMode = newMode;
        HUDMessageBus.Post($"Mining-Modus geändert zu: {GetMiningModeDescription(newMode)}");
    }

    /// <summary>
    /// Erzwingt einen Mining-Modus-Wechsel während des Minings
    /// </summary>
    public void ForceMiningModeChange(MiningMode newMode)
    {
        if (!isMining || target == null) return;
        
        MiningMode oldMode = currentMiningMode;
        currentMiningMode = newMode;
        
        // Visuelle Effekte stoppen und neu starten
        StopVisualEffects();
        ExecuteMiningMode(newMode, target);
        
        HUDMessageBus.Post($"Mining-Modus gewechselt: {GetMiningModeDescription(oldMode)} → {GetMiningModeDescription(newMode)}");
    }

    private MineableAsteroid ResolveCurrentAsteroidTarget()
    {
        // 1) Bevorzugt: Nav-Target aus ProbeAutopilot
        var ap = GetComponent<ProbeAutopilot>();
        Transform navT = ap != null ? ap.NavTarget : null;

        // 2) Fallback: ProbeController-navTarget (falls vorhanden)
        if (navT == null)
        {
            var pc = GetComponent<ProbeController>();
            if (pc != null) navT = pc.navTarget; // entspricht deiner bisherigen Logik
        }

        if (navT == null) return null;

        // a) Wenn ein AsteroidBelt anvisiert ist → nächsten Asteroiden holen
        var belt = navT.GetComponent<AsteroidBelt>();
        if (belt != null)
        {
            var closest = belt.GetClosestAsteroid(transform.position);
            if (closest != null)
            {
                // Root/Parent mit MineableAsteroid auflösen (High/Low LOD-Kinder etc.)
                var ma = closest.GetComponentInParent<MineableAsteroid>();
                if (ma != null) return ma;
            }
            return null; // Belt ohne Kind gefunden
        }

        // b) Direkt ein Asteroid(-Kind) als Target? → Parent prüfen
        var directMa = navT.GetComponentInParent<MineableAsteroid>();
        if (directMa != null) return directMa;

        return null;
    }
}

