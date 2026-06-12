using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class BaseScannerController : MonoBehaviour
{
    [Header("Scanner ? Reichweite (AE)")]
    [Tooltip("Scanradius in Astronomischen Einheiten (1 AE ? 149,6 Mio. km).")]
    [Min(0f)]
    public float scanRadiusAU = 0.10f;

    [Header("Filter (f?r Collider-basierte Treffer)")]
    public LayerMask scanLayers = ~0;
    [Tooltip("Zus?tzliche Tags ignorieren. Die eigene Sonde wird immer ausgeschlossen.")]
    public string[] ignoreTags = System.Array.Empty<string>();

    protected const float AU_IN_KM = 149_597_870.7f;

    protected float AuToUnits(float au) =>
        (au * AU_IN_KM) / Mathf.Max(PlanetScale.KM_PER_UNIT, 1e-6f);
    protected float UnitsToKm(float units) =>
        units * PlanetScale.KM_PER_UNIT;
    protected float UnitsToAu(float units) =>
        UnitsToKm(units) / AU_IN_KM;

    public virtual void PerformScan()
    {
        float radiusUnits = AuToUnits(scanRadiusAU);
        Vector3 origin = transform.position;

        var results = new List<SystemObject>(128);
        var seen = new HashSet<EntityId>();

        CollectColliderHits(origin, radiusUnits, results, seen, includeAsteroids: true);
        CollectBeltHits(origin, radiusUnits, results, seen);

        Publish(results);
    }

    protected void CollectColliderHits(
        Vector3 origin,
        float radiusUnits,
        List<SystemObject> results,
        HashSet<EntityId> seen,
        bool includeAsteroids)
    {
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

            GameObject go = col.attachedRigidbody ? col.attachedRigidbody.gameObject : col.gameObject;

            var asteroidRoot = go.GetComponentInParent<MineableAsteroid>();
            if (asteroidRoot != null)
            {
                if (!includeAsteroids) continue;
                go = asteroidRoot.gameObject;
            }

            if (IsPartOfScanningProbe(go)) continue;

            var id = go.GetEntityId();
            if (seen.Contains(id)) continue;
            seen.Add(id);

            bool isAsteroid = asteroidRoot != null;
            results.Add(new SystemObject
            {
                Kind = isAsteroid ? SystemObject.ObjectKind.Asteroid : SystemObject.ObjectKind.ScannedObject,
                Id = id.ToString(),
                Name = go.tag,
                DisplayName = BuildDisplayName(go.transform, origin),
                Dto = col,
                GameObject = go,
                RequiresNearScan = false
            });
        }
    }

    protected void CollectBeltHits(
        Vector3 origin,
        float radiusUnits,
        List<SystemObject> results,
        HashSet<EntityId> seen)
    {
        var belts = FindObjectsByType<AsteroidBelt>();

        foreach (var belt in belts)
        {
            if (belt == null) continue;

            var id = belt.gameObject.GetEntityId();
            if (seen.Contains(id)) continue;

            float distUnits = BeltNearestDistanceUnits(belt, origin, out _, out _);
            if (distUnits > radiusUnits) continue;

            Transform closestAsteroid = belt.GetClosestAsteroid(origin);
            GameObject targetObject = belt.gameObject;

            if (IsPartOfScanningProbe(targetObject)) continue;

            if (closestAsteroid != null)
                seen.Add(closestAsteroid.gameObject.GetEntityId());

            results.Add(new SystemObject
            {
                Kind = SystemObject.ObjectKind.AsteroidBelt,
                Id = id.ToString(),
                Name = "AsteroidBelt",
                DisplayName = BuildDisplayNameForBelt(belt, origin, closestAsteroid),
                Dto = null,
                GameObject = targetObject,
                RequiresNearScan = false
            });
            seen.Add(id);
        }
    }

    /// <summary>Anzeigename ohne Distanz (Distanz kommt in die Scan-Liste rechts).</summary>
    protected virtual string BuildDisplayName(Transform t, Vector3 origin)
    {
        if (!string.IsNullOrWhiteSpace(t.name))
            return t.name.Trim();
        return string.IsNullOrWhiteSpace(t.tag) ? "Object" : t.tag;
    }

    /// <summary>Anzeigename für Gürtel ohne Distanz.</summary>
    protected virtual string BuildDisplayNameForBelt(AsteroidBelt belt, Vector3 origin, Transform nearestAsteroid)
    {
        return string.IsNullOrWhiteSpace(belt.name) ? "Asteroidengürtel" : belt.name.Trim();
    }

    /// <summary>
    /// K?rzeste Entfernung von 'pos' zur Belt-Ringfl?che in Units.
    /// Gibt zus?tzlich den n?chstgelegenen Punkt 'nearestPoint' und den zugeh?rigen Zielradius zur?ck.
    /// </summary>
    protected static float BeltNearestDistanceUnits(AsteroidBelt belt, Vector3 pos, out Vector3 nearestPoint, out float targetRadius)
    {
        var cfg = belt.ToResolvedConfig();
        float inner = cfg.innerRadiusUU;
        float outer = cfg.outerRadiusUU;

        Vector3 C = belt.transform.position;
        Vector3 N = belt.transform.up;

        Vector3 toProbe = pos - C;
        Vector3 inPlane = Vector3.ProjectOnPlane(toProbe, N);

        if (inPlane.sqrMagnitude < 1e-10f)
            inPlane = belt.transform.forward;

        float r = inPlane.magnitude;

        if (r < inner) targetRadius = inner;
        else if (r > outer) targetRadius = outer;
        else
        {
            float dInner = r - inner;
            float dOuter = outer - r;
            targetRadius = (dInner <= dOuter) ? inner : outer;
        }

        Vector3 radialDir = inPlane.normalized;
        nearestPoint = C + radialDir * targetRadius;

        return Vector3.Distance(pos, nearestPoint);
    }

    /// <summary>Eigene Sonde (Scanner-Hierarchie) von Ergebnissen ausschlie?en.</summary>
    protected bool IsPartOfScanningProbe(GameObject go)
    {
        if (go == null) return false;

        Transform probeRoot = GetScanningProbeRoot();
        Transform t = go.transform;
        return t == probeRoot || t.IsChildOf(probeRoot);
    }

    private Transform GetScanningProbeRoot()
    {
        var probe = GetComponentInParent<ProbeController>();
        return probe != null ? probe.transform : transform.root;
    }

    /// <summary> Von Near/Far-Spezialisierungen zu implementieren. </summary>
    protected abstract void Publish(List<SystemObject> entries);

    // --------- Gemeinsamer Helper f?r Near/Far: ViewModel bef?llen + HUD-Refresh ----------
    /// <summary>
    /// Schreibt die Scan-Ergebnisse in ein ViewModel-Component (wird bei Bedarf angelegt)
    /// und triggert danach ein HUD-Update via WorldRegistry.
    /// </summary>
    protected void ApplyResultsToViewModelAndNotify<TViewModel>(List<SystemObject> entries)
        where TViewModel : Component, IScanResultsReceiver
    {
        // ViewModel holen/erzeugen
        var vm = GetComponent<TViewModel>();
        if (vm == null) vm = gameObject.AddComponent<TViewModel>();
        vm.SetResults(entries ?? new List<SystemObject>());

        // HUD informieren
        var reg = GetComponent<RegistrableEntity>();
        if (reg != null)
        {
            var worldRegistry = ServiceContainer.Instance?.Get<WorldRegistry>();
            if (worldRegistry != null)
                worldRegistry.NotifyChanged(reg.Guid);
        }
    }
}

/// <summary>
/// Minimales Interface f?r Scanner-ViewModels, damit Base die Ergebnisse einf?llen kann.
/// </summary>
public interface IScanResultsReceiver
{
    void SetResults(List<SystemObject> entries);
}
