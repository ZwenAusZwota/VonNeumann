// Assets/Scripts/Probe/NearScannerController.cs
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RegistrableEntity))]
public class NearScannerController : BaseScannerController
{
    [Header("NearScan – Voreinstellung (AE)")]
    [Tooltip("Sinnvoll für lokale Umgebung (z.B. Asteroidengürtel, Orbit-Umfeld).")]
    public float defaultNearAU = 0.02f; // ~3 Mio. km

    void Reset()
    {
        scanRadiusAU = defaultNearAU;
    }

    protected override void Publish(List<SystemObject> entries)
    {
        // Gemeinsame Helper-Methode aus Base nutzen:
        ApplyResultsToViewModelAndNotify<NearScanViewModel>(entries);
    }
}
