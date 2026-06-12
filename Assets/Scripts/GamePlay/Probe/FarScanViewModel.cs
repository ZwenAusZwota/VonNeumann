using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// H�lt die letzten FarScan-Ergebnisse. Implementiert IScanResultsReceiver,
/// damit BaseScannerController die Liste direkt setzen kann.
/// </summary>
[DisallowMultipleComponent]
public class FarScanViewModel : MonoBehaviour, IScanResultsReceiver
{
    private readonly List<SystemObject> _latest = new();

    public IReadOnlyList<SystemObject> LatestEntries => _latest;

    public void SetResults(List<SystemObject> entries)
    {
        ScanResultsMerger.Merge(_latest, entries, fromNearScan: false);
    }
}
