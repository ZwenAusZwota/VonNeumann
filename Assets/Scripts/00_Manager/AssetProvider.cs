// Assets/Scripts/00_Manager/AssetProvider.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

/// <summary>
/// Zentraler Provider für Addressables.
/// - Einmalige Initialisierung
/// - Optionales Vorladen von Abhängigkeiten (PreloadCatalog)
/// - Bequeme Helfer zum Laden, Instanziieren und Freigeben
/// </summary>
public class AssetProvider : MonoBehaviour
{
    public static AssetProvider I { get; private set; }

    /// <summary>Wird true, sobald Addressables erfolgreich initialisiert ist.</summary>
    public bool IsInitialized => _initialized;

    [Header("Debug")]
    [Tooltip("Zusätzliche Logs ausgeben.")]
    [SerializeField] private bool verboseLogs = false;

    private bool _initialized;

    // Optional: Für Assets, die du bewusst im Speicher halten willst (Release bei Bedarf).
    private readonly Dictionary<object, AsyncOperationHandle> _retainedAssets = new();

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    // =====================================================================================
    // Initialisierung & Preload
    // =====================================================================================

    /// <summary>
    /// Initialisiert Addressables und lädt optional Abhängigkeiten für alle Keys/Labels
    /// aus dem Katalog in den lokalen Cache (DownloadDependencies).
    /// Fortschritt (0..1) wird – falls gesetzt – laufend gemeldet.
    /// </summary>
    public async UniTask Initialize(PreloadCatalog catalog, IProgress<float> progress = null)
    {
        if (!_initialized)
        {
            Log("[AssetProvider] Addressables.InitializeAsync...");
            var initHandle = Addressables.InitializeAsync();
            await initHandle.ToUniTask();
            _initialized = true;
            progress?.Report(0f);
            Log("[AssetProvider] Addressables initialized.");
        }

        // Falls kein Katalog: fertig.
        if (catalog == null || catalog.Keys == null || catalog.Keys.Count == 0)
        {
            progress?.Report(1f);
            return;
        }

        // Dependencies pro Key/Label herunterladen und Fortschritt aggregieren
        float each = 1f / catalog.Keys.Count;
        float acc = 0f;

        //foreach (var key in catalog.Keys)
        //{
        //    Log($"[AssetProvider] DownloadDependenciesAsync: '{key}'");
        //    var handle = Addressables.DownloadDependenciesAsync((object)key, true);

        //    while (!handle.IsDone)
        //    {
        //        progress?.Report(Mathf.Clamp01(acc + handle.PercentComplete * each));
        //        await UniTask.Yield(); // Frameweise updaten
        //    }

        //    // Handle freigeben – Daten verbleiben im Cache
        //    Addressables.Release(handle);

        //    acc += each;
        //    progress?.Report(Mathf.Clamp01(acc));
        //}

        // ... nach InitializeAsync()
        var keys = catalog != null ? catalog.Keys : new List<string>();
        if (keys.Count == 0) { _initialized = true; progress?.Report(1f); return; }

        foreach (var key in keys)
        {
            // 0) Gibt es für den Key/Label überhaupt Locations?
            IList<UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation> locs = null;
            try
            {
                var locHandle = Addressables.LoadResourceLocationsAsync((object)key);
                locs = await locHandle.ToUniTask();
                Addressables.Release(locHandle);
            }
            catch { /* ignorieren – locs bleibt null */ }

            if (locs == null || locs.Count == 0)
            {
                Debug.LogWarning($"[AssetProvider] Preload-Key/Label '{key}' hat keine Locations. (Falscher Name? Label nicht gesetzt?) – überspringe.");
                acc += each; progress?.Report(Mathf.Clamp01(acc));
                continue;
            }

            // 1) Dependencies laden
            var handle = Addressables.DownloadDependenciesAsync((object)key, true);
            while (!handle.IsDone)
            {
                progress?.Report(Mathf.Clamp01(acc + handle.PercentComplete * each));
                await Cysharp.Threading.Tasks.UniTask.Yield();
            }
            Addressables.Release(handle);

            acc += each;
            progress?.Report(Mathf.Clamp01(acc));
        }

        _initialized = true;

        progress?.Report(1f);
        Log("[AssetProvider] Preload done.");
    }

    /// <summary>
    /// Ermittelt die Downloadgröße (in Byte) für eine Menge von Keys/Labels.
    /// </summary>
    public async UniTask<long> GetDownloadSizeAsync(IEnumerable<string> keys)
    {
        EnsureInitialized();
        var handle = Addressables.GetDownloadSizeAsync(new List<string>(keys));
        var size = await handle.ToUniTask();
        Addressables.Release(handle);
        return size;
    }

    /// <summary>
    /// Lädt Abhängigkeiten für mehrere Keys/Labels in den Cache. Fortschritt optional.
    /// </summary>
    public async UniTask DownloadDependenciesAsync(IEnumerable<string> keys, IProgress<float> progress = null)
    {
        EnsureInitialized();
        var list = new List<string>(keys);
        if (list.Count == 0) { progress?.Report(1f); return; }

        float each = 1f / list.Count;
        float acc = 0f;

        foreach (var key in list)
        {
            var handle = Addressables.DownloadDependenciesAsync((object)key, true);
            while (!handle.IsDone)
            {
                progress?.Report(Mathf.Clamp01(acc + handle.PercentComplete * each));
                await UniTask.Yield();
            }
            Addressables.Release(handle);
            acc += each;
            progress?.Report(Mathf.Clamp01(acc));
        }

        progress?.Report(1f);
    }

