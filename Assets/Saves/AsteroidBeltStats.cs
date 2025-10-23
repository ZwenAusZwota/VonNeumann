using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public struct AsteroidBeltStats
{
    [Tooltip("Gesamtanzahl der aktiven Asteroiden")]
    public int TotalAsteroids;

    [Tooltip("Anzahl der aktuell sichtbaren/gerenderten Asteroiden")]
    public int VisibleAsteroids;

    [Tooltip("Ob Object Pooling verwendet wird")]
    public bool UsePooling;

    [Tooltip("Innerer Radius des Gürtels")]
    public float InnerRadius;

    [Tooltip("Äußerer Radius des Gürtels")]
    public float OuterRadius;

    [Tooltip("Maximale Sichtbarkeitsdistanz")]
    public float VisibilityDistance;

    [Tooltip("Ob Orbital-Bewegung aktiviert ist")]
    public bool OrbitalMotion;

    // Zusätzliche nützliche Stats für dein Bobiverse-Spiel
    [Tooltip("Durchschnittliche Ressourcendichte pro Asteroid")]
    public float AverageResourceDensity;

    [Tooltip("Geschätzte Gesamtressourcen im Gürtel")]
    public long EstimatedTotalResources;

    [Tooltip("Anzahl bereits abgebauter Asteroiden")]
    public int MinedAsteroids;

    [Tooltip("Prozentuale Ausbeutung des Gürtels")]
    public float ExploitationPercentage;

    // Konstruktor für einfache Initialisierung
    public AsteroidBeltStats(int totalAsteroids, int visibleAsteroids, bool usePooling,
                           float innerRadius, float outerRadius, float visibilityDistance,
                           bool orbitalMotion)
    {
        TotalAsteroids = totalAsteroids;
        VisibleAsteroids = visibleAsteroids;
        UsePooling = usePooling;
        InnerRadius = innerRadius;
        OuterRadius = outerRadius;
        VisibilityDistance = visibilityDistance;
        OrbitalMotion = orbitalMotion;

        // Standardwerte für erweiterte Stats
        AverageResourceDensity = 0f;
        EstimatedTotalResources = 0L;
        MinedAsteroids = 0;
        ExploitationPercentage = 0f;
    }

    // Utility-Methode für erweiterte Statistiken
    public void CalculateResourceStats(List<MineableAsteroid> asteroids)
    {
        if (asteroids == null || asteroids.Count == 0)
        {
            AverageResourceDensity = 0f;
            EstimatedTotalResources = 0L;
            MinedAsteroids = 0;
            ExploitationPercentage = 0f;
            return;
        }

        long totalResources = 0L;
        int minedCount = 0;

        foreach (var asteroid in asteroids)
        {
            if (asteroid != null)
            {
                // Explizite Konvertierung von startUnits zu long
                totalResources += (long)asteroid.startUnits;

                // Prüfe ob Asteroid vollständig abgebaut ist
                if (asteroid.IsFullyMined)
                {
                    minedCount++;
                }
            }
        }

        AverageResourceDensity = (float)totalResources / asteroids.Count;
        EstimatedTotalResources = totalResources;
        MinedAsteroids = minedCount;
        ExploitationPercentage = asteroids.Count > 0 ? (float)minedCount / asteroids.Count * 100f : 0f;
    }

    // String-Ausgabe für Debug-Zwecke
    public override string ToString()
    {
        return $"AsteroidBelt Stats:\n" +
               $"  Asteroids: {TotalAsteroids} ({VisibleAsteroids} visible)\n" +
               $"  Ring: {InnerRadius:F1} - {OuterRadius:F1} units\n" +
               $"  Pooling: {UsePooling}, Orbital: {OrbitalMotion}\n" +
               $"  Resources: {EstimatedTotalResources:N0} units total\n" +
               $"  Exploitation: {ExploitationPercentage:F1}% ({MinedAsteroids} mined)";
    }
}