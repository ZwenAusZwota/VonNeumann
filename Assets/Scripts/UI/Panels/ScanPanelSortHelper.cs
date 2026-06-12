using System.Collections.Generic;

using UnityEngine;



public enum ScanSortMode

{

    ProbeDistance,

    StarDistance

}



public static class ScanPanelSortHelper

{

    private const float AU_IN_KM = 149_597_870.7f;



    public static float GetProbeDistanceUnits(Vector3 probePosition, SystemObject entry)

    {

        if (entry?.GameObject == null) return float.PositiveInfinity;

        return Vector3.Distance(probePosition, entry.GameObject.transform.position);

    }



    public static float GetStarDistanceUnits(Transform star, SystemObject entry)

    {

        if (entry?.GameObject == null) return float.PositiveInfinity;

        if (star == null) return float.PositiveInfinity;

        return Vector3.Distance(star.position, entry.GameObject.transform.position);

    }



    public static float UnitsToAu(float units)

    {

        float km = units * Mathf.Max(PlanetScale.KM_PER_UNIT, 1e-6f);

        return km / AU_IN_KM;

    }



    public static void Sort(List<SystemObject> entries, Vector3 probePosition, Transform star, ScanSortMode mode)

    {

        entries.Sort((a, b) => CompareDistance(a, b, probePosition, star, mode));



        foreach (var entry in entries)

        {

            if (entry?.Children == null || entry.Children.Count <= 1) continue;

            entry.Children.Sort((a, b) => CompareDistance(a, b, probePosition, star, mode));

        }

    }



    private static int CompareDistance(

        SystemObject a,

        SystemObject b,

        Vector3 probePosition,

        Transform star,

        ScanSortMode mode)

    {

        float da = mode == ScanSortMode.StarDistance

            ? GetStarDistanceUnits(star, a)

            : GetProbeDistanceUnits(probePosition, a);

        float db = mode == ScanSortMode.StarDistance

            ? GetStarDistanceUnits(star, b)

            : GetProbeDistanceUnits(probePosition, b);

        return da.CompareTo(db);

    }



    public static List<SystemObject> MergeDistinct(IEnumerable<SystemObject> near, IEnumerable<SystemObject> far)

    {

        return ScanResultsMerger.BuildDisplayCatalog(

            far as IReadOnlyList<SystemObject> ?? ToList(far),

            near as IReadOnlyList<SystemObject> ?? ToList(near));

    }



    private static List<SystemObject> ToList(IEnumerable<SystemObject> source)

    {

        var list = new List<SystemObject>();

        if (source == null) return list;

        foreach (var entry in source)

            list.Add(entry);

        return list;

    }

}

