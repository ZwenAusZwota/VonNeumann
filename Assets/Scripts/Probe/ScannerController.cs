// Assets/Scripts/Probe/ScannerController.cs
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

[DisallowMultipleComponent]
public class ScannerController : MonoBehaviour
{
    [Header("Scanner Settings (km)")]
    [Tooltip("Maximale Reichweite für Near-Scan (Detail/Umgebung).")]
    public float nearScanRadiusKm = 1_000_000f;       // 1e6 km
    [Tooltip("Maximale Reichweite für Far-Scan (weite Umgebung/System).")]
    public float farScanRadiusKm = 75_000_000f;      // 7.5e7 km

    [Header("Filter")]
    public LayerMask scanLayers = ~0;
    public string[] ignoreTags = new[] { "Player" };

    // interne Buffer, werden bei Publish übergeben
    private readonly List<SystemObject> _near = new();
    private readonly List<SystemObject> _far = new();

    /* ───────────── öffentliche API ───────────── */
    public void PerformNearScan()
    {
        ScanInto(_near, nearScanRadiusKm);
        HUDBindingService.PublishNearScan(gameObject, _near);
    }

    public void PerformFarScan()
    {
        ScanInto(_far, farScanRadiusKm);
        HUDBindingService.PublishFarScan(gameObject, _far);
    }

    /* ───────────── Logik ───────────── */
    private void ScanInto(List<SystemObject> targetList, float radiusKm)
    {
        targetList.Clear();

        float radiusUnits = radiusKm;// / PlanetScale.KM_PER_UNIT;
        Vector3 origin = transform.position;

        var hits = Physics.OverlapSphere(origin, radiusUnits, scanLayers);

        // sortiert nach Distanz
        Array.Sort(hits, (a, b) =>
        {
            float dA2 = (a.transform.position - origin).sqrMagnitude;
            float dB2 = (b.transform.position - origin).sqrMagnitude;
            return dA2.CompareTo(dB2);
        });

        foreach (var col in hits)
        {
            if (ignoreTags != null && ignoreTags.Contains(col.tag)) continue;

            var so = new SystemObject
            {
                Kind = SystemObject.ObjectKind.ScannedObject,
                Id = col.GetInstanceID().ToString(),
                Name = col.tag,
                DisplayName = BuildDisplayName(col.transform, origin),
                Dto = col,
                GameObject = col.gameObject
            };
            targetList.Add(so);
        }
    }

    private string BuildDisplayName(Transform t, Vector3 origin)
    {
        float distUnits = (t.position - origin).magnitude;
        float distKm = distUnits * PlanetScale.KM_PER_UNIT;
        return $"{t.tag} — {(int)distKm:N0} km";
    }
}
