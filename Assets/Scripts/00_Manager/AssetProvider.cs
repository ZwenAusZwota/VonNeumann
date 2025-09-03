// Assets/Scripts/00_Manager/AssetProvider.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.ResourceManagement.ResourceLocations;


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

    // Für Assets, die du bewusst im Speicher halten willst (Release bei Bedarf).
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
        // 1) Initialize (nur einmal)
        if (!_initialized)
        {
            Log("[AssetProvider] Addressables.InitializeAsync...");
            AsyncOperationHandle initHandle = default;
            try
            {
                initHandle = Addressables.InitializeAsync();
                await initHandle.ToUniTask();
                if (!initHandle.IsValid())
                    throw new Exception("InitializeAsync lieferte ungültigen Handle.");

                _initialized = true;
                progress?.Report(0f);
                Log("[AssetProvider] Addressables initialized.");
            }
            finally
            {
                SafeRelease(initHandle);
            }
        }

        // 2) Falls kein Katalog oder keine Keys → fertig.
        var keys = (catalog != null && catalog.Keys != null) ? catalog.Keys : new List<string>();
        if (keys.Count == 0)
        {
            progress?.Report(1f);
            return;
        }

        // 3) Dependencies pro Key/Label herunterladen und Fortschritt aggregieren
        float each = 1f / keys.Count;
        float acc = 0f;

        foreach (var key in keys)
        {
            // 3a) Prüfen, ob es zu diesem Key/Label überhaupt Locations gibt
            IList<IResourceLocation> locs = null;
            AsyncOperationHandle<IList<IResourceLocation>> locHandle = default;
            try
            {
                locHandle = Addressables.LoadResourceLocationsAsync((object)key);
                locs = await locHandle.ToUniTask();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AssetProvider] LoadResourceLocationsAsync('{key}') Exception: {e.Message}");
            }
            finally
            {
                SafeRelease(locHandle);
            }

            if (locs == null || locs.Count == 0)
            {
                Debug.LogWarning($"[AssetProvider] Preload-Key/Label '{key}' hat keine Locations. (Falscher Name? Label nicht gesetzt?) – überspringe.");
                acc += each; progress?.Report(Mathf.Clamp01(acc));
                continue;
            }

            // 3b) Dependencies laden (WICHTIG: autoReleaseHandle = false!)
            AsyncOperationHandle depsHandle = default;
            try
            {
                depsHandle = Addressables.DownloadDependenciesAsync((object)key, false);
                while (!depsHandle.IsDone)
                {
                    progress?.Report(Mathf.Clamp01(acc + depsHandle.PercentComplete * each));
                    await UniTask.Yield();
                }

                if (depsHandle.Status != AsyncOperationStatus.Succeeded)
                    throw new Exception($"DownloadDependenciesAsync fehlgeschlagen: '{key}' (Status: {depsHandle.Status})");
            }
            finally
            {
                // Exakt EIN Release auf den Handle.
                SafeRelease(depsHandle);
            }

            acc += each;
            progress?.Report(Mathf.Clamp01(acc));
        }

        progress?.Report(1f);
        Log("[AssetProvider] Preload done.");
    }

    /// <summary>
    /// Ermittelt die Downloadgröße (in Byte) für eine Menge von Keys/Labels.
    /// </summary>
    public async UniTask<long> GetDownloadSizeAsync(IEnumerable<string> keys)
    {
        EnsureInitialized();
        var list = new List<string>(keys);
        AsyncOperationHandle<long> handle = default;
        try
        {
            handle = Addressables.GetDownloadSizeAsync(list);
            var size = await handle.ToUniTask();
            return size;
        }
        finally
        {
            SafeRelease(handle);
        }
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
            // Optional: Vorab prüfen, ob es Locations gibt
            IList<IResourceLocation> locs = null;
            AsyncOperationHandle<IList<IResourceLocation>> locHandle = default;
            try
            {
                locHandle = Addressables.LoadResourceLocationsAsync((object)key);
                locs = await locHandle.ToUniTask();
            }
            catch { /* ignore */ }
            finally
            {
                SafeRelease(locHandle);
            }

            if (locs == null || locs.Count == 0)
            {
                Debug.LogWarning($"[AssetProvider] DownloadDependencies: '{key}' hat keine Locations – übersprungen.");
                acc += each; progress?.Report(Mathf.Clamp01(acc));
                continue;
            }

            AsyncOperationHandle depsHandle = default;
            try
            {
                // WICHTIG: autoReleaseHandle = false, wir releasen genau 1x selbst
                depsHandle = Addressables.DownloadDependenciesAsync((object)key, false);
                while (!depsHandle.IsDone)
                {
                    progress?.Report(Mathf.Clamp01(acc + depsHandle.PercentComplete * each));
                    await UniTask.Yield();
                }
                if (depsHandle.Status != AsyncOperationStatus.Succeeded)
                    throw new Exception($"DownloadDependenciesAsync fehlgeschlagen: '{key}' (Status: {depsHandle.Status})");
            }
            finally
            {
                SafeRelease(depsHandle);
            }

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
    /// Gibt das Asset zurück. Du kannst später <see cref="ReleaseAsset{T}(object, T)"/> aufrufen.
    /// </summary>
    public async UniTask<T> LoadAssetAsync<T>(object key)
    {
        EnsureInitialized();
        AsyncOperationHandle<T> handle = default;
        try
        {
            handle = Addressables.LoadAssetAsync<T>(key);
            var asset = await handle.ToUniTask();
            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new Exception($"LoadAssetAsync fehlgeschlagen: '{key}' (Status: {handle.Status})");

            // Handle bewusst halten – bis ReleaseAsset() aufgerufen wird
            _retainedAssets[key] = handle;
            Log($"[AssetProvider] Loaded asset '{key}' ({typeof(T).Name})");
            return asset;
        }
        catch
        {
            // Bei Fehlern Handle freigeben (falls valide)
            SafeRelease(handle);
            throw;
        }
    }

    /// <summary>
    /// Gibt ein zuvor mit LoadAssetAsync geladenes Asset wieder frei.
    /// </summary>
    public void ReleaseAsset<T>(object key, T asset = default)
    {
        if (_retainedAssets.TryGetValue(key, out var handle))
        {
            SafeRelease(handle);
            _retainedAssets.Remove(key);
            Log($"[AssetProvider] Released asset handle for '{key}'.");
        }
        else
        {
            // Notfalls direkt das Asset freigeben (sofern Addressables es kennt)
            if (asset != null)
            {
                try
                {
                    Addressables.Release(asset);
                    Log($"[AssetProvider] Released asset object for '{key}'.");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AssetProvider] Release(asset) Exception for '{key}': {e.Message}");
                }
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
        try
        {
            Addressables.ReleaseInstance(instance);
            Log($"[AssetProvider] ReleaseInstance → {(instance ? instance.name : "<null>")}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AssetProvider] ReleaseInstance Exception: {e.Message}");
        }
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
        AsyncOperationHandle<IList<T>> handle = default;
        try
        {
            handle = Addressables.LoadAssetsAsync<T>(label, a => onLoadedEach?.Invoke(a), true);
            var list = await handle.ToUniTask();
            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new Exception($"LoadAssetsByLabelAsync fehlgeschlagen: '{label}' (Status: {handle.Status})");

            // Handle halten, bis ReleaseLabel() aufgerufen wird
            _retainedAssets[label] = handle;
            Log($"[AssetProvider] LoadAssetsByLabel '{label}' → {list?.Count ?? 0} assets");
            return list;
        }
        catch
        {
            SafeRelease(handle);
            throw;
        }
    }

    /// <summary>
    /// Gibt die mit <see cref="LoadAssetsByLabelAsync{T}"/> geladenen Assets wieder frei.
    /// </summary>
    public void ReleaseLabel(string label)
    {
        if (_retainedAssets.TryGetValue(label, out var handle))
        {
            SafeRelease(handle);
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
            SafeRelease(kv.Value);
        _retainedAssets.Clear();
        Log("[AssetProvider] Released all retained asset handles.");
    }

    // =====================================================================================
    // Internals
    // =====================================================================================

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("AssetProvider not initialized. Call Initialize(...) first.");
    }

    private void Log(string msg)
    {
        if (verboseLogs) Debug.Log(msg);
    }

    private static void SafeRelease(AsyncOperationHandle handle)
    {
        if (handle.IsValid())
        {
            try { Addressables.Release(handle); }
            catch (Exception e) { Debug.LogWarning($"[AssetProvider] SafeRelease (non-generic) warn: {e.Message}"); }
        }
    }

    private static void SafeRelease<T>(AsyncOperationHandle<T> handle)
    {
        if (handle.IsValid())
        {
            try { Addressables.Release(handle); }
            catch (Exception e) { Debug.LogWarning($"[AssetProvider] SafeRelease<T> warn: {e.Message}"); }
        }
    }

#if UNITY_EDITOR
    // Kleine Editor-Shortcuts zum Testen
    [ContextMenu("Editor: Release All Retained")]
    private void EditorReleaseAllRetained() => ReleaseAllRetained();
#endif
}