    // =====================================================================================
    // Laden & Freigeben von Assets
    // =====================================================================================

    /// <summary>
    /// Lädt ein Asset vom Typ T anhand eines Addressables-Keys/Labels.
    /// Gibt das Asset zurück. Du kannst später <see cref="ReleaseAsset{T}(T)"/> aufrufen.
    /// </summary>
    public async UniTask<T> LoadAssetAsync<T>(object key)
    {
        EnsureInitialized();
        var handle = Addressables.LoadAssetAsync<T>(key);
        var asset = await handle.ToUniTask();
        _retainedAssets[key] = handle; // bewusst im Speicher halten (ReleaseAsset() löst es wieder)
        Log($"[AssetProvider] Loaded asset '{key}' ({typeof(T).Name})");
        return asset;
    }

    /// <summary>
    /// Gibt ein zuvor mit LoadAssetAsync geladenes Asset wieder frei.
    /// </summary>
    public void ReleaseAsset<T>(object key, T asset = default)
    {
        if (_retainedAssets.TryGetValue(key, out var handle))
        {
            Addressables.Release(handle);
            _retainedAssets.Remove(key);
            Log($"[AssetProvider] Released asset handle for '{key}'.");
        }
        else
        {
            // Notfalls direkt das Asset freigeben (sofern Addressables es kennt)
            if (asset != null)
            {
                Addressables.Release(asset);
                Log($"[AssetProvider] Released asset object for '{key}'.");
            }
        }
    }

    // =====================================================================================
    // Instanziieren & Freigeben von Instanzen
    // =====================================================================================

    /// <summary>
    /// Instanziiert ein Prefab per Addressables und gibt die Instanz zurück.
    /// Du kannst sie später mit <see cref="ReleaseInstance(GameObject)"/> freigeben.
    /// </summary>
    public async UniTask<GameObject> InstantiateAsync(object key, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        EnsureInitialized();
        var go = await Addressables.InstantiateAsync(key, position, rotation, parent).ToUniTask();
        Log($"[AssetProvider] Instantiate '{key}' → {go.name}");
        return go;
    }

    /// <summary>
    /// Instanziiert ein Prefab per Addressables und gibt die Instanz zurück.
    /// </summary>
    public async UniTask<GameObject> InstantiateAsync(object key, Transform parent = null, bool inWorldSpace = false)
    {
        EnsureInitialized();
        var go = await Addressables.InstantiateAsync(key, parent, inWorldSpace).ToUniTask();
        Log($"[AssetProvider] Instantiate '{key}' → {go.name}");
        return go;
    }

    /// <summary>
    /// Gibt eine per Addressables instanziierte Instanz frei.
    /// (Nicht mit GameObject.Destroy verwechseln.)
    /// </summary>
    public void ReleaseInstance(GameObject instance)
    {
        if (!instance) return;
        Addressables.ReleaseInstance(instance);
        Log($"[AssetProvider] ReleaseInstance → {(instance ? instance.name : "<null>")}");
    }

    // =====================================================================================
    // Utilities
    // =====================================================================================

    /// <summary>
    /// Lädt alle Assets zu einem Label in den Speicher (ohne zu instanzieren).
    /// Praktisch, wenn du bestimmte Assets schnell verfügbar halten willst.
    /// </summary>
    public async UniTask<IList<T>> LoadAssetsByLabelAsync<T>(string label, Action<T> onLoadedEach = null)
    {
        EnsureInitialized();
        var handle = Addressables.LoadAssetsAsync<T>(label, a => onLoadedEach?.Invoke(a), true);
        var list = await handle.ToUniTask();
        _retainedAssets[label] = handle; // Handle halten, bis ReleaseLabel() aufgerufen wird
        Log($"[AssetProvider] LoadAssetsByLabel '{label}' → {list?.Count ?? 0} assets");
        return list;
    }

    /// <summary>
    /// Gibt die mit <see cref="LoadAssetsByLabelAsync{T}"/> geladenen Assets wieder frei.
    /// </summary>
    public void ReleaseLabel(string label)
    {
        if (_retainedAssets.TryGetValue(label, out var handle))
        {
            Addressables.Release(handle);
            _retainedAssets.Remove(label);
            Log($"[AssetProvider] Released label handle '{label}'.");
        }
    }

    /// <summary>
    /// Gibt alle vom Provider gehaltenen Asset-Handles frei (vorsichtig einsetzen).
    /// </summary>
    public void ReleaseAllRetained()
    {
        foreach (var kv in _retainedAssets)
            Addressables.Release(kv.Value);
        _retainedAssets.Clear();
        Log("[AssetProvider] Released all retained asset handles.");
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("AssetProvider not initialized. Call Initialize(...) first.");
    }

    private void Log(string msg)
    {
        if (verboseLogs) Debug.Log(msg);
    }

#if UNITY_EDITOR
    // Kleine Editor-Shortcuts zum Testen
    [ContextMenu("Editor: Release All Retained")]
    private void EditorReleaseAllRetained() => ReleaseAllRetained();
#endif
}
