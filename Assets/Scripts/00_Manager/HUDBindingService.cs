// Assets/Scripts/00_Manager/HUDBindingService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// Zentraler Daten-Hub zwischen WorldRegistry (Spielobjekte) und HUD (UI-Panels).
/// - Hört auf Registry-Events (Add/Remove/Changed)
/// - Pflegt eine Laufzeitliste von HUDItems
/// - Bietet Events für UI (OnItemAdded/Removed/Changed, OnSelectionChanged)
/// - Stellt Auswahl- und Abfragefunktionen bereit
/// </summary>
public class HUDBindingService : MonoBehaviour
{
    public static HUDBindingService I { get; private set; }

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

    public IReadOnlyCollection<HUDItem> Items => _items.Values.ToList().AsReadOnly();
    public HUDItem SelectedItem => _selectedId.HasValue && _items.TryGetValue(_selectedId.Value, out var it) ? it : null;

    private bool _isBound;

    // -------------------------- Lifecycle --------------------------
    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        if (!autoBindToRegistry) return;

        if (WorldRegistry.I == null)
        {
            if (waitForRegistrySeconds > 0f)
            {
                using var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(waitForRegistrySeconds));
                try
                {
                    await UniTask.WaitUntil(() => WorldRegistry.I != null, cancellationToken: cts.Token);
                }
                catch (OperationCanceledException)
                {
                    Debug.LogWarning("[HUDBindingService] WorldRegistry nicht gefunden (Timeout). Manuelles Bind erforderlich.");
                    return;
                }
            }
            else
            {
                await UniTask.WaitUntil(() => WorldRegistry.I != null);
            }
        }

        BindToRegistry(WorldRegistry.I);
        InitialSyncFromRegistry();
    }

    private void OnDestroy()
    {
        UnbindFromRegistry();
        if (I == this) I = null;
    }

    // -------------------------- Binding zur Registry --------------------------
    public void BindToRegistry(WorldRegistry registry)
    {
        if (registry == null || _isBound) return;

        registry.OnEntityAdded += HandleEntityAdded;
        registry.OnEntityRemoved += HandleEntityRemoved;
        registry.OnEntityChanged += HandleEntityChanged;
        _isBound = true;
    }

    public void UnbindFromRegistry()
    {
        if (!_isBound) return;
        var reg = WorldRegistry.I;
        if (reg != null)
        {
            reg.OnEntityAdded -= HandleEntityAdded;
            reg.OnEntityRemoved -= HandleEntityRemoved;
            reg.OnEntityChanged -= HandleEntityChanged;
        }
        _isBound = false;
    }

    private void InitialSyncFromRegistry()
    {
        _items.Clear();

        var reg = WorldRegistry.I;
        if (reg != null)
        {
            foreach (var e in reg.All)
            {
                var item = BuildItem(e);
                _items[item.Id] = item;
            }
        }

        if (_selectedId.HasValue && !_items.ContainsKey(_selectedId.Value))
            _selectedId = null;

        OnListReset?.Invoke(_items.Values.ToList());
        if (SelectedItem != null) OnSelectionChanged?.Invoke(SelectedItem);
    }

    // -------------------------- Registry-Event-Handler --------------------------
    private void HandleEntityAdded(IRegistrableEntity e)
    {
        if (e == null) return;
        var id = e.Guid.Value;
        if (id == Guid.Empty) return;

        var item = BuildItem(e);
        _items[id] = item;
        OnItemAdded?.Invoke(item);
    }

    private void HandleEntityRemoved(Guid id)
    {
        if (_items.Remove(id))
        {
            OnItemRemoved?.Invoke(id);

            if (_selectedId.HasValue && _selectedId.Value == id)
            {
                _selectedId = null;
                OnSelectionChanged?.Invoke(null);
            }
        }
    }

    private void HandleEntityChanged(Guid id, HUDPayload payload)
    {
        if (_items.TryGetValue(id, out var item))
        {
            item.Payload = payload;
            OnItemChanged?.Invoke(item);

            if (_selectedId.HasValue && _selectedId.Value == id)
                OnSelectionChanged?.Invoke(item);
        }
        else
        {
            var reg = WorldRegistry.I;
            if (reg != null && reg.TryGet(id, out var entity))
            {
                var fresh = BuildItem(entity);
                _items[id] = fresh;
                OnItemAdded?.Invoke(fresh);
            }
        }
    }

    // -------------------------- Öffentliche API fürs HUD --------------------------
    public void Select(Guid? id)
    {
        if (id.HasValue && _items.TryGetValue(id.Value, out var item))
        {
            _selectedId = id.Value;
            OnSelectionChanged?.Invoke(item);
        }
        else
        {
            _selectedId = null;
            OnSelectionChanged?.Invoke(null);
        }
    }

    public void Select(IRegistrableEntity entity)
    {
        if (entity == null) { Select((Guid?)null); return; }
        Select(entity.Guid.Value);
    }

    public void Select(GameObject go)
    {
        if (!go) { Select((Guid?)null); return; }
        var reg = go.GetComponent<IRegistrableEntity>();
        Select(reg);
    }

    public void ClearSelection() => Select((Guid?)null);

    public bool TryGet(Guid id, out HUDItem item) => _items.TryGetValue(id, out item);

    public List<HUDItem> GetSnapshot(Func<HUDItem, bool> filter = null, Comparison<HUDItem> sort = null)
    {
        IEnumerable<HUDItem> q = _items.Values;
        if (filter != null) q = q.Where(filter);
        var list = q.ToList();
        if (sort != null) list.Sort(sort);
        return list;
    }

    public void SelectNext(Func<HUDItem, bool> filter = null, Comparison<HUDItem> sort = null)
    {
        var list = GetSnapshot(filter, sort);
        if (list.Count == 0) { ClearSelection(); return; }

        if (!_selectedId.HasValue)
        {
            Select(list[0].Id);
            return;
        }

        var idx = list.FindIndex(it => it.Id == _selectedId.Value);
        var next = (idx >= 0 && idx + 1 < list.Count) ? list[idx + 1] : list[0];
        Select(next.Id);
    }

    public void SelectPrevious(Func<HUDItem, bool> filter = null, Comparison<HUDItem> sort = null)
    {
        var list = GetSnapshot(filter, sort);
        if (list.Count == 0) { ClearSelection(); return; }

        if (!_selectedId.HasValue)
        {
            Select(list[^1].Id);
            return;
        }

        var idx = list.FindIndex(it => it.Id == _selectedId.Value);
        var prev = (idx > 0) ? list[idx - 1] : list[^1];
        Select(prev.Id);
    }

    public HUDItem FindNearest(Vector3 position, Func<HUDItem, bool> filter = null)
    {
        HUDItem best = null;
        float bestDist = float.MaxValue;

        foreach (var it in _items.Values)
        {
            if (filter != null && !filter(it)) continue;
            var t = it.Transform;
            if (!t) continue;
            float d = (t.position - position).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = it;
            }
        }
        return best;
    }

    public void SelectNearest(Vector3 position, Func<HUDItem, bool> filter = null)
    {
        var it = FindNearest(position, filter);
        Select(it != null ? it.Id : (Guid?)null);
    }

    // -------------------------- Hilfen --------------------------
    private static HUDItem BuildItem(IRegistrableEntity e)
    {
        var payload = e.GetHUDPayload();
        var tr = (e as Component)?.transform;
        return new HUDItem
        {
            Id = e.Guid.Value,
            TypeId = e.TypeId,
            Payload = payload,
            Source = e,
            Transform = tr
        };
    }
}

/// <summary>
/// Laufzeit-Modell, das der HUDBindingService für Panels bereitstellt.
/// Enthält Referenzen auf Quelle/Transform und den letzten HUDPayload.
/// </summary>
public class HUDItem
{
    public Guid Id;
    public string TypeId;
    public HUDPayload Payload;
    public IRegistrableEntity Source;
    public Transform Transform;

    public string Name => Payload.Name;
    public Vector3 Position => Payload.Position;

    public override string ToString() => $"{Name} ({TypeId}) [{Id}]";
}
