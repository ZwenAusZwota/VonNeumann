// Assets/Scripts/World/PlanetGenerator.cs
using UnityEngine;
using System;

/// <summary>
/// Generates planets, asteroid belts and validates orbital parameters for a star system.
/// </summary>
public class PlanetGenerator : MonoBehaviour
{
    #region Inspector Fields

    [Header("Atmosphären-Materialien (kein/dünn/sauerstoffarm/stickstoffreich)")]
    public Material[] atmosphereMaterials;

    [Header("Scale")]
    [Tooltip("Vergrößerung des sichtbaren Planeten‑Durchmessers")]
    public float sizeMultiplier = 10f;

    [Header("Realistic Distance Validation")]
    [Tooltip("Prüfe, ob Planetenabstände realistisch sind")]
    public bool validateDistances = true;

    [Tooltip("Minimaler Abstand zweier Planeten (in AU)")]
    public float minPlanetSpacing = 0.3f; // 0.3 AU ≈ 45 Mio km

    [Header("Orbital Parameters")]
    [Tooltip("Planeten bewegen sich in Umlaufbahnen um das Systemzentrum")]
    public bool enableOrbitalMotion = true;

    [Tooltip("Orbitalgeschwindigkeits‑Faktor (höher = schnellere Umläufe)")]
    public float orbitalSpeedMultiplier = 1f;

    [Tooltip("Variation in orbital speeds (0 = uniform, 1 = hohe Variation)")]
    [Range(0f, 1f)]
    public float speedVariation = 0.3f;

    [Header("Debug")]
    public bool showDebugInfo = true;

    #endregion

    #region Public API

    /// <summary>
    /// Erstellt ein <see cref="GameObject"/> entsprechend des <paramref name="dto"/>.
    /// </summary>
    public GameObject CreatePlanet(PlanetDto dto)
    {
        if (dto == null)
        {
            Debug.LogError("PlanetGenerator: DTO is null!");
            return null;
        }

        return dto.object_type switch
        {
            "planet" => CreatePlanetSphere(dto),
            //"asteroid_belt" => CreateAsteroidBelt(dto),
            _ => UnknownType(dto)
        };
    }

    #endregion

    #region Helpers

    private static int AtmosphereIndex(string a) => a switch
    {
        "dünn" => 0,
        "sauerstoffarm" => 1,
        "stickstoffrich" => 2,
        _ => -1
    };

    #endregion

    #region Planet Creation

    private GameObject CreatePlanetSphere(PlanetDto dto)
    {
        // Basis
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.tag = "Planet";
        body.name = string.IsNullOrEmpty(dto.displayName) ? dto.name : dto.displayName;

        // 1) Position
        Vector3 worldPosition = CalculateRealisticPosition(dto);
        body.transform.position = worldPosition;

        // 2) Validierung
        //if (validateDistances && showDebugInfo)
        //    ValidatePlanetDistance(dto, worldPosition);

        // 3) Größe
        body.transform.localScale = Vector3.one * CalculateVisualSize(dto);

        // 4) Atmosphäre
        CreateAtmosphere(body, dto);

        // 5) Orbitale Bewegung (falls aktiviert)
        if (enableOrbitalMotion)
        {
            var orbitAnim = body.AddComponent<OrbitAnimation>();
            orbitAnim.Init(dto.orbital_period_days);
           // orbitAnim.speedMultiplier = orbitalSpeedMultiplier;
           // orbitAnim.speedVariation = speedVariation;
        }

        // 6) Planet‑Komponente
        body.AddComponent<Planet>();

        // 7) Registry
        PlanetRegistry.Instance?.RegisterPlanet(body.transform);

        if (showDebugInfo)
        {
            float au = worldPosition.magnitude * PlanetScale.KM_PER_UNIT / 149_597_870.7f;
            Debug.Log($"Planet {body.name} created at {au:F2} AU (Radius {dto.radius_km:F0} km)");
        }

        return body;
    }

