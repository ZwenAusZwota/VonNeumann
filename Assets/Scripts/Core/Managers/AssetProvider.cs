// Assets/Scripts/00_Manager/AssetProvider.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using TMPro;

public class AssetProvider : MonoBehaviour
{
    public static AssetProvider I { get; private set; }
    public bool IsInitialized => _initialized;

    [Tooltip("Dateiname des Logs im Application.persistentDataPath.")]
    [SerializeField] private string logFileName = "AssetProvider.log";

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;

    [Header("Optional: Statusausgabe")]
    [Tooltip("Wird zur Laufzeit vom LoadingScreenController gesetzt.")]
    [SerializeField] private TextMeshProUGUI statusLabel;

    private static readonly object _logLock = new object();
    private string _logFilePath;
    private bool _initialized;
    private readonly Dictionary<object, AsyncOperationHandle> _retainedAssets = new();

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>UI-Ziel zum Anzeigen der Statusmeldungen setzen oder entfernen (null).</summary>
    public void SetStatusTarget(TextMeshProUGUI label)
    {
        statusLabel = label;
        SafeStatus("[AssetProvider] Statusziel gesetzt.");
    }

    // =====================================================================
    // Initialisierung & Preload
    // =====================================================================

    public async UniTask Initialize(PreloadCatalog catalog, IProgress<float> progress = null, CancellationToken ct = default)
    {
        if (!_initialized)
        {
            SafeStatus("Initialisiere Addressables …");
            AsyncOperationHandle initHandle = default;
            try
            {
                initHandle = Addressables.InitializeAsync();
                await initHandle.ToUniTask(cancellationToken: ct);
                if (!initHandle.IsValid())
                    throw new Exception("InitializeAsync lieferte ungültigen Handle.");
                _initialized = true;
                progress?.Report(0f);
                SafeStatus("Addressables initialisiert.");
            }
            finally { SafeRelease(initHandle); }
        }

        var keys = (catalog != null && catalog.Keys != null) ? catalog.Keys : new List<string>();
        if (keys.Count == 0)
        {
            SafeStatus("Kein Preload-Katalog / keine Keys. Überspringe Preload.");
            progress?.Report(1f);
            return;
        }

        float each = 1f / keys.Count;
        float acc = 0f;

        foreach (var key in keys)
        {
            ct.ThrowIfCancellationRequested();

            // Locations prüfen
            AsyncOperationHandle<IList<IResourceLocation>> locHandle = default;
            IList<IResourceLocation> locs = null;
            try
            {
                SafeStatus($"Prüfe Ressourcen-Orte für '{key}' …");
                locHandle = Addressables.LoadResourceLocationsAsync((object)key);
                locs = await locHandle.ToUniTask(cancellationToken: ct);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AssetProvider] Locations('{key}') warn: {e.Message}");
                SafeStatus($"Warnung: Konnte Locations für '{key}' nicht ermitteln.");
            }
            finally { SafeRelease(locHandle); }

            if (locs == null || locs.Count == 0)
            {
                SafeStatus($"Keine Locations für '{key}'. Überspringe.");
                acc += each; progress?.Report(Mathf.Clamp01(acc));
                continue;
            }

            // Dependencies laden (mit Fortschritt)
            AsyncOperationHandle depsHandle = default;
            try
            {
                SafeStatus($"Lade Abhängigkeiten für '{key}' …");
                depsHandle = Addressables.DownloadDependenciesAsync((object)key, false);

                int lastPct = -1;
                while (!depsHandle.IsDone)
                {
                    ct.ThrowIfCancellationRequested();
                    var pct = Mathf.Clamp01(depsHandle.PercentComplete);
                    int ipct = Mathf.RoundToInt(pct * 100f);
                    if (ipct != lastPct)
                    {
                        lastPct = ipct;
                        SafeStatus($"Lade Abhängigkeiten für '{key}' ({ipct}%) …");
                    }
                    progress?.Report(Mathf.Clamp01(acc + pct * each));
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                if (depsHandle.Status != AsyncOperationStatus.Succeeded)
                    throw new Exception($"DownloadDependenciesAsync fehlgeschlagen: '{key}' (Status: {depsHandle.Status})");

                SafeStatus($"Abhängigkeiten für '{key}' geladen.");
            }
            finally { SafeRelease(depsHandle); }

            acc += each;
            progress?.Report(Mathf.Clamp01(acc));
        }

        progress?.Report(1f);
        SafeStatus("Preload abgeschlossen.");
    }

