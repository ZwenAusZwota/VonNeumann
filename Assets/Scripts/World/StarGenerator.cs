// Assets/Scripts/World/StarGenerator.cs
using UnityEngine;

/// <summary>
/// Erzeugt einen leuchtenden Stern (Sphere + Punktlicht) im Szenenzentrum.
/// Das GameObject wird als Kind des StarGenerator-Objekts angelegt.
/// Alle Distanzen/Größen basieren auf km und werden über PlanetScale in Units umgerechnet.
/// </summary>
public class StarGenerator : MonoBehaviour
{
    [Header("Material & Farbe")]
    [Tooltip("Unlit- oder URP/HDRP-taugliches Material mit Emission-Unterstützung.")]
    public Material starMaterial;

    [Header("Farbtemperatur")]
    [Tooltip("Wenn aktiv, wird die Sternfarbe aus der Kelvin-Temperatur berechnet (dto.colorTemperatureK).")]
    public bool preferKelvinColor = true;

    [Range(1000f, 40000f)]
    [Tooltip("Fallback, falls dto.colorTemperatureK ≤ 0 ist.")]
    public float kelvinFallback = 5772f;

    [Tooltip("Multiplikator für Emission-Helligkeit.")]
    public float emissionStrength = 40f;

    [Header("Licht")]
    [Tooltip("Punktlicht-Intensität (Pipeline-abhängig).")]
    public float lightIntensity = 20f;

    [Tooltip("Reichweite des Punktlichts in Unity-Units.")]
    public float lightRangeUnits = 1_000_000f;

    [Header("Darstellung")]
    [Tooltip("Vergrößerung des sichtbaren Stern-Durchmessers (nur Visualisierung).")]
    public float sizeMultiplier = 10f;

    [Tooltip("SphereCollider vom Stern entfernen (empfohlen).")]
    public bool removeCollider = true;

    const float SUN_RADIUS_KM = 696_000f;   // Näherung