    #endregion

    #region Position & Size

    /// <summary>
    /// Berechnet eine realistische Umlaufposition basierend auf <see cref="PlanetDto.star_distance_km"/>.
    /// Es werden nun <b>x</b>, <b>y</b> und <b>z</b>‑Koordinaten erzeugt und gleichzeitig <c>dto.position</c>
    /// in Kilometern aktualisiert, sodass nachfolgende Systeme (z. B. <c>GameManager.SqrDist</c>) keine
    /// NullReferenceException mehr auslösen.
    /// </summary>
    private Vector3 CalculateRealisticPosition(PlanetDto dto)
    {
        const float KM_PER_AU = 149_597_870.7f;
        float scale = PlanetScale.KM_PER_UNIT;

        // 1) Distanz ermitteln
        float distanceKm = Mathf.Max(dto.star_distance_km, 0f);

        // Fallback: DTO‑Position, falls Wert 0/leer
        if (distanceKm <= 0f)
        {
            distanceKm = new Vector3(dto.position?.x ?? 0f, dto.position?.y ?? 0f, dto.position?.z ?? 0f).magnitude;
            if (showDebugInfo)
                Debug.LogWarning($"Planet {dto.name}: 'star_distance_km' fehlt – nutze {distanceKm:F0} km aus dto.position.");
        }

        // 2) Clamp auf 0.1 – 50 AU
        float distanceAU = distanceKm / KM_PER_AU;
        if (distanceAU < 0.1f || distanceAU > 500f)
        {
            if (showDebugInfo)
                Debug.LogWarning($"Planet {dto.name}: Distanz {distanceAU:F2} AU außerhalb realistischer Spanne (0.1‑500). Wird begrenzt.");
            distanceAU = Mathf.Clamp(distanceAU, 0.1f, 100f);
        }
        distanceKm = distanceAU * KM_PER_AU;

        // 3) Deterministische Winkel (Seed = Name‑Hash)
        System.Random rng = new System.Random(dto.name.GetHashCode());
        float azimuthRad = (float)(rng.NextDouble() * Math.PI * 2.0);                    // 0° … 360°
        float inclinationRad = (float)((rng.NextDouble() - 0.5) * 15.0 * Mathf.Deg2Rad);     // ±7.5°

        // 4) Sphärisch → Kartesisch
        float cosI = Mathf.Cos(inclinationRad);
        float sinI = Mathf.Sin(inclinationRad);

        float xKm = cosI * Mathf.Cos(azimuthRad) * distanceKm;
        float yKm = sinI * distanceKm;
        float zKm = cosI * Mathf.Sin(azimuthRad) * distanceKm;

        Vector3 posKm = new Vector3(xKm, yKm, zKm);

        /* 4b) DTO-Position in Kilometern synchronisieren
         * ------------------------------------------------
         * GameManager sortiert und spawnt Objekte anhand von <c>dto.position</c>.
         * Ist diese Referenz null, schlägt der Zugriff fehl (siehe NullReferenceException).
         */
  
        dto.position = new Vec3Dto();
        dto.position.x = xKm;
        dto.position.y = yKm;
        dto.position.z = zKm;

        // 5) Km → Unity‑Units
        return posKm / scale;
    }

    /// <summary>
    /// Sichtbarer Durchmesser des Planeten in Unity‑Einheiten.
    /// </summary>
    private float CalculateVisualSize(PlanetDto dto)
    {
        float radiusUnits = dto.radius_km / PlanetScale.KM_PER_UNIT;
        float visualDiameter = radiusUnits * 2f * sizeMultiplier;
        return Mathf.Max(visualDiameter, 0.01f);
    }

    #endregion

    #region Atmosphäre