    public async UniTask<long> GetDownloadSizeAsync(IEnumerable<string> keys, CancellationToken ct = default)
    {
        EnsureInitialized();
        SafeStatus("Ermittle Downloadgröße …");
        var list = new List<string>(keys);
        AsyncOperationHandle<long> handle = default;
        try
        {
            handle = Addressables.GetDownloadSizeAsync(list);
            var size = await handle.ToUniTask(cancellationToken: ct);
            SafeStatus($"Downloadgröße ermittelt: {size} Byte.");
            return size;
        }
        finally { SafeRelease(handle); }
    }

    public async UniTask DownloadDependenciesAsync(IEnumerable<string> keys, IProgress<float> progress = null, CancellationToken ct = default)
    {
        EnsureInitialized();
        var list = new List<string>(keys);
        if (list.Count == 0) { progress?.Report(1f); return; }

        float each = 1f / list.Count;
        float acc = 0f;

        foreach (var key in list)
        {
            ct.ThrowIfCancellationRequested();

            AsyncOperationHandle<IList<IResourceLocation>> locHandle = default;
            IList<IResourceLocation> locs = null;
            try
            {
                SafeStatus($"Prüfe Ressourcen-Orte für '{key}' …");
                locHandle = Addressables.LoadResourceLocationsAsync((object)key);
                locs = await locHandle.ToUniTask(cancellationToken: ct);
            }
            finally { SafeRelease(locHandle); }

            if (locs == null || locs.Count == 0)
            {
                SafeStatus($"Keine Locations für '{key}'. Überspringe Download.");
                acc += each; progress?.Report(Mathf.Clamp01(acc));
                continue;
            }

            AsyncOperationHandle depsHandle = default;
            try
            {
                SafeStatus($"Lade Abhängigkeiten für '{key}' …");
                depsHandle = Addressables.DownloadDependenciesAsync((object)key, false);

                int lastPct = -1;
                while (!depsHandle.IsDone)
                {
                    ct.ThrowIfCancellationRequested();
                    var pct = Mathf.Clamp01(depsHandle.PercentComplete);
                    int ipct = Mathf.RoundToInt(pct * 100f);
                    if (ipct != lastPct)
                    {
                        lastPct = ipct;
                        SafeStatus($"Lade Abhängigkeiten für '{key}' ({ipct}%) …");
                    }
                    progress?.Report(Mathf.Clamp01(acc + pct * each));
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                if (depsHandle.Status != AsyncOperationStatus.Succeeded)
                    throw new Exception($"DownloadDependenciesAsync fehlgeschlagen: '{key}' (Status: {depsHandle.Status})");

                SafeStatus($"Abhängigkeiten für '{key}' geladen.");
            }
            finally { SafeRelease(depsHandle); }

            acc += each;
            progress?.Report(Mathf.Clamp01(acc));
        }

        progress?.Report(1f);
        SafeStatus("DownloadDependencies abgeschlossen.");
    }

    // =====================================================================
    // Instanziieren (mit Timeout + PostInit-Frames, framebasiert)
    // =====================================================================

    /// <summary>
    /// Instanziiert ein Addressable-Prefab. Bricht nach timeoutSek ab (Default 30s).
    /// Wartet danach noch 2 Frames (framebasiert, unabhängig von Time.timeScale),
    /// damit Awake/Start/OnEnable sauber durchlaufen, und meldet „Post-Init ok“.
    /// </summary>
    public async UniTask<GameObject> InstantiateAsync(
        object key,
        Vector3 position,
        Quaternion rotation,
        Transform parent = null,
        float timeoutSek = 30f,
        CancellationToken externalCt = default)
    {
        EnsureInitialized();
        SafeStatus($"Instanziiere '{key}' …");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        if (timeoutSek > 0f) cts.CancelAfter(TimeSpan.FromSeconds(timeoutSek));

        GameObject go = null;
        AsyncOperationHandle<GameObject> handle = default;
        try
        {
            handle = Addressables.InstantiateAsync(key, position, rotation, parent);
            go = await handle.ToUniTask(cancellationToken: cts.Token);
            if (handle.Status != AsyncOperationStatus.Succeeded || !go)
                throw new Exception($"InstantiateAsync fehlgeschlagen: '{key}' (Status: {handle.Status})");

            Log($"[AssetProvider] Instantiate '{key}' → {go.name}");
            SafeStatus($"Instanz '{go.name}' erstellt.");

            // Framebasiert (unabhängig von Time.timeScale):
            await UniTask.DelayFrame(2, PlayerLoopTiming.Update, cts.Token);
            SafeStatus($"Instanz '{go.name}' Post-Init ok.");
            return go;
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"InstantiateAsync timeout nach {timeoutSek:0}s für '{key}'.");
        }
        finally
        {
            // Nur im Fehlerfall freigeben – bei Erfolg erfolgt das Release via ReleaseInstance(...)
            if (go == null) SafeRelease(handle);
        }
    }