    // --------------------------------------------------------------------
    public GameObject CreateStar(StarDto dto)
    {
        // ---------- 1) Sphere-Mesh --------------------------------------
        GameObject starGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        starGO.tag = "Star";
        starGO.name = string.IsNullOrWhiteSpace(dto.name) ? "Star" : dto.name;
        //starGO.transform.SetParent(transform, false);
        starGO.transform.SetParent(WorldRoot.Instance.starRoot, false);
        starGO.transform.localPosition = Vector3.zero;

        if (removeCollider)
        {
            var col = starGO.GetComponent<Collider>();
            if (col) Destroy(col);
        }

        // Radius: aus Luminosität/Temperatur, sonst Spektralklasse
        float radiusKm = TryComputeRadiusFromLuminosity(dto.luminosity_solar, dto.colorTemperatureK, out float rLum)
                         ? rLum
                         : EstimateRadiusKm(dto.spect);

        float radiusUnits = radiusKm / PlanetScale.KM_PER_UNIT;
        starGO.transform.localScale = Vector3.one * (radiusUnits * 2f * sizeMultiplier); // Durchmesser sichtbar

        // ---------- 2) Farbe bestimmen ----------------------------------
        float k = (dto.colorTemperatureK > 0f) ? dto.colorTemperatureK : kelvinFallback;
        Color baseCol = (preferKelvinColor) ? ColorFromKelvin(k) : SpectralColor(dto.spect);

        // ---------- 3) Material mit Emission auf dem Renderer von starGO -
        var rend = starGO.GetComponent<Renderer>();
        if (!rend) rend = starGO.GetComponentInChildren<Renderer>();

        var mat = starMaterial != null ? new Material(starMaterial) : new Material(Shader.Find("Standard"));
        // Basisfarbe (je nach Shader "_BaseColor" / "_Color")
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseCol);
        else if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseCol);

        // Emission aktivieren
        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            // Hinweis: Im Linear-Color-Space ggf. baseCol.linear nutzen
            mat.SetColor("_EmissionColor", baseCol * emissionStrength);
        }
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.material = mat; // <<< WICHTIG: auf Renderer des Stern-GameObjects!

        // ---------- 4) Punktlicht für Beleuchtung -----------------------
        GameObject lightGO = new GameObject($"{starGO.name}_Light");
        lightGO.transform.SetParent(starGO.transform, false);
        lightGO.transform.localPosition = Vector3.zero;

        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = baseCol;
        light.intensity = lightIntensity;
        light.range = Mathf.Max(1f, lightRangeUnits * sizeMultiplier);

        // ---------- 5) Registrierung ------------------------------------
        PlanetRegistry.Instance?.RegisterStar(starGO.transform);

        return starGO;
    }

    // --------------------------------------------------------------------
    /// <summary>
    /// Schätzung des Sternradius aus Luminosität (L/☉) und Temperatur (K).
    /// L = 4πσ R^2 T^4  ⇒  R/R☉ = sqrt(L) * (T☉/T)^2
    /// </summary>
    private bool TryComputeRadiusFromLuminosity(float luminositySolar, float tempK, out float radiusKm)
    {
        radiusKm = 0f;
        if (luminositySolar <= 0f || tempK <= 0f) return false;

        float t = Mathf.Clamp(tempK, 1000f, 40000f);
        float rOverSun = Mathf.Sqrt(Mathf.Max(0.01f, luminositySolar)) * Mathf.Pow(5772f / t, 2f);
        radiusKm = Mathf.Clamp(rOverSun, 0.05f, 1000f) * SUN_RADIUS_KM; // Schutzkappen
        return true;
        // Falls du streng am Spektraltyp bleiben willst, setze preferKelvinColor=false
        // und/oder ignoriere diese Methode.
    }

    /// <summary>
    /// Grobe Radien-Näherung nach Spektralklasse als Fallback.
    /// </summary>
    private static float EstimateRadiusKm(string spect)
    {
        char c = string.IsNullOrEmpty(spect) ? 'G' : char.ToUpper(spect[0]);
        return c switch
        {
            'O' => 1_500_000f,
            'B' => 1_000_000f,
            'A' => 800_000f,
            'F' => 700_000f,
            'G' => 696_000f, // Sonne
            'K' => 500_000f,
            'M' => 300_000f,
            _ => 696_000f,
        };
    }

    /// <summary>
    /// Stilisiert-physikalische Spektral-Farben als Fallback.
    /// </summary>
    private static Color SpectralColor(string spect)
    {
        char c = string.IsNullOrEmpty(spect) ? 'G' : char.ToUpper(spect[0]);
        return c switch
        {
            'O' => new Color(0.65f, 0.78f, 1f),   // blau
            'B' => new Color(0.75f, 0.85f, 1f),
            'A' => new Color(0.9f, 0.9f, 1f),
            'F' => new Color(1f, 0.95f, 0.9f),
            'G' => new Color(1f, 0.92f, 0.8f),    // gelb-weiß
            'K' => new Color(1f, 0.8f, 0.6f),     // orange
            'M' => new Color(1f, 0.6f, 0.4f),     // rot-orange
            _ => Color.white,
        };
    }

    /// <summary>
    /// Kelvin -> RGB (Approximation 1000..40000 K).
    /// </summary>
    private static Color ColorFromKelvin(float kelvin)
    {
        float t = Mathf.Clamp(kelvin, 1000f, 40000f) / 100f;

        float r, g, b;
        if (t <= 66f)
        {
            r = 255f;
            g = 99.4708025861f * Mathf.Log(t) - 161.1195681661f;
            b = (t <= 19f) ? 0f : 138.5177312231f * Mathf.Log(t - 10f) - 305.0447927307f;
        }
        else
        {
            r = 329.698727446f * Mathf.Pow(t - 60f, -0.1332047592f);
            g = 288.122169528f * Mathf.Pow(t - 60f, -0.0755148492f);
            b = 255f;
        }

        r = Mathf.Clamp(r, 0f, 255f);
        g = Mathf.Clamp(g, 0f, 255f);
        b = Mathf.Clamp(b, 0f, 255f);
        return new Color(r / 255f, g / 255f, b / 255f, 1f);
    }
}
