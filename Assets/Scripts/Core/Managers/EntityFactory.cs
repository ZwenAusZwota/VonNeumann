// Assets/Scripts/00_Manager/EntityFactory.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;

/// <summary>
/// Zentrale Factory zum Instanziieren und Recyclen (Pooling) von Entities.
/// - Standardweg ist Addressables anhand eines typeId-Keys.
/// - Erstellt pro typeId einen einfachen Pool (Stack).
/// - Sorgt f�r frische GUIDs bei neuen Instanzen, damit WorldRegistry keine Duplikate sieht.
/// - Registriert Entities sauber (RegisterNow/UnregisterNow), unabh�ngig davon, ob sie aus Pool oder frisch sind.
/// </summary>
public class EntityFactory : MonoBehaviour
{
    [System.Obsolete("Use ServiceContainer.Instance.Get<EntityFactory>() instead")]
    public static EntityFactory I { get; private set; }

    [Header("Pooling")]
    [Tooltip("Pooling standardm��ig beim Spawn benutzen.")]
    [SerializeField] private bool usePoolingByDefault = true;

    [Tooltip("Optional: Anzahl an Prewarm-Instanzen (pro typeId), falls PrewarmAll benutzt wird.")]
    [SerializeField] private int defaultPrewarmCount = 0;

    [Tooltip("Types, die beim Start automatisch vorgew�rmt werden (falls defaultPrewarmCount > 0).")]
    [SerializeField] private List<string> prewarmTypeIds = new();

    [Header("Organisation")]
    [Tooltip("Eltern-Transform f�r gepoolte (inaktive) Instanzen.")]
    [SerializeField] private Transform poolRoot;

    // --- Pools: typeId -> Stack<GameObject> ---
    private readonly Dictionary<string, Stack<GameObject>> _pools = new();

    // --- Buchhaltung: um Double-Despawn o. �. zu erkennen (optional) ---
    private readonly HashSet<GameObject> _inPool = new();

    private void Awake()
    {
        // Service Container Registrierung
        if (ServiceContainer.Instance != null)
        {
            ServiceContainer.Instance.RegisterSingleton<EntityFactory>(this);
        }

        // Singleton-Absicherung (fr Rckwrtskompatibilitt)
        var existingInstance = GetExistingInstance();
        if (existingInstance != null && existingInstance != this) 
        { 
            Destroy(gameObject); 
            return; 
        }
        SetInstance(this);
        DontDestroyOnLoad(gameObject);

        if (poolRoot == null)
        {
            var go = new GameObject("[POOL]");
            go.transform.SetParent(transform, false);
            poolRoot = go.transform;
        }
    }

    private async void Start()
    {
        if (defaultPrewarmCount > 0 && prewarmTypeIds != null && prewarmTypeIds.Count > 0)
        {
            foreach (var tid in prewarmTypeIds)
            {
                try { await Prewarm(tid, defaultPrewarmCount); }
                catch (Exception ex) { Debug.LogError($"[EntityFactory] Prewarm failed for '{tid}': {ex.Message}"); }
            }
        }
    }

    // =====================================================================================
    // Public API
    // =====================================================================================

    /// <summary>
    /// Spawnt eine Entit�t anhand des Addressables-typeId.
    /// - Nutzt Pooling (Default) oder frische Instanzierung.
    /// - Position/Rotation/Parent werden gesetzt.
    /// - Stellt sicher, dass die Instanz einen frischen GUID-Satz hat und korrekt in der WorldRegistry registriert wird.
    /// </summary>
    public async UniTask<IRegistrableEntity> Spawn(
        string typeId,
        Vector3 position,
        Quaternion rotation,
        Transform parent = null,
        bool? usePooling = null)
    {
        bool pooling = usePooling ?? usePoolingByDefault;

        GameObject go = null;
        RegistrableEntity reg = null;

        if (pooling && TryPop(typeId, out go))
        {
            // Aus Pool nehmen
            reg = go.GetComponent<RegistrableEntity>();
            if (reg == null)
                Debug.LogWarning($"[EntityFactory] Pooled object has no RegistrableEntity: {typeId}");

            // Transform anwenden
            if (parent) go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);

            // Aktivieren -> OnEnable registriert NICHT automatisch erneut (siehe RegistrableEntity),
            // daher manuell registrieren:
            if (!go.activeSelf) go.SetActive(true);
            reg?.RegisterNow();

            return reg;
        }

        // Frisch instanziieren
        go = await Addressables.InstantiateAsync(typeId, position, rotation, parent).ToUniTask();
        reg = go.GetComponent<RegistrableEntity>();
        if (reg == null)
            Debug.LogWarning($"[EntityFactory] Instantiated object has no RegistrableEntity: {typeId}");

        // 1) Direkt nach Instanziierung ist das GO aktiv und wurde evtl. schon registriert (OnEnable).
        //    Wir wollen einen FRISCHEN GUID-Satz. Daher:
        reg?.UnregisterNow();           // altes (ggf. doppeltes) Mapping aus Registry entfernen
        AssignFreshGuids(go);           // neue GUIDs f�r alle GuidProvider im Hierarchie-Ast
        if (reg != null) reg.SetTypeId(typeId); // sicherstellen, dass TypeId korrekt gesetzt ist

