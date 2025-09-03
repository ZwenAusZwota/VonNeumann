// Assets/Scripts/Core/HubRegistry.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class HubRegistry : MonoBehaviour
{
    public static HubRegistry Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // Doppeltes Exemplar vermeiden
            Destroy(gameObject);
        }
    }

    [Serializable]
    public class HubInfo
    {
        public string Id;             // stabile ID (GUID o.ä.)
        public string DisplayName;    // z.B. "Sonde-01" / "Fabrik-Beta"
        public string Kind;           // "Probe", "Factory", ...
        public Vector3 LastKnownPos;  // optional
    }

    private readonly Dictionary<string, HubInfo> _byId = new Dictionary<string, HubInfo>();

    public IEnumerable<HubInfo> All() => _byId.Values;

    public bool TryGet(string id, out HubInfo info) => _byId.TryGetValue(id, out info);

    public void RegisterOrUpdate(HubInfo info)
    {
        if (info == null || string.IsNullOrWhiteSpace(info.Id))
        {
            Debug.LogWarning("HubRegistry.RegisterOrUpdate: ungültige HubInfo.");
            return;
        }
        _byId[info.Id] = info;
    }

    /// <summary>Komfortliste für UI (Id + Label "DisplayName (Kind)")</summary>
    public List<(string id, string label)> GetOptions()
    {
        var list = new List<(string, string)>();
        foreach (var h in _byId.Values)
            list.Add((h.Id, $"{h.DisplayName} ({h.Kind})"));

        list.Sort((a, b) => string.Compare(a.Item2, b.Item2, StringComparison.Ordinal));
        return list;
    }

    // Erstellt automatisch eine HubRegistry vor dem Laden der ersten Szene,
    // falls noch keine vorhanden ist.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance == null)
        {
            var go = new GameObject("HubRegistry");
            go.AddComponent<HubRegistry>(); // Awake setzt Instance & DontDestroyOnLoad
        }
    }
}
