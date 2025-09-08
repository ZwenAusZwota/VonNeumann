// Assets/Scripts/00_Manager/SaveSystem.cs
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;

public class SaveSystem : MonoBehaviour
{
    public static SaveSystem I { get; private set; }

    [Header("Allgemein")]
    [Tooltip("Version des Save-Formats – bei Änderungen erhöhen und ggf. Migration implementieren.")]
    [SerializeField] private int saveVersion = 2;

    [Tooltip("Ordnername relativ zu Application.persistentDataPath.")]
    [SerializeField] private string folderName = "SaveSlots";

    [Tooltip("Optionaler Default-Slotname (z. B. Autosave).")]
    [SerializeField] private string defaultSlot = "slot_1";

#if UNITY_EDITOR
    [Header("Editor (optional)")]
    [Tooltip("Im Editor statt persistentDataPath nach Assets/Saves/ schreiben (leichter auffindbar).")]
    [SerializeField] private bool useProjectSavesInEditor = true;
    [SerializeField] private string projectSavesFolder = "Assets/Saves/SaveSlots";
#endif

    public event Action<string> OnBeforeSave;
    public event Action<string> OnAfterSave;
    public event Action<string> OnBeforeLoad;
    public event Action<string> OnAfterLoad;

    private string RootPath
    {
        get
        {
#if UNITY_EDITOR
            if (useProjectSavesInEditor)
                return Path.Combine(Application.dataPath.Replace("/Assets",""), projectSavesFolder);
#endif
            return Path.Combine(Application.persistentDataPath, folderName);
        }
    }

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        EnsureFolder();
    }

    private void EnsureFolder()
    {
        if (!Directory.Exists(RootPath))
            Directory.CreateDirectory(RootPath);
    }

    // --------------------------------------------------------------------------------------
    // Öffentliche API
    // --------------------------------------------------------------------------------------

    public UniTask SaveAsync() => SaveAsync(defaultSlot);
    public UniTask<bool> LoadAsync() => LoadAsync(defaultSlot);

    /// <summary>Speichert den aktuellen Spielstand in den angegebenen Slot.</summary>
    public async UniTask SaveAsync(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId)) slotId = defaultSlot;

        OnBeforeSave?.Invoke(slotId);

        // 1) Daten sammeln
        var save = new SaveGame
        {
            version = saveVersion,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            player = CapturePlayer(),
            entities = CaptureEntities(),
            hud = CaptureHUD()
        };

        // 2) Serialisieren
        string json = JsonUtility.ToJson(save, false);

        // 3) Atomisch schreiben
        EnsureFolder();
        string path = GetPath(slotId);
        string tmp = path + ".tmp";

        await UniTask.SwitchToThreadPool();
        try
        {
            File.WriteAllText(tmp, json);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
        finally
        {
            await UniTask.SwitchToMainThread();
        }

        OnAfterSave?.Invoke(slotId);
        Debug.Log($"[SaveSystem] Gespeichert: {slotId} @ {path}");
    }

    /// <summary>Lädt den Spielstand aus dem Slot. Erwartet, dass die Spielszene aktiv ist.</summary>
    public async UniTask<bool> LoadAsync(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId)) slotId = defaultSlot;
        string path = GetPath(slotId);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SaveSystem] Slot nicht gefunden: {slotId}");
            return false;
        }

        OnBeforeLoad?.Invoke(slotId);

        // 1) Datei lesen
        string json;
        await UniTask.SwitchToThreadPool();
        try { json = File.ReadAllText(path); }
        finally { await UniTask.SwitchToMainThread(); }

        // 2) Deserialisieren
        SaveGame save = null;
        try { save = JsonUtility.FromJson<SaveGame>(json); }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSystem] Fehler beim Lesen des Savegames: {ex}");
            return false;
        }

        // 3) Version prüfen
        if (save.version != saveVersion)
            Debug.LogWarning($"[SaveSystem] Save-Version {save.version} != erwartete {saveVersion}. Migration nötig?");

        // 4) Welt vorbereiten: dynamische Entities entfernen
        await ClearDynamicEntities();

        // 5) Entities respawnen & Zustand wiederherstellen
        await RespawnFromSave(save);

        // 6) Player wiederherstellen
        RestorePlayer(save.player);

        // 7) HUD wiederherstellen
        RestoreHUD(save.hud);

        OnAfterLoad?.Invoke(slotId);
        Debug.Log($"[SaveSystem] Geladen: {slotId}");
        return true;
    }

    public bool HasSlot(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId)) slotId = defaultSlot;
        return File.Exists(GetPath(slotId));
    }

    public bool DeleteSlot(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId)) slotId = defaultSlot;
        var path = GetPath(slotId);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public List<SaveSlotInfo> ListSlots()
    {
        EnsureFolder();
        var infos = new List<SaveSlotInfo>();
        foreach (var file in Directory.GetFiles(RootPath, "*.json"))
        {
            var slotId = Path.GetFileNameWithoutExtension(file);
            long ts = 0; int ver = 0;
            try
            {
                var text = File.ReadAllText(file);
                var sg = JsonUtility.FromJson<SaveGame>(text);
                ts = sg?.timestamp ?? 0;
                ver = sg?.version ?? 0;
            }
            catch { /* ignore broken files */ }

            infos.Add(new SaveSlotInfo
            {
                slotId = slotId,
                path = file,
                version = ver,
                timestamp = ts,
                lastWriteUtc = File.GetLastWriteTimeUtc(file)
            });
        }
        return infos.OrderByDescending(i => i.lastWriteUtc).ToList();
    }

    // --------------------------------------------------------------------------------------
    // Interna
    // --------------------------------------------------------------------------------------

    private string GetPath(string slotId)
        => Path.Combine(RootPath, $"{slotId}.json");

    private PlayerSaveData CapturePlayer()
    {
        var player = FindObjectOfTypeMono<IPlayerSavable>();
        if (player == null)
        {
            Debug.LogWarning("[SaveSystem] Kein IPlayerSavable gefunden – Player wird nicht gespeichert.");
            return null;
        }
        return player.Capture();
    }

    private void RestorePlayer(PlayerSaveData data)
    {
        if (data == null) return;
        var player = FindObjectOfTypeMono<IPlayerSavable>();
        if (player == null)
        {
            Debug.LogWarning("[SaveSystem] Kein IPlayerSavable gefunden – Player kann nicht wiederhergestellt werden.");
            return;
        }
        player.Restore(data);
    }

    private List<EntitySaveData> CaptureEntities()
    {
        var list = new List<EntitySaveData>();
        var registry = WorldRegistryOrNull();
        if (registry == null)
        {
            Debug.LogWarning("[SaveSystem] WorldRegistry nicht gefunden – es werden keine Entities gespeichert.");
            return list;
        }

        foreach (var e in registry.All)
        {
            try { list.Add(e.Capture()); }
            catch (Exception ex) { Debug.LogError($"[SaveSystem] Capture fehlgeschlagen für Entity: {ex}"); }
        }
        return list;
    }

    private async UniTask ClearDynamicEntities()
    {
        var registry = WorldRegistryOrNull();
        if (registry == null) return;

        var current = registry.All.ToList();
        foreach (var e in current)
        {
            if (e is Component c && c != null && c.gameObject != null)
                Destroy(c.gameObject);
        }
        await UniTask.Yield();
    }

    private async UniTask RespawnFromSave(SaveGame save)
    {
        if (save.entities == null || save.entities.Count == 0) return;

        bool hasFactory = EntityFactoryOrNull() != null;

        foreach (var data in save.entities)
        {
            try
            {
                IRegistrableEntity reg = null;

                if (hasFactory)
                {
                    var factory = EntityFactoryOrNull();
                    reg = await factory.Spawn(data.TypeId, data.Pos, data.Rot);
                }
                else
                {
                    var go = await Addressables.InstantiateAsync(data.TypeId, data.Pos, data.Rot).ToUniTask();
                    reg = go.GetComponent<IRegistrableEntity>();
                    if (reg == null)
                        Debug.LogWarning($"[SaveSystem] Instanziiertes Objekt hat kein IRegistrableEntity: {data.TypeId}");
                }

                reg?.Restore(data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Respawn fehlgeschlagen für {data?.TypeId}: {ex}");
            }
        }
    }

    // ---------- HUD ----------
    private HUDLayoutSaveData CaptureHUD()
    {
        var layout = new HUDLayoutSaveData();
        var panels = FindAllInterfaces<IHUDPanelSavable>();
        foreach (var p in panels)
        {
            try
            {
                var rt = (p as Component).GetComponent<RectTransform>();
                if (rt == null) continue;

                layout.panels.Add(new HUDPanelSaveData
                {
                    panelId = p.PanelId,
                    anchoredPosition = rt.anchoredPosition,
                    sizeDelta = rt.sizeDelta,
                    pivot = rt.pivot,
                    visible = p.IsVisible()
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] HUD-Capture fehlgeschlagen ({p?.PanelId}): {ex}");
            }
        }
        return layout;
    }

    private void RestoreHUD(HUDLayoutSaveData data)
    {
        if (data == null || data.panels == null) return;

        var dict = data.panels.ToDictionary(k => k.panelId, v => v);
        var panels = FindAllInterfaces<IHUDPanelSavable>();

        foreach (var p in panels)
        {
            if (!dict.TryGetValue(p.PanelId, out var s)) continue;

            try
            {
                var rt = (p as Component).GetComponent<RectTransform>();
                if (rt == null) continue;

                // Reihenfolge: Größe/Pivot vor Position, damit Layout korrekt gerechnet wird
                rt.pivot = s.pivot;
                rt.sizeDelta = s.sizeDelta;
                rt.anchoredPosition = s.anchoredPosition;
                p.SetVisible(s.visible);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] HUD-Restore fehlgeschlagen ({p?.PanelId}): {ex}");
            }
        }
    }

    private WorldRegistry WorldRegistryOrNull() => WorldRegistry.I;
    private EntityFactory EntityFactoryOrNull() => EntityFactory.I;

    private T FindObjectOfTypeMono<T>() where T : class
    {
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (mb is T t) return t;
        return null;
    }

    private List<T> FindAllInterfaces<T>() where T : class
    {
        var list = new List<T>();
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (mb is T t) list.Add(t);
        return list;
    }
}