    public UniTask<GameObject> InstantiateAsync(
        object key,
        Transform parent = null,
        bool inWorldSpace = false,
        float timeoutSek = 30f,
        CancellationToken externalCt = default)
        => InstantiateAsync(key, Vector3.zero, Quaternion.identity, parent, timeoutSek, externalCt);

    public void ReleaseInstance(GameObject instance)
    {
        if (!instance) return;
        try
        {
            Addressables.ReleaseInstance(instance);
            Log($"[AssetProvider] ReleaseInstance → {(instance ? instance.name : "<null>")}");
            SafeStatus($"Instanz '{(instance ? instance.name : "<null>")}' freigegeben.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AssetProvider] ReleaseInstance Exception: {e.Message}");
        }
    }

    // =====================================================================
    // Asset-Load Helpers
    // =====================================================================

    public async UniTask<T> LoadAssetAsync<T>(object key, CancellationToken ct = default)
    {
        EnsureInitialized();
        SafeStatus($"Lade Asset '{key}' ({typeof(T).Name}) …");
        AsyncOperationHandle<T> handle = default;
        try
        {
            handle = Addressables.LoadAssetAsync<T>(key);
            var asset = await handle.ToUniTask(cancellationToken: ct);
            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new Exception($"LoadAssetAsync fehlgeschlagen: '{key}' (Status: {handle.Status})");

            _retainedAssets[key] = handle;
            Log($"[AssetProvider] Loaded asset '{key}' ({typeof(T).Name})");
            SafeStatus($"Asset '{key}' geladen.");
            return asset;
        }
        catch
        {
            SafeRelease(handle);
            throw;
        }
    }

    public void ReleaseAsset<T>(object key, T asset = default)
    {
        if (_retainedAssets.TryGetValue(key, out var handle))
        {
            SafeRelease(handle);
            _retainedAssets.Remove(key);
            Log($"[AssetProvider] Released asset handle for '{key}'.");
            SafeStatus($"Asset-Handle für '{key}' freigegeben.");
        }
        else if (asset != null)
        {
            try
            {
                Addressables.Release(asset);
                Log($"[AssetProvider] Released asset object for '{key}'.");
                SafeStatus($"Asset-Objekt für '{key}' freigegeben.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AssetProvider] Release(asset) warn: {e.Message}");
            }
        }
    }

    // =====================================================================
    // Internals
    // =====================================================================

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("AssetProvider not initialized. Call Initialize(...) first.");
    }

    private void Log(string msg)
    {
        //if (verboseLogs)
        //{
            Debug.Log(msg);
            try
            {
                var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var line = $"{ts} {msg}";
                lock (_logLock)
                {
                    EnsureLogFileReady(writeHeader: false);
                    File.AppendAllText(_logFilePath, line + Environment.NewLine);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AssetProvider] Logfile-Write warn: {e.Message}");
            }
        //}
    }

    private void SafeStatus(string msg)
    {
        try { if (statusLabel) statusLabel.text = msg; } catch { /* UI evtl. entladen */ }
        if (verboseLogs) Debug.Log(msg);
    }

    private static void SafeRelease(AsyncOperationHandle handle)
    {
        if (handle.IsValid())
        {
            try { Addressables.Release(handle); }
            catch (Exception e) { Debug.LogWarning($"[AssetProvider] SafeRelease warn: {e.Message}"); }
        }
    }

    private void EnsureLogFileReady(bool writeHeader)
    {

        try
        {
            var dir = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (writeHeader || !File.Exists(_logFilePath))
            {
                var header = $"=== AssetProvider Log start {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ==={Environment.NewLine}";
                File.AppendAllText(_logFilePath, header);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[AssetProvider] EnsureLogFileReady warn: {e.Message}");
        }
    }
#if UNITY_EDITOR
    [ContextMenu("Editor: Release All Retained")]
    private void EditorReleaseAllRetained()
    {
        foreach (var kv in _retainedAssets) SafeRelease(kv.Value);
        _retainedAssets.Clear();
        Debug.Log("[AssetProvider] Released all retained asset handles.");
    }
#endif
}
