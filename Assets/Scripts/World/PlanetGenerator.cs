// Assets/Scripts/World/PlanetGenerator.cs
using UnityEngine;

public class PlanetGenerator : MonoBehaviour
{
    [Header("Atmosphären‑Materialien (kein/dünn/sauerstoffarm/stickstoffreich)")]
    public Material[] atmosphereMaterials;

    [Header("Scale")]
    [Tooltip("Vergrößerung des sichtbaren Planeten-Durchmessers")]
    public float sizeMultiplier = 10f;

    public GameObject CreatePlanet(PlanetDto dto)
    {
        if (dto == null) { Debug.LogError("PlanetGenerator: DTO is null!"); return null; }

        return dto.object_type switch
        {
            "planet" => CreatePlanetSphere(dto),
            "asteroid_belt" => CreateAsteroidBelt(dto),
            _ => UnknownType(dto)
        };
    }

    /* ---------- kleine Helfer ---------- */
    private static int AtmosphereIndex(string a) => a switch
    {
        "dünn" => 0,
        "sauerstoffarm" => 1,
        "stickstoffreich" => 2,
        _ => -1
    };

    GameObject CreatePlanetSphere(PlanetDto dto)
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.tag = "Planet"; // Tag für Planeten
        /* 🔸 Anzeige-Name verwenden, sonst Fallback auf dto.name */
        string display = string.IsNullOrEmpty(dto.displayName) ? dto.name : dto.displayName;
        body.name = display;

        float scale = PlanetScale.KM_PER_UNIT;
        body.transform.position = new Vector3(dto.position.x, dto.position.y, dto.position.z) / scale;
        //body.transform.position = new Vector3(dto.position.x, dto.position.y, dto.position.z);

        // sichtbare Größe
        float diameterUnits = (dto.radius_km * 2f) / PlanetScale.KM_PER_UNIT * sizeMultiplier;
        body.transform.localScale = Vector3.one * diameterUnits;

        // Atmosphäre …
        if (dto.atmosphere != "kein" && dto.atmosphere != "")
        {
            GameObject atmo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            atmo.transform.SetParent(body.transform, false);
            atmo.transform.localScale = Vector3.one * 1.02f * sizeMultiplier;
            atmo.GetComponent<MeshRenderer>().material =
                atmosphereMaterials[AtmosphereIndex(dto.atmosphere)];
        }

        body.AddComponent<OrbitAnimation>().Init(dto.orbital_period_days);
        PlanetRegistry.Instance?.RegisterPlanet(body.transform);
        return body;
    }

    GameObject UnknownType(PlanetDto dto)
    {
        Debug.LogError($"PlanetGenerator: Unknown planet type '{dto.object_type}' for {dto.name}");
        return null;
    }

    private GameObject CreateAsteroidBelt(PlanetDto dto)
    {
        // sehr vereinfachter Ring-Placeholder
        GameObject belt = new(dto.name);
        belt.tag = "AsteroidBelt"; // Tag für Asteroiden-Gürtel
        belt.transform.position = new Vector3(dto.position.x, dto.position.y, dto.position.z) /
                                  PlanetScale.KM_PER_UNIT;

        belt.AddComponent<AsteroidBelt>();
        PlanetRegistry.Instance?.RegisterAsteroidBelt(belt.transform);
        return belt;
    }

}
