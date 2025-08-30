// Assets/Scripts/00_Manager/SaveSystem.cs
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;

/// <summary>
/// Zentrales Speichersystem (DontDestroyOnLoad).
/// - JSON-Datei pro Slot unter Application.persistentDataPath/SaveSlots
/// - Speichert Player + alle registrierten Entities (über WorldRegistry/IRegistrableEntity)
/// - Lädt Slot: entfernt dynamische Objekte, respawnt aus TypeId (EntityFactory bevorzugt, sonst Addressables)
/// </summary>
public class SaveSystem : MonoBehaviour
{
    public static SaveSystem I { get; private set; }

    [Header("Allgemein")]
    [Tooltip("Version des Save-Formats – bei Änderungen erhöhen und ggf. Migration implementieren.")]
    [SerializeField] private int saveVersion = 1;

    [Tooltip("Ordnername relativ zu Application.persistentDataPath.")]
    [SerializeField] private string folderName = "SaveSlots";

    [Tooltip("Optionaler Default-Slotname (z. B. Autosave).")]
    [SerializeField] private string defaultSlot = "slot_1";

    public event Action<string> OnBeforeSave;
    public event Action<string> OnAfterSave;
    public event Action<string> OnBeforeLoad;
    public event Action<string> OnAfterLoad;

    private string RootPath => Path.Combine(Application.persistentDataPath, folderName);

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
            entities = CaptureEntities()
        };

        // 2) Serialisieren
        string json = JsonUtility.ToJson(save, false);

        // 3) Atomisch schreiben (Threadpool, damit der Mainthread nicht blockiert)
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
            // Zurück auf den Mainthread
            await UniTask.SwitchToMainThread();
        }

        OnAfterSave?.Invoke(slotId);
        Debug.Log($"[SaveSystem] Gespeichert: {slotId} @ {path}");
    }

    /// <summary>
    /// Lädt den Spielstand aus dem Slot. Erwartet, dass die Spielszene bereits aktiv ist
    /// (z. B. via SceneRouter: Loading -> Game + GameUI), da hier nur dynamische Objekte respawnt werden.
    /// </summary>
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
        try
        {
            json = File.ReadAllText(path);
        }
        finally
        {
            await UniTask.SwitchToMainThread();
        }

        // 2) Deserialisieren
        SaveGame save = null;
        try
        {
            save = JsonUtility.FromJson<SaveGame>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveSystem] Fehler beim Lesen des Savegames: {ex}");
            return false;
        }

        // 3) Version prüfen (hier nur Log; echte Migration nach Bedarf implementieren)
        if (save.version != saveVersion)
            Debug.LogWarning($"[SaveSystem] Save-Version {save.version} != erwartete {saveVersion}. Migration nötig?");

        // 4) Welt vorbereiten: dynamische Entities entfernen
        await ClearDynamicEntities();

        // 5) Entities respawnen & Zustand wiederherstellen
        await RespawnFromSave(save);

        // 6) Player wiederherstellen
        RestorePlayer(save.player);

        OnAfterLoad?.Invoke(slotId);
        Debug.Log($"[SaveSystem] Geladen: {slotId}");
        return true;
    }

    /// <summary>Prüft, ob der Slot existiert.</summary>
    public bool HasSlot(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId)) slotId = defaultSlot;
        return File.Exists(GetPath(slotId));
    }

    /// <summary>Löscht den angegebenen Slot.</summary>
    public bool DeleteSlot(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId)) slotId = defaultSlot;
        var path = GetPath(slotId);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    /// <summary>Listet alle vorhandenen Slots inkl. Metadaten (Version, Timestamp).</summary>
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
        // Neueste zuerst
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
            try
            {
                list.Add(e.Capture());
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Capture fehlgeschlagen für Entity: {ex}");
            }
        }
        return list;
    }

    /// <summary>Entfernt alle aktuell registrierten dynamischen Entities aus der Welt.</summary>
    private async UniTask ClearDynamicEntities()
    {
        var registry = WorldRegistryOrNull();
        if (registry == null) return;

        // Snapshot bilden, da beim Destroy die Registry modifiziert wird
        var current = registry.All.ToList();

        foreach (var e in current)
        {
            if (e is Component c && c != null && c.gameObject != null)
                Destroy(c.gameObject);
        }

        // Einen Frame warten, damit Unity die Zerstörung verarbeitet
        await UniTask.Yield();
    }

    /// <summary>Respawnt alle Entities aus dem SaveGame. Bevorzugt EntityFactory, sonst Addressables.</summary>
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
                    // Über zentrale Factory spawnen (empfohlen)
                    var factory = EntityFactoryOrNull();
                    reg = await factory.Spawn(data.TypeId, data.Pos, data.Rot);
                }
                else
                {
                    // Fallback: direkt über Addressables instantiieren
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

    private WorldRegistry WorldRegistryOrNull()
        => WorldRegistry.I;

    private EntityFactory EntityFactoryOrNull()
        => EntityFactory.I;

    /// <summary>
    /// Findet die erste Komponente im aktiven Spiel, die ein bestimmtes Interface implementiert.
    /// Unity erlaubt kein FindObjectOfType mit Interface, daher Workaround.
    /// </summary>
    private T FindObjectOfTypeMono<T>() where T : class
    {
        // Alle MonoBehaviours durchsuchen und erstes Interface-Match zurückgeben
        foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mb is T t) return t;
        }
        return null;
    }
}

