using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hält die letzten FarScan-Ergebnisse. Implementiert IScanResultsReceiver,
/// damit BaseScannerController die Liste direkt setzen kann.
/// </summary>
[DisallowMultipleComponent]
public class FarScanViewModel : MonoBehaviour, IScanResultsReceiver
{
    private readonly List<SystemObject> _latest = new();

    public IReadOnlyList<SystemObject> LatestEntries => _latest;

    public void SetResults(List<SystemObject> entries)
    {
        _latest.Clear();
        if (entries != null) _latest.AddRange(entries);
    }
}
