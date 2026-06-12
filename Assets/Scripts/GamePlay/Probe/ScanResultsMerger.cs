using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Führt Scan-Ergebnisse zusammen und behält bekannte Objekte bei.
/// </summary>
public static class ScanResultsMerger
{
    public static void Merge(List<SystemObject> catalog, IReadOnlyList<SystemObject> incoming, bool fromNearScan)
    {
        if (catalog == null || incoming == null) return;

        foreach (var entry in incoming)
        {
            if (entry?.GameObject == null) continue;
            MergeEntry(catalog, entry, fromNearScan);
        }

        RemoveDestroyed(catalog);
        ScanOrbiterHelper.PromoteOrbitersToParents(catalog);
        ScanAsteroidHelper.GroupAsteroidsUnderBelts(catalog);
    }

    public static List<SystemObject> BuildDisplayCatalog(
        IReadOnlyList<SystemObject> farEntries,
        IReadOnlyList<SystemObject> nearEntries)
    {
        var catalog = new List<SystemObject>();
        Merge(catalog, farEntries, fromNearScan: false);
        Merge(catalog, nearEntries, fromNearScan: true);
        return catalog;
    }

    private static void MergeEntry(List<SystemObject> catalog, SystemObject incoming, bool fromNearScan)
    {
        var existing = FindByGameObject(catalog, incoming.GameObject);
        if (existing != null)
        {
            UpdateEntry(existing, incoming, fromNearScan);
            return;
        }

        catalog.Add(CloneEntry(incoming));
    }

    public static void UpdateEntry(SystemObject existing, SystemObject incoming, bool fromNearScan)
    {
        if (existing == null || incoming == null) return;

        if (!string.IsNullOrWhiteSpace(incoming.Id))
            existing.Id = incoming.Id;
        if (!string.IsNullOrWhiteSpace(incoming.Name))
            existing.Name = incoming.Name;
        if (!string.IsNullOrWhiteSpace(incoming.DisplayName))
        {
            if (fromNearScan || existing.RequiresNearScan || string.IsNullOrWhiteSpace(existing.DisplayName))
                existing.DisplayName = incoming.DisplayName;
        }

        if (incoming.Dto != null)
            existing.Dto = incoming.Dto;
        if (incoming.GameObject != null)
            existing.GameObject = incoming.GameObject;

        existing.Kind = incoming.Kind;
        if (fromNearScan)
            existing.RequiresNearScan = false;
        else if (incoming.RequiresNearScan)
            existing.RequiresNearScan = true;

        foreach (var child in incoming.Children)
        {
            if (child?.GameObject == null) continue;

            var existingChild = FindByGameObject(existing.Children, child.GameObject);
            if (existingChild != null)
                UpdateEntry(existingChild, child, fromNearScan);
            else
                existing.Children.Add(CloneEntry(child));
        }
    }

    public static SystemObject FindByGameObject(IEnumerable<SystemObject> entries, GameObject go)
    {
        if (entries == null || go == null) return null;

        foreach (var entry in entries)
        {
            if (entry?.GameObject == null) continue;
            if (entry.GameObject == go)
                return entry;

            var nested = FindByGameObject(entry.Children, go);
            if (nested != null)
                return nested;
        }

        return null;
    }

    public static SystemObject CloneEntry(SystemObject source)
    {
        if (source == null) return null;

        var clone = new SystemObject
        {
            Kind = source.Kind,
            Id = source.Id,
            Name = source.Name,
            DisplayName = source.DisplayName,
            Dto = source.Dto,
            GameObject = source.GameObject,
            RequiresNearScan = source.RequiresNearScan
        };

        foreach (var child in source.Children)
        {
            var childClone = CloneEntry(child);
            if (childClone != null)
                clone.Children.Add(childClone);
        }

        return clone;
    }

    private static void RemoveDestroyed(List<SystemObject> catalog)
    {
        for (int i = catalog.Count - 1; i >= 0; i--)
        {
            var entry = catalog[i];
            if (entry == null || entry.GameObject == null)
            {
                catalog.RemoveAt(i);
                continue;
            }

            RemoveDestroyed(entry.Children);
        }
    }
}