// ==========================================================================================
// Schnittstellen & DTOs (minimal, damit SaveSystem out-of-the-box lauffähig ist)
// Du kannst diese bei Bedarf in eigene Dateien auslagern.
// ==========================================================================================

/// <summary>
/// Player-Komponente soll dieses Interface implementieren, damit Save/Load funktioniert.
/// </summary>
public interface IPlayerSavable
{
    PlayerSaveData Capture();
    void Restore(PlayerSaveData data);
}

/// <summary>
/// Muss von deinen dynamischen Spielobjekten (Miner, Probe, Asteroid, Station, …) implementiert werden.
/// </summary>
//public interface IRegistrableEntity
//{
//    SerializedGuid Guid { get; }
//    string TypeId { get; }                 // Addressables-Key / Factory-Key
//    HUDPayload GetHUDPayload();            // Für HUD (nicht zwingend fürs Speichern nötig)
//    EntitySaveData Capture();              // Laufzeit-Zustand -> DTO
//    void Restore(EntitySaveData data);     // DTO -> Laufzeit-Zustand
//}

///// <summary>
///// Simple Factory zum Spawnen – sollte es bereits geben. Hier als Singleton erwartet.
///// </summary>
//public class EntityFactory : MonoBehaviour
//{
//    public static EntityFactory I { get; private set; }
//    private void Awake()
//    {
//        if (I != null && I != this) { Destroy(gameObject); return; }
//        I = this;
//        DontDestroyOnLoad(gameObject);
//    }

//    public async UniTask<IRegistrableEntity> Spawn(string typeId, Vector3 pos, Quaternion rot)
//    {
//        // Default: Addressables – kannst du nach Bedarf anpassen (Pooling etc.)
//        var go = await Addressables.InstantiateAsync(typeId, pos, rot).ToUniTask();
//        var reg = go.GetComponent<IRegistrableEntity>();
//        if (reg == null) Debug.LogWarning($"[EntityFactory] Instanz ohne IRegistrableEntity: {typeId}");
//        return reg;
//    }
//}

// -------------------------------- DTOs --------------------------------

[Serializable]
public class SaveGame
{
    public int version = 1;
    public long timestamp;
    public PlayerSaveData player;
    public List<EntitySaveData> entities = new();
}

[Serializable]
public class PlayerSaveData
{
    public Vector3 position;
    public Quaternion rotation;

    // Ergänze nach Bedarf:
    // public float health;
    // public InventorySaveData inventory;
    // public string currentScene; ...
}

[Serializable]
public class EntitySaveData
{
    public string Guid;        // stabiler Identifier
    public string TypeId;      // Addressables/Factory-Key
    public Vector3 Pos;
    public Quaternion Rot;
    public string StateJson;   // eingebettete JSON des spezifischen Zustands

    // Optional: zusätzliche Felder (Scale, Velocity, CustomFlags …)
}

[Serializable]
public struct HUDPayload
{
    public string Name;
    public Vector3 Position;
    // beliebig erweiterbar
}

[Serializable]
public struct SerializedGuid
{
    [SerializeField] private string _value;

    public Guid Value => string.IsNullOrEmpty(_value) ? Guid.Empty : Guid.Parse(_value);
    public void Ensure() { if (string.IsNullOrEmpty(_value)) _value = Guid.NewGuid().ToString(); }
}

/// <summary>Info für Savegame-Auswahl.</summary>
public struct SaveSlotInfo
{
    public string slotId;
    public string path;
    public int version;
    public long timestamp;
    public DateTime lastWriteUtc;
}