        // (GO ist aktiv) -> erneut registrieren
        reg?.RegisterNow();

        return reg;
    }

    /// <summary>
    /// Gibt eine Instanz in den Pool zur�ck (oder zerst�rt sie, falls Pooling nicht aktiv sein soll).
    /// </summary>
    public void Despawn(IRegistrableEntity entity, bool? usePooling = null)
    {
        if (entity is not Component c || c == null) return;
        Despawn(c.gameObject, entity.TypeId, usePooling);
    }

    /// <summary>
    /// Gibt eine Instanz in den Pool zur�ck (oder zerst�rt sie), anhand des GameObjects.
    /// </summary>
    public void Despawn(GameObject go, string typeId, bool? usePooling = null)
    {
        if (!go) return;
        bool pooling = usePooling ?? usePoolingByDefault;

        var reg = go.GetComponent<RegistrableEntity>();
        reg?.UnregisterNow();

        if (pooling)
        {
            // Inaktiv schalten, an PoolRoot h�ngen und zur�cklegen
            go.SetActive(false);
            go.transform.SetParent(poolRoot, false);
            Push(typeId, go);
        }
        else
        {
            // Kein Pooling -> Addressables-Instanz freigeben
            Addressables.ReleaseInstance(go);
        }
    }

    /// <summary>
    /// W�rmt den Pool f�r ein typeId vor (legt count Instanzen inaktiv ab).
    /// </summary>
    public async UniTask Prewarm(string typeId, int count)
    {
        if (count <= 0) return;

        var stack = GetOrCreatePool(typeId);

        for (int i = 0; i < count; i++)
        {
            var go = await Addressables.InstantiateAsync(typeId).ToUniTask();
            var reg = go.GetComponent<RegistrableEntity>();

            // Registrierungen r�ckg�ngig machen, frische GUIDs zuweisen:
            reg?.UnregisterNow();
            AssignFreshGuids(go);
            if (reg != null) reg.SetTypeId(typeId);

            // Deaktivieren und in Pool h�ngen
            go.SetActive(false);
            go.transform.SetParent(poolRoot, false);

            stack.Push(go);
            _inPool.Add(go);
        }
    }

    /// <summary>
    /// Leert den Pool f�r ein bestimmtes typeId und gibt alle Addressables-Instanzen frei.
    /// </summary>
    public void ClearPool(string typeId)
    {
        if (!_pools.TryGetValue(typeId, out var stack)) return;

        while (stack.Count > 0)
        {
            var go = stack.Pop();
            if (!go) continue;
            _inPool.Remove(go);
            Addressables.ReleaseInstance(go);
        }
    }

    /// <summary>Leert alle Pools (Achtung: Gibt alle gepoolten Instanzen frei).</summary>
    public void ClearAllPools()
    {
        foreach (var kv in _pools)
        {
            var stack = kv.Value;
            while (stack.Count > 0)
            {
                var go = stack.Pop();
                if (!go) continue;
                _inPool.Remove(go);
                Addressables.ReleaseInstance(go);
            }
        }
        _pools.Clear();
    }

    /// <summary>Aktuelle Poolgr��e f�r typeId.</summary>
    public int GetPoolCount(string typeId)
        => _pools.TryGetValue(typeId, out var stack) ? stack.Count : 0;

    // =====================================================================================
    // Internals
    // =====================================================================================

    private Stack<GameObject> GetOrCreatePool(string typeId)
    {
        if (!_pools.TryGetValue(typeId, out var stack))
        {
            stack = new Stack<GameObject>(Mathf.Max(0, defaultPrewarmCount));
            _pools[typeId] = stack;
        }
        return stack;
    }

    private bool TryPop(string typeId, out GameObject go)
    {
        go = null;
        if (!_pools.TryGetValue(typeId, out var stack)) return false;
        if (stack.Count == 0) return false;

        go = stack.Pop();
        _inPool.Remove(go);
        return go;
    }

    private void Push(string typeId, GameObject go)
    {
        var stack = GetOrCreatePool(typeId);
        stack.Push(go);
        _inPool.Add(go);
    }

    /// <summary>
    /// Weist allen GuidProvidern in der Hierarchie neue GUIDs zu.
    /// Wichtig, damit Instanzen (auch aus Pool) in der WorldRegistry eindeutig sind.
    /// </summary>
    private void AssignFreshGuids(GameObject root)
    {
        if (!root) return;
        var providers = root.GetComponentsInChildren<GuidProvider>(true);
        foreach (var gp in providers)
        {
            // ForceNewGuid() ist in unserer GuidProvider-Implementierung zur Laufzeit nutzbar.
            gp.ForceNewGuid();
        }
    }

    // Hilfsmethoden zur Vermeidung der Warnung
    private static EntityFactory GetExistingInstance()
    {
        return ServiceContainer.Instance?.Get<EntityFactory>();
    }

    private static void SetInstance(EntityFactory instance)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        I = instance;
#pragma warning restore CS0618
    }
}
