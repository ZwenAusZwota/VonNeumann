// Assets/Scripts/Probe/FarScannerController.cs
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class FarScannerController : BaseScannerController
{
    [Header("FarScan – Voreinstellung (AE)")]
    [Tooltip("Sinnvoll für Systeme/weite Umgebung. Beispiel: 2 AE.")]
    public float defaultFarAU = 10.0f;

    [Header("Signalverzögerung (Radar / Lichtlaufzeit)")]
    [Tooltip("Wenn aktiv, werden Scan-Ergebnisse mit t-Δ (Beobachtungszeitpunkt) angezeigt.")]
    public bool simulateLightDelay = true;

    [Tooltip("Signalgeschwindigkeit in km/s (Licht ~ 299792.458).")]
    public float signalSpeedKmPerSec = 299_792.458f;

    void Reset()
    {
        scanRadiusAU = defaultFarAU;
    }

    protected override void Publish(List<SystemObject> entries)
    {
        if (simulateLightDelay && signalSpeedKmPerSec > 0f)
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
                    string baseName = string.IsNullOrWhiteSpace(so.DisplayName) ? (so.Name ?? so.GameObject.tag) : so.DisplayName;
                    so.DisplayName = $"{baseName} — t-{suffix}";
                }
            }
        }

        HUDBindingService.PublishFarScan(gameObject, entries); // unverändert, UI & Autopilot arbeiten wie gehabt. 
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
