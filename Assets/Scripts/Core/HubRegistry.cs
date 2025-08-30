using System.Collections.Generic;
using UnityEngine;
using System;

public class HubRegistry : MonoBehaviour
{
    public static HubRegistry Instance { get; private set; }
    void Awake() { if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); } else Destroy(gameObject); }

    [System.Serializable]
    public class HubInfo
    {
        public string Id;             // stabile ID (GUID)
        public string DisplayName;    // z.B. "Sonde-01" / "Fabrik-Beta"
        public string Kind;           // "Probe", "Factory", ...
        public Vector3 LastKnownPos;  // optional
    }

    readonly Dictionary<string, HubInfo> _byId = new();
    public IEnumerable<HubInfo> All() => _byId.Values;

    public void RegisterOrUpdate(HubInfo info) => _byId[info.Id] = info;

    // Convenience: Liste für UI
    public List<(string id, string label)> GetOptions()
    {
        var list = new List<(string, string)>();
        foreach (var h in _byId.Values)
            list.Add((h.Id, $"{h.DisplayName} ({h.Kind})"));

        // Sortiere nach Label (Item2)
        list.Sort((a, b) => string.Compare(a.Item2, b.Item2, StringComparison.Ordinal));
        return list;
    }

}
