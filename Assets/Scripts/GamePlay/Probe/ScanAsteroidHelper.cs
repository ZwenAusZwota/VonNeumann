using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gruppiert per NearScan gefundene Asteroiden unter ihrem Asteroidengürtel.
/// </summary>
public static class ScanAsteroidHelper
{
    public static bool IsBeltEntry(SystemObject entry)
    {
        if (entry?.GameObject == null) return false;
        return entry.Kind == SystemObject.ObjectKind.AsteroidBelt
               || entry.GameObject.GetComponent<AsteroidBelt>() != null;
    }

    public static bool IsAsteroidBody(GameObject go)
    {
        return go != null && go.GetComponentInParent<MineableAsteroid>() != null;
    }

    public static void GroupAsteroidsUnderBelts(List<SystemObject> catalog)
    {
        if (catalog == null || catalog.Count == 0) return;

        var beltByTransform = new Dictionary<Transform, SystemObject>();
        foreach (var entry in catalog)
        {
            if (!IsBeltEntry(entry) || entry.GameObject == null) continue;
            beltByTransform[entry.GameObject.transform] = entry;
        }

        var removeFromRoot = new List<SystemObject>();
        foreach (var entry in catalog)
        {
            if (entry?.GameObject == null) continue;
            if (IsBeltEntry(entry)) continue;
            if (!IsAsteroidBody(entry.GameObject)) continue;

            var belt = entry.GameObject.GetComponentInParent<AsteroidBelt>();
            if (belt == null) continue;
            if (!beltByTransform.TryGetValue(belt.transform, out var beltEntry)) continue;

            AttachAsteroid(beltEntry, entry);
            removeFromRoot.Add(entry);
        }

        foreach (var entry in removeFromRoot)
            catalog.Remove(entry);
    }

    private static void AttachAsteroid(SystemObject beltEntry, SystemObject asteroidEntry)
    {
        var existing = ScanResultsMerger.FindByGameObject(beltEntry.Children, asteroidEntry.GameObject);
        if (existing != null)
            ScanResultsMerger.UpdateEntry(existing, asteroidEntry, fromNearScan: true);
        else
            beltEntry.Children.Add(ScanResultsMerger.CloneEntry(asteroidEntry));
    }
}
