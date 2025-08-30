// Assets/Scripts/Probe/NearScanViewModel.cs
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NearScanViewModel : MonoBehaviour, IScanResultsReceiver
{
    private readonly List<SystemObject> _latest = new();

    public IReadOnlyList<SystemObject> LatestEntries => _latest;

    public void SetResults(List<SystemObject> entries)
    {
        _latest.Clear();
        if (entries != null) _latest.AddRange(entries);
    }
}
