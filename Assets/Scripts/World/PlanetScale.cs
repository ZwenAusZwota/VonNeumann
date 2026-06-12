// Assets/Scripts/World/PlanetScale.cs
public static class PlanetScale
{
    /// <summary>1 Unity-Unit entspricht so vielen Kilometern (Distanz).</summary>
    public const float KM_PER_UNIT = 1_000_000f;

    /// <summary>
    /// Anzeige-Skala für Geschwindigkeit (unabhängig von der Distanzkompression).
    /// Kalibriert auf maxCruiseSpeed ≈ 400 u/s → ~120 km/s.
    /// </summary>
    public const float KM_PER_UNIT_SPEED = 0.3f;

    public static float UnitsPerSecToKmPerSec(float unitsPerSec)
    {
        return unitsPerSec * KM_PER_UNIT_SPEED;
    }
}
