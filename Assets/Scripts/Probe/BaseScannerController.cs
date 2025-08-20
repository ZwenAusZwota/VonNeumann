using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class BaseScannerController : MonoBehaviour
{
    [Header("Scanner – Reichweite (AE)")]
    [Tooltip("Scanradius in Astronomischen Einheiten (1 AE ≈ 149,6 Mio. km).")]
    [Min(0f)]
    public float scanRadiusAU = 0.10f;

    [Header("Filter (für Collider-basierte Treffer)")]
    public LayerMask scanLayers = ~0;
    public string[] ignoreTags = new[] { "Player" };

    protected const float AU_IN_KM = 149_597_870.7f;

    protected float AuToUnits(float au) =>
        (au * AU_IN_KM) / Mathf.Max(PlanetScale.KM_PER_UNIT, 1e-6f);
    protected float UnitsToKm(float units) =>
        units * PlanetScale.KM_PER_UNIT;
    protected float UnitsToAu(float units) =>
        UnitsToKm(units) / AU_IN_KM;

    public void PerformScan()
    {
        float radiusUnits = AuToUnits(scanRadiusAU);
        Vector3 origin = transform.position;

        var results = new List<SystemObject>(128);
        var seen = new HashSet<int>();

        /* ---------- 1) Collider-basierte Treffer (Planeten, Asteroiden, Stationen …) ---------- */
        var hits = Physics.OverlapSphere(origin, radiusUnits, scanLayers);
        Array.Sort(hits, (a, b) =>
        {
            float dA2 = (a.transform.position - origin).sqrMagnitude;
            float dB2 = (b.transform.position - origin).sqrMagnitude;
            return dA2.CompareTo(dB2);
        });

        foreach (var col in hits)
        {
            if (col == null) continue;
            if (ignoreTags != null && ignoreTags.Contains(col.tag)) continue;

            int id = col.GetInstanceID();
            if (seen.Contains(id)) continue;
            seen.Add(id);

            results.Add(new SystemObject
            {
                Kind = SystemObject.ObjectKind.ScannedObject,
                Id = id.ToString(),
                Name = col.tag,
                DisplayName = BuildDisplayName(col.transform, origin),
                Dto = col,
                GameObject = col.gameObject
            });
        }

        /* ---------- 2) Gürtel ohne Collider: geometrisch prüfen & hinzufügen ---------- */
        var belts = FindObjectsByType<AsteroidBelt>( FindObjectsSortMode.None);  // keine Sortierung nötig → deutlich schneller

        foreach (var belt in belts)
        {
            if (belt == null) continue;

            // wenn es doch einen Collider gibt und bereits gesehen: überspringen
            int id = belt.gameObject.GetInstanceID();
            if (seen.Contains(id))
                continue;

            // kürzeste Distanz von 'origin' zur Ringfläche (in Units)
            float distUnits = BeltNearestDistanceUnits(belt, origin, out Vector3 nearestPoint, out float targetRadius);

            if (distUnits <= radiusUnits)
            {
                results.Add(new SystemObject
                {
                    Kind = SystemObject.ObjectKind.ScannedObject,
                    Id = id.ToString(),
                    Name = "AsteroidBelt",
                    DisplayName = BuildDisplayNameForBelt(belt, origin, nearestPoint),
                    Dto = null, // kein Collider nötig
                    GameObject = belt.gameObject
                });
                seen.Add(id);
            }
        }

        Publish(results);
    }

    /// <summary> Standard-Anzeige: unter 0,05 AE in km, sonst AE. </summary>
    protected virtual string BuildDisplayName(Transform t, Vector3 origin)
    {
        float distUnits = (t.position - origin).magnitude;
        float distAu = UnitsToAu(distUnits);
        if (distAu >= 0.05f)
            return $"{t.tag} — {distAu:0.###} AU";
        float distKm = UnitsToKm(distUnits);
        return $"{t.tag} — {(int)distKm:N0} km";
    }

    /// <summary> Schöne Anzeige für Belts: Distanz zum nächsten Randpunkt. </summary>
    protected virtual string BuildDisplayNameForBelt(AsteroidBelt belt, Vector3 origin, Vector3 nearestPoint)
    {
        float distUnits = (nearestPoint - origin).magnitude;
        float distAu = UnitsToAu(distUnits);
        string range = $"[{belt.innerRadius:0.#}..{belt.outerRadius:0.#} u]";
        // Hinweis: 'u' = Unity Units. Wenn du möchtest, rechne inner/outer ebenfalls in km/AU um.
        return $"AsteroidBelt — {distAu:0.###} AU (edge) {range}";
    }

    /// <summary>
    /// Kürzeste Entfernung von 'pos' zur Belt-Ringfläche in Units.
    /// Gibt zusätzlich den nächstgelegenen Punkt 'nearestPoint' und den zugehörigen Zielradius zurück.
    /// </summary>
    protected static float BeltNearestDistanceUnits(AsteroidBelt belt, Vector3 pos, out Vector3 nearestPoint, out float targetRadius)
    {
        Vector3 C = belt.transform.position;   // Belt-Zentrum
        Vector3 N = belt.transform.up;         // Normal der Belt-Ebene

        // Projektion in die Belt-Ebene
        Vector3 toProbe = pos - C;
        Vector3 inPlane = Vector3.ProjectOnPlane(toProbe, N);

        // Falls exakt auf der Normalen: ersatzweise eine Richtung in der Ebene wählen
        if (inPlane.sqrMagnitude < 1e-10f)
            inPlane = belt.transform.forward;

        float r = inPlane.magnitude;

        // Zielradius = nächstgelegene Kante (inner oder outer)
        if (r < belt.innerRadius) targetRadius = belt.innerRadius;
        else if (r > belt.outerRadius) targetRadius = belt.outerRadius;
        else
        {
            float dInner = r - belt.innerRadius;
            float dOuter = belt.outerRadius - r;
            targetRadius = (dInner <= dOuter) ? belt.innerRadius : belt.outerRadius;
        }

        Vector3 radialDir = inPlane.normalized;
        nearestPoint = C + radialDir * targetRadius;

        // Distanz von pos zum nächstgelegenen Punkt auf dem Ring
        return Vector3.Distance(pos, nearestPoint);
    }

    /// <summary> Von Near/Far-Spezialisierungen zu implementieren. </summary>
    protected abstract void Publish(List<SystemObject> entries);
}
