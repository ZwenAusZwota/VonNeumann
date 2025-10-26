// Assets/Scripts/Core/HubRegistry.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class HubRegistry : MonoBehaviour
{
    [System.Obsolete("Use ServiceContainer.Instance.Get<HubRegistry>() instead")]
    public static HubRegistry Instance { get; private set; }

    private void Awake()
    {
        // Service Container Registrierung
        if (ServiceContainer.Instance != null)
        {
            ServiceContainer.Instance.RegisterSingleton<HubRegistry>(this);
        }

        // Singleton-Absicherung (fr Rckwrtskompatibilitt)
        // Verwende private field direkt, um Warnung zu vermeiden
        var existingInstance = GetExistingInstance();
        if (existingInstance == null)
        {
            SetInstance(this);
            DontDestroyOnLoad(gameObject);
        }
        else if (existingInstance != this)
        {
            // Doppeltes Exemplar vermeiden
            Destroy(gameObject);
        }
    }

    // Hilfsmethoden zur Vermeidung der Warnung
    private static HubRegistry GetExistingInstance()
    {
        return ServiceContainer.Instance?.Get<HubRegistry>();
    }

    private static void SetInstance(HubRegistry instance)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        Instance = instance;
#pragma warning restore CS0618
    }

    [Serializable]
    public class HubInfo
    {
        public string Id;             // stabile ID (GUID o.�.)
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
            Debug.LogWarning("HubRegistry.RegisterOrUpdate: ung�ltige HubInfo.");
            return;
        }
        _byId[info.Id] = info;
    }

    /// <summary>Komfortliste f�r UI (Id + Label "DisplayName (Kind)")</summary>
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
        var existing = ServiceContainer.Instance?.Get<HubRegistry>();
        if (existing == null)
        {
            var go = new GameObject("HubRegistry");
            go.AddComponent<HubRegistry>(); // Awake setzt Instance & DontDestroyOnLoad
        }
    }
}