    private void CreateAtmosphere(GameObject planet, PlanetDto dto)
    {
        if (dto.atmosphere == "kein" || string.IsNullOrEmpty(dto.atmosphere))
            return;

        int index = AtmosphereIndex(dto.atmosphere);
        if (index < 0 || index >= atmosphereMaterials.Length)
            return;

        GameObject atmo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        atmo.name = $"{planet.name}_Atmosphere";
        atmo.transform.SetParent(planet.transform, false);
        atmo.transform.localScale = Vector3.one * 1.05f; // 5 % größer als Planet

        var renderer = atmo.GetComponent<MeshRenderer>();
        renderer.material = atmosphereMaterials[index];

        var collider = atmo.GetComponent<Collider>();
        if (collider) collider.isTrigger = true;
    }

    #endregion

    #region Validation

    /*private void ValidatePlanetDistance(PlanetDto dto, Vector3 position)
    {
        float distanceKm = position.magnitude * PlanetScale.KM_PER_UNIT;
        float distanceAU = distanceKm / 149_597_870.7f;

        // Vergleich mit bestehenden Planeten
        if (PlanetRegistry.Instance != null)
        {
            foreach (Transform other in PlanetRegistry.Instance.Planets)
            {
                if (other == null || other == position) continue;

                float dAu = Vector3.Distance(position, other.position) * PlanetScale.KM_PER_UNIT / 149_597_870.7f;
                if (dAu < minPlanetSpacing)
                    Debug.LogWarning($"Planet {dto.name} ist {dAu:F3} AU von {other.name} entfernt – unter Mindestabstand {minPlanetSpacing:F2} AU!");
            }
        }

        Debug.Log($"Planet {dto.name}: {distanceAU:F2} AU Abstand zum Stern.");
    }*/

    #endregion

    #region Asteroid Belt

    public GameObject CreateAsteroidBelt(AsteroidBeltDto dto)
    {
        GameObject belt = new GameObject(dto.name);
        belt.tag = "AsteroidBelt";

        // Zentrum bestimmen
        //Vector3 beltCenter = new Vector3(dto.position.x, dto.position.y, dto.position.z) / PlanetScale.KM_PER_UNIT;

        belt.transform.position = Vector3.zero;

        // Komponente hinzufügen
        var asteroidBelt = belt.AddComponent<AsteroidBelt>();

        // Parameter berechnen
        //float distanceUnits = beltCenter.magnitude;
        //float beltWidthUnits = 0.7f * 149_597_870.7f / PlanetScale.KM_PER_UNIT; // 0.7 AU

        //asteroidBelt.innerRadius = Mathf.Max(0f, distanceUnits - beltWidthUnits * 0.5f);
        //asteroidBelt.outerRadius = distanceUnits + beltWidthUnits * 0.5f;
        asteroidBelt.innerRadius = dto.inner_radius_km / PlanetScale.KM_PER_UNIT;
        asteroidBelt.outerRadius = dto.outer_radius_km / PlanetScale.KM_PER_UNIT;
        //asteroidBelt.asteroidCount = Mathf.Clamp(Mathf.RoundToInt(distanceUnits * 50f), 500, 5000);

        //if (showDebugInfo)
        //{
        //    float distanceAU = distanceUnits * PlanetScale.KM_PER_UNIT / 149_597_870.7f;
        //    Debug.Log($"Asteroid Belt {dto.name} created at {distanceAU:F2} AU (inner {asteroidBelt.innerRadius * PlanetScale.KM_PER_UNIT / 149_597_870.7f:F2} AU, outer {asteroidBelt.outerRadius * PlanetScale.KM_PER_UNIT / 149_597_870.7f:F2} AU)");
        //}

        PlanetRegistry.Instance?.RegisterAsteroidBelt(belt.transform);
        return belt;
    }

    #endregion
    GameObject UnknownType(PlanetDto dto)
    {
        Debug.LogError($"PlanetGenerator: Unknown planet type '{dto.object_type}' for {dto.name}");
        return null;
    }
}
