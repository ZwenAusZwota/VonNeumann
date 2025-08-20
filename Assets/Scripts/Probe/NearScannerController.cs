using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NearScannerController : BaseScannerController
{
    [Header("NearScan � Voreinstellung (AE)")]
    [Tooltip("Sinnvoll f�r lokale Umgebung (z.B. Asteroideng�rtel, Orbit-Umfeld).")]
    public float defaultNearAU = 0.02f; // ~3 Mio. km

    void Reset()
    {
        scanRadiusAU = defaultNearAU;
    }

    protected override void Publish(List<SystemObject> entries)
    {
        // Nur weiterreichen, wenn dieses GameObject aktuell im HUD gebunden ist.
        HUDBindingService.PublishNearScan(gameObject, entries); // nutzt bestehende HUDBindingService API. 
    }
}
