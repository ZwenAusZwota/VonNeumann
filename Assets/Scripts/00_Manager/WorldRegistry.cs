using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Zentrale Registrierung aller interaktiven Spielobjekte.
/// Wird am AppRoot (00_Bootstrap) betrieben und überlebt Szenenwechsel.
/// </summary>
public class WorldRegistry : MonoBehaviour
{
    public static WorldRegistry I { get; private set; }

    private readonly Dictionary<Guid, IRegistrableEntity> _entities = new();

    /// <summary>Fired when an entity has been added to the registry.</summary>
    public event Action<IRegistrableEntity> OnEntityAdded;

    /// <summary>Fired when an entity has been removed from the registry.</summary>
    public event Action<Guid> OnEntityRemoved;

    /// <summary>Fired when an entity indicates its HUD payload/state changed.</summary>
    public event Action<Guid, HUDPayload> OnEntityChanged;

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>All currently registered entities.</summary>
    public IEnumerable<IRegistrableEntity> All => _entities.Values;

    /// <summary>Number of registered entities.</summary>
    public int Count => _entities.Count;

    /// <summary>Register (or replace) an entity by its GUID.</summary>
    public void Register(IRegistrableEntity e)
    {
        if (e == null) return;
        var id = e.Guid.Value;
        if (id == Guid.Empty)
        {
            Debug.LogWarning($"[WorldRegistry] Entity {NameOf(e)} has empty GUID and cannot be registered.");
            return;
        }

        if (_entities.TryGetValue(id, out var existing) && !ReferenceEquals(existing, e))
        {
            Debug.LogWarning($"[WorldRegistry] Duplicate GUID detected ({id}) between {NameOf(existing)} and {NameOf(e)}. Replacing mapping with newest.");
        }

        _entities[id] = e;
        OnEntityAdded?.Invoke(e);
        // Optional: initial change push
        NotifyChanged(id);
    }

    /// <summary>Unregister an entity.</summary>
    public void Unregister(IRegistrableEntity e)
    {
        if (e == null) return;
        var id = e.Guid.Value;
        if (id == Guid.Empty) return;

        if (_entities.Remove(id))
            OnEntityRemoved?.Invoke(id);
    }

    /// <summary>Notify listeners that an entity's HUD payload/state changed.</summary>
    public void NotifyChanged(SerializedGuid id) => NotifyChanged(id.Value);

    public void NotifyChanged(Guid id)
    {
        if (id == Guid.Empty) return;
        if (_entities.TryGetValue(id, out var e))
        {
            var payload = e.GetHUDPayload();
            OnEntityChanged?.Invoke(id, payload);
        }
    }

    /// <summary>Try get a registered entity by GUID.</summary>
    public bool TryGet(Guid id, out IRegistrableEntity entity) => _entities.TryGetValue(id, out entity);

    /// <summary>Returns a snapshot list (useful before destroying many entries).</summary>
    public List<IRegistrableEntity> Snapshot() => _entities.Values.ToList();

    private static string NameOf(IRegistrableEntity e)
        => (e as Component) ? ((Component)e).name : e?.ToString() ?? "<null>";
}
