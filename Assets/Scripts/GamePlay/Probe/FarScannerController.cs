using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RegistrableEntity))]
public class FarScannerController : BaseScannerController
{
    private readonly HashSet<MineableAsteroid> _asteroidScratch = new();
    [Header("FarScan – Voreinstellung (AU)")]
    [Tooltip("Sinnvoll für Systeme/weite Umgebung. Beispiel: 10 AU.")]
    public float defaultFarAU = 10.0f;

    [Header("Signalverzögerung (Radar / Lichtlaufzeit)")]
    [Tooltip("Wenn aktiv, werden Scan-Ergebnisse mit t-Δ (Beobachtungszeitpunkt) angezeigt.")]
    public bool simulateLightDelay = true;

    [Tooltip("Signalgeschwindigkeit in km/s (Licht ~ 299792.458).")]
    public float signalSpeedKmPerSec = 299_792.458f;

    private void Reset()
    {
        scanRadiusAU = defaultFarAU;
    }

    public override void PerformScan()
    {
        float radiusUnits = AuToUnits(scanRadiusAU);
        Vector3 origin = transform.position;

        var results = new List<SystemObject>(128);
        var seen = new HashSet<EntityId>();

        CollectColliderHits(origin, radiusUnits, results, seen, includeAsteroids: false);
        CollectNearestAsteroidBeltHit(origin, radiusUnits, results, seen);
        ScanOrbiterHelper.GroupOrbitersUnderPlanets(results, origin);

        Publish(results);
    }

    private void CollectNearestAsteroidBeltHit(
        Vector3 origin,
        float radiusUnits,
        List<SystemObject> results,
        HashSet<EntityId> seen)
    {
        MineableAsteroid nearestAsteroid = null;
        float nearestDistUnits = float.MaxValue;

        _asteroidScratch.Clear();
        foreach (var asteroid in FindObjectsByType<MineableAsteroid>())
        {
            if (asteroid == null) continue;
            if (!_asteroidScratch.Add(asteroid)) continue;

            var go = asteroid.gameObject;
            if (IsPartOfScanningProbe(go)) continue;

            float distUnits = (go.transform.position - origin).magnitude;
            if (distUnits > radiusUnits || distUnits >= nearestDistUnits) continue;

            nearestAsteroid = asteroid;
            nearestDistUnits = distUnits;
        }

        if (nearestAsteroid != null)
        {
            var belt = nearestAsteroid.GetComponentInParent<AsteroidBelt>();
            var targetGo = belt != null ? belt.gameObject : nearestAsteroid.gameObject;
            var beltId = targetGo.GetEntityId();
            if (!seen.Contains(beltId))
            {
                results.Add(new SystemObject
                {
                    Kind = SystemObject.ObjectKind.AsteroidBelt,
                    Id = beltId.ToString(),
                    Name = "AsteroidBelt",
                    DisplayName = belt != null
                        ? BuildDisplayNameForBelt(belt, origin, nearestAsteroid.transform)
                        : BuildDisplayName(nearestAsteroid.transform, origin),
                    Dto = belt,
                    GameObject = targetGo,
                    RequiresNearScan = false
                });
                seen.Add(beltId);
                seen.Add(nearestAsteroid.gameObject.GetEntityId());
                return;
            }
        }

        CollectBeltHits(origin, radiusUnits, results, seen);
    }

    /// <summary>
    /// Wird vom BaseScannerController aufgerufen, wenn neue Treffer vorliegen.
    /// Berechnet optional t-Δ und übergibt die Liste an das ViewModel + HUD.
    /// </summary>
    protected override void Publish(List<SystemObject> entries)
    {
        if (simulateLightDelay && signalSpeedKmPerSec > 0f && entries != null)
        {
            Vector3 origin = transform.position;

            for (int i = 0; i < entries.Count; i++)
            {
                var so = entries[i];
                if (so == null || so.GameObject == null) continue;

                // Distanz Scanner -> Ziel
                float distUnits = (so.GameObject.transform.position - origin).magnitude;
                float distKm = UnitsToKm(distUnits);

                // Δt = Strecke / c
                double delaySec = distKm / signalSpeedKmPerSec;

                // Anzeige „t-Δ“ anhängen
                string suffix = FormatDelay(delaySec); // z.B. "8.3 min" oder "2.1 h"
                if (!string.IsNullOrWhiteSpace(suffix))
                {
                    string baseName = string.IsNullOrWhiteSpace(so.DisplayName)
                        ? (so.Name ?? so.GameObject.tag)
                        : so.DisplayName;
                    so.DisplayName = $"{baseName} — t-{suffix}";
                }
            }
        }

        // Einheitlich zum NearScan: ViewModel befüllen + HUD-Refresh aus Base
        ApplyResultsToViewModelAndNotify<FarScanViewModel>(entries);
    }

    private static string FormatDelay(double seconds)
    {
        if (seconds < 1.0) return $"{seconds * 1000.0:0} ms";
        if (seconds < 60.0) return $"{seconds:0.#} s";
        double minutes = seconds / 60.0;
        if (minutes < 60.0) return $"{minutes:0.#} min";
        double hours = minutes / 60.0;
        if (hours < 24.0) return $"{hours:0.#} h";
        double days = hours / 24.0;
        return $"{days:0.#} d";
    }
}
