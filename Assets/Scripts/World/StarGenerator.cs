// Assets/Scripts/World/StarGenerator.cs
using UnityEngine;

/// <summary>
/// Erzeugt einen leuchtenden Stern (Sphere + Punktlicht) im Szenenzentrum.
/// Das GameObject wird als Kind des StarGenerator‑Objekts angelegt,
/// damit alles sauber im Hierarchy‑Baum bleibt.
/// </summary>
public class StarGenerator : MonoBehaviour
{
    [Header("Material & Farbe")]
    [Tooltip("Unlit‑ oder HDRP/URP‑Material mit Emission aktiviert.")]
    public Material starMaterial;

    [Tooltip("Multiplikator für Emission‑Helligkeit.")]
    public float emissionStrength = 40f;

    [Header("Licht")]
    public float lightIntensity = 20f;     // HDRP: 8–20
    public float lightRangeUnits = 1000000f; // 1000 Units = 1 Gm

    [Header("Scale")]
    [Tooltip("Vergrößerung des sichtbaren Stern-Durchmessers")]
    public float sizeMultiplier = 10f;

    // --------------------------------------------------------------------
    public GameObject CreateStar(StarDto dto)
    {
        // ---------- 1) Sphere‑Mesh --------------------------------------
        GameObject starGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        starGO.tag = "Star"; // Tag für Filterung
        starGO.name = dto.name;
        starGO.transform.SetParent(transform, false);      // Kind von StarGenerator
        starGO.transform.position = Vector3.zero;

        // Radius: real km -> Units
        float radiusKm = EstimateRadiusKm(dto.spect);
        float radiusUnits = radiusKm / PlanetScale.KM_PER_UNIT;
        starGO.transform.localScale = Vector3.one * (radiusUnits * 2f * sizeMultiplier); // Durchmesser

        // ---------- 2) Material mit Emission ---------------------------
        Material mat = new Material(starMaterial);
        Color baseCol = SpectralColor(dto.spect);
        mat.SetColor("_EmissionColor", baseCol * emissionStrength);
        var renderer = starGO.GetComponent<MeshRenderer>();
        renderer.material = mat;

        // ---------- 3) Punktlicht für Beleuchtung ----------------------
        GameObject lightGO = new GameObject($"{dto.name}_Light");
        lightGO.transform.SetParent(transform, false);
        lightGO.transform.position = Vector3.zero;

        Light light = lightGO.AddComponent<Light>();
        light.type = LightType.Point;
        light.intensity = lightIntensity;
        light.range = lightRangeUnits * sizeMultiplier;

        // Registrieren
        PlanetRegistry.Instance?.RegisterStar(starGO.transform);

        return starGO;
    }

    // --------------------------------------------------------------------
    /* Realistische Schätzungen (M‑Zwerg bis O‑Riese). */
    private static float EstimateRadiusKm(string spect)
    {
        char c = string.IsNullOrEmpty(spect) ? 'G' : char.ToUpper(spect[0]);
        return c switch
        {
            'O' => 1_500_000f,
            'B' => 1_000_000f,
            'A' => 800_000f,
            'F' => 700_000f,
            'G' => 696_000f,   // Sonne
            'K' => 500_000f,
            'M' => 300_000f,
            _ => 696_000f,
        };
    }

    /* Farbwerte – leicht stilisiert, aber astrophysikalisch plausibel. */
    private static Color SpectralColor(string spect)
    {
        char c = string.IsNullOrEmpty(spect) ? 'G' : char.ToUpper(spect[0]);
        return c switch
        {
            'O' => new Color(0.65f, 0.78f, 1f),   // blau
            'B' => new Color(0.75f, 0.85f, 1f),
            'A' => new Color(0.9f, 0.9f, 1f),
            'F' => new Color(1f, 0.95f, 0.9f),
            'G' => new Color(1f, 0.92f, 0.8f), // gelb‑weiß
            'K' => new Color(1f, 0.8f, 0.6f), // orange
            'M' => new Color(1f, 0.6f, 0.4f), // rot‑orange
            _ => Color.white,
        };
    }
}
