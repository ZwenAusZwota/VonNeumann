using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gruppiert Monde und Satelliten unter ihrem Planeten für FarScan-Baumansicht.
/// </summary>
public static class ScanOrbiterHelper
{
    public static bool IsPlanet(SystemObject entry)
    {
        if (entry?.GameObject == null) return false;
        return entry.GameObject.CompareTag("Planet") || entry.GameObject.GetComponent<Planet>() != null;
    }

    public static bool IsPlanetBody(GameObject go)
    {
        if (go == null) return false;
        return go.CompareTag("Planet") || go.GetComponent<Planet>() != null;
    }

    public static bool IsMoonBody(GameObject go)
    {
        return go != null && go.CompareTag("Moon");
    }

    /// <summary>
    /// Monde (Tag Moon) und direkte Nicht-Planet-Kinder eines Planeten (z. B. Satelliten).
    /// </summary>
    public static bool IsOrbiterBody(GameObject go)
    {
        if (go == null) return false;
        if (IsMoonBody(go)) return true;

        var parent = go.transform.parent;
        if (parent == null) return false;
        return IsPlanetBody(parent.gameObject) && !IsPlanetBody(go);
    }

    public static bool IsSatelliteBody(GameObject go)
    {
        return go != null && IsOrbiterBody(go) && !IsMoonBody(go);
    }

    public static Transform FindParentPlanetTransform(GameObject go)
    {
        if (go == null) return null;

        Transform current = go.transform.parent;
        while (current != null)
        {
            if (IsPlanetBody(current.gameObject))
                return current;
            current = current.parent;
        }

        return null;
    }

    public static void GroupOrbitersUnderPlanets(List<SystemObject> results, Vector3 origin)
    {
        if (results == null || results.Count == 0) return;

        var planetByTransform = new Dictionary<Transform, SystemObject>();
        foreach (var entry in results)
        {
            if (IsPlanet(entry) && entry.GameObject != null)
                planetByTransform[entry.GameObject.transform] = entry;
        }

        var removeFromRoot = new List<SystemObject>();
        foreach (var entry in results)
        {
            if (entry?.GameObject == null) continue;
            if (IsPlanet(entry)) continue;
            if (entry.Kind == SystemObject.ObjectKind.AsteroidBelt) continue;
            if (entry.GameObject.GetComponentInParent<MineableAsteroid>() != null) continue;

            var parentPlanet = FindParentPlanetTransform(entry.GameObject);
            if (parentPlanet == null || !planetByTransform.TryGetValue(parentPlanet, out var planetEntry))
                continue;

            AttachOrbiter(planetEntry, entry, fromNearScan: !entry.RequiresNearScan);
            removeFromRoot.Add(entry);
        }

        foreach (var entry in removeFromRoot)
            results.Remove(entry);

        foreach (var kvp in planetByTransform)
        {
            var planetTransform = kvp.Key;
            var planetEntry = kvp.Value;
            if (planetTransform == null) continue;

            foreach (Transform child in planetTransform)
            {
                if (!IsOrbiterBody(child.gameObject)) continue;
                if (ScanResultsMerger.FindByGameObject(planetEntry.Children, child.gameObject) != null)
                    continue;

                planetEntry.Children.Add(CreateFarScanStub(child, origin));
            }
        }
    }

    public static void PromoteOrbitersToParents(List<SystemObject> catalog)
    {
        if (catalog == null) return;

        foreach (var entry in catalog)
        {
            if (!IsPlanet(entry) || entry.GameObject == null) continue;

            var removeFromRoot = new List<SystemObject>();
            foreach (var root in catalog)
            {
                if (root == entry) continue;
                if (root?.GameObject == null) continue;
                if (!IsOrbiterBody(root.GameObject)) continue;

                var parent = FindParentPlanetTransform(root.GameObject);
                if (parent != entry.GameObject.transform) continue;

                AttachOrbiter(entry, root, fromNearScan: !root.RequiresNearScan);
                removeFromRoot.Add(root);
            }

            foreach (var root in removeFromRoot)
                catalog.Remove(root);
        }
    }

    private static void AttachOrbiter(SystemObject planetEntry, SystemObject orbiterEntry, bool fromNearScan)
    {
        var existing = ScanResultsMerger.FindByGameObject(planetEntry.Children, orbiterEntry.GameObject);
        if (existing != null)
            ScanResultsMerger.UpdateEntry(existing, orbiterEntry, fromNearScan);
        else
            planetEntry.Children.Add(CloneOrbiterEntry(orbiterEntry));
    }

    public static SystemObject CreateFarScanStub(Transform orbiter, Vector3 origin)
    {
        var go = orbiter.gameObject;
        return new SystemObject
        {
            Kind = SystemObject.ObjectKind.ScannedObject,
            Id = go.GetEntityId().ToString(),
            Name = string.IsNullOrWhiteSpace(go.tag) ? "Moon" : go.tag,
            DisplayName = orbiter.name,
            GameObject = go,
            RequiresNearScan = true
        };
    }

    private static SystemObject CloneOrbiterEntry(SystemObject source)
    {
        var clone = ScanResultsMerger.CloneEntry(source);
        if (clone.RequiresNearScan && string.IsNullOrWhiteSpace(clone.DisplayName))
            clone.DisplayName = source.GameObject != null ? source.GameObject.name : source.Name;
        return clone;
    }
}