// ==========================================================================================
// Schnittstellen & DTOs
// ==========================================================================================

public interface IPlayerSavable
{
    PlayerSaveData Capture();
    void Restore(PlayerSaveData data);
}

// ---- HUD-Panels ----
public interface IHUDPanelSavable
{
    string PanelId { get; }           // stabiler, eindeutiger Name/Key je Panel
    bool IsVisible();                 // aktuelle Sichtbarkeit
    void SetVisible(bool visible);    // Sichtbarkeit setzen (ggf. mit eigener Logik)
}

[Serializable]
public class SaveGame
{
    public int version = 2;
    public long timestamp;
    public PlayerSaveData player;
    public List<EntitySaveData> entities = new();
    public HUDLayoutSaveData hud; // NEU
}

[Serializable]
public class PlayerSaveData
{
    public Vector3 position;
    public Quaternion rotation;
}

[Serializable]
public class EntitySaveData
{
    public string Guid;        // stabiler Identifier
    public string TypeId;      // Addressables/Factory-Key
    public Vector3 Pos;
    public Quaternion Rot;
    public string StateJson;   // eingebettete JSON des spezifischen Zustands
}

[Serializable]
public class HUDLayoutSaveData
{
    public List<HUDPanelSaveData> panels = new();
}

[Serializable]
public class HUDPanelSaveData
{
    public string panelId;
    public Vector2 anchoredPosition;
    public Vector2 sizeDelta;
    public Vector2 pivot;
    public bool visible;
}

[Serializable]
public struct HUDPayload
{
    public string Name;
    public Vector3 Position;
}

[Serializable]
public struct SerializedGuid
{
    [SerializeField] private string _value;
    public Guid Value => string.IsNullOrEmpty(_value) ? Guid.Empty : Guid.Parse(_value);
    public void Ensure() { if (string.IsNullOrEmpty(_value)) _value = Guid.NewGuid().ToString(); }
}

public struct SaveSlotInfo
{
    public string slotId;
    public string path;
    public int version;
    public long timestamp;
    public DateTime lastWriteUtc;
}
