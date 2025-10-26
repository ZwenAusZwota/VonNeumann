// Assets/Scripts/Core/Managers/HUDBindingServiceV2.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// Zentraler Daten-Hub zwischen WorldRegistry (Spielobjekte) und HUD (UI-Panels).
/// V2: Verwendet Service Container statt Singleton-Pattern.
/// - Hört auf Registry-Events (Add/Remove/Changed)
/// - Pflegt eine Laufzeitliste von HUDItems
/// - Bietet Events für UI (OnItemAdded/Removed/Changed, OnSelectionChanged)
/// - Stellt Auswahl- und Abfragefunktionen bereit
/// </summary>
public class HUDBindingServiceV2 : MonoBehaviour
{
    // -------------------------- Events für UI --------------------------
    public event Action<HUDItem> OnItemAdded;
    public event Action<Guid> OnItemRemoved;
    public event Action<HUDItem> OnItemChanged;
    public event Action<HUDItem> OnSelectionChanged;
    public event Action<IReadOnlyList<HUDItem>> OnListReset;

    // -------------------------- Zustand --------------------------
    private readonly Dictionary<Guid, HUDItem> _items = new();
    private Guid? _selectedId;

    [Header("Optionen")]
    [Tooltip("Bei Start automatisch auf WorldRegistry warten und binden.")]
    [SerializeField] private bool autoBindToRegistry = true;

    [Tooltip("Maximale Zeit in Sekunden, auf WorldRegistry beim Start zu warten (0 = unendlich).")]
    [SerializeField] private float waitForRegistrySeconds = 0f;

    public IReadOnlyList<HUDItem> Items => _items.Values.ToList().AsReadOnly();
    public HUDItem SelectedItem => _selectedId.HasValue && _items.TryGetValue(_selectedId.Value, out var it) ? it : null;

    private bool _isBound;

    // -------------------------- Lifecycle --------------------------
    private void Awake()
    {
        // Service im Container registrieren
        ServiceContainer.Instance?.RegisterSingleton(this);
    }

    private async void Start()
    {
        if (autoBindToRegistry)
        {
            await BindToRegistryAsync();
        }
    }

    private void OnDestroy()
    {
        UnbindFromRegistry();
    }

    // -------------------------- Registry Binding --------------------------
    
    private async UniTask BindToRegistryAsync()
    {
        if (_isBound) return;

        var startTime = Time.time;
        while (Time.time - startTime < waitForRegistrySeconds || waitForRegistrySeconds <= 0f)
        {
            var worldRegistry = ServiceContainer.Instance.Get<WorldRegistry>();
            if (worldRegistry != null)
            {
                BindToRegistry(worldRegistry);
                return;
            }
            
            await UniTask.Delay(100, cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        
        Debug.LogWarning("[HUDBindingServiceV2] WorldRegistry nicht gefunden - Binding abgebrochen");
    }

    private void BindToRegistry(WorldRegistry registry)
    {
        if (_isBound) return;

        registry.OnEntityAdded += HandleEntityAdded;
        registry.OnEntityRemoved += HandleEntityRemoved;
        registry.OnEntityChanged += HandleEntityChanged;
        
        _isBound = true;
        Debug.Log("[HUDBindingServiceV2] Successfully bound to WorldRegistry");
    }

    private void UnbindFromRegistry()
    {
        if (!_isBound) return;

        var registry = ServiceContainer.Instance.Get<WorldRegistry>();
        if (registry != null)
        {
            registry.OnEntityAdded -= HandleEntityAdded;
            registry.OnEntityRemoved -= HandleEntityRemoved;
            registry.OnEntityChanged -= HandleEntityChanged;
        }
        
        _isBound = false;
    }

    // -------------------------- Event Handlers --------------------------
    
    private void HandleEntityAdded(IRegistrableEntity entity)
    {
        if (entity == null) return;

        var hudItem = BuildHUDItem(entity);
        _items[entity.Guid.Value] = hudItem;
        OnItemAdded?.Invoke(hudItem);
        
        // GameEvents Integration - Note: GameEvents uses SystemObject, but we have IRegistrableEntity
        // We'll need to adapt this or create a bridge
        Debug.Log($"[HUDBindingServiceV2] Entity added: {entity}");
    }

    private void HandleEntityRemoved(Guid entityId)
    {
        if (_items.TryGetValue(entityId, out var item))
        {
            _items.Remove(entityId);
            OnItemRemoved?.Invoke(entityId);
            
            // GameEvents Integration
            Debug.Log($"[HUDBindingServiceV2] Entity removed: {entityId}");
        }
    }

    private void HandleEntityChanged(Guid entityId, HUDPayload payload)
    {
        if (_items.TryGetValue(entityId, out var item))
        {
            // Update item data from payload
            item.Payload = payload;
            OnItemChanged?.Invoke(item);
            
            // GameEvents Integration
            Debug.Log($"[HUDBindingServiceV2] Entity changed: {entityId}");
        }
    }

    // -------------------------- Helper Methods --------------------------
    
    private static HUDItem BuildHUDItem(IRegistrableEntity entity)
    {
        var payload = entity.GetHUDPayload();
        var transform = (entity as Component)?.transform;
        
        return new HUDItem
        {
            Id = entity.Guid.Value,
            TypeId = entity.TypeId,
            Payload = payload,
            Source = entity,
            Transform = transform
        };
    }

    // -------------------------- Public API --------------------------
    
    public void SelectItem(Guid id)
    {
        if (_items.TryGetValue(id, out var item))
        {
            _selectedId = id;
            OnSelectionChanged?.Invoke(item);
            
            // GameEvents Integration
            GameEvents.SelectProbe(item.Transform?.gameObject);
        }
    }

    public void SelectItem(HUDItem item)
    {
        if (item != null && _items.ContainsKey(item.Id))
        {
            SelectItem(item.Id);
        }
    }

    public void ClearSelection()
    {
        _selectedId = null;
        OnSelectionChanged?.Invoke(null);
    }

    public void RefreshAll()
    {
        OnListReset?.Invoke(Items);
    }

    // -------------------------- Static Access (Backward Compatibility) --------------------------
    
    /// <summary>Backward compatibility - wird durch Service Container ersetzt</summary>
    [Obsolete("Use ServiceContainer.Instance.Get<HUDBindingServiceV2>() instead")]
    public static HUDBindingServiceV2 I => ServiceContainer.Instance.Get<HUDBindingServiceV2>();
}
