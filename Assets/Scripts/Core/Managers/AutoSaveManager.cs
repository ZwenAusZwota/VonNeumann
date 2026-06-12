// Assets/Scripts/Core/Managers/AutoSaveManager.cs
using System;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// Automatisches Speichern beim Beenden und optional in regelmäßigen Intervallen
/// </summary>
public class AutoSaveManager : MonoBehaviour
{
    [Header("Auto-Save Settings")]
    [Tooltip("Automatisch speichern beim Beenden")]
    [SerializeField] private bool saveOnQuit = true;
    
    [Tooltip("Automatisch speichern beim Szenenwechsel (z.B. zurück zum Hauptmenü)")]
#pragma warning disable CS0414 // Field is assigned but never used (may be used by future features)
    [SerializeField] private bool saveOnSceneChange = true;
#pragma warning restore CS0414
    
    [Tooltip("Regelmäßiges Auto-Save aktivieren")]
    [SerializeField] private bool enablePeriodicAutoSave = true;
    
    [Tooltip("Intervall für Auto-Save in Sekunden (0 = deaktiviert)")]
    [SerializeField] private float autoSaveInterval = 300f; // 5 Minuten
    
    [Tooltip("Slot-ID für Auto-Save")]
    [SerializeField] private string autoSaveSlotId = "autosave";
    
    [Tooltip("Minimale Zeit zwischen Auto-Saves (verhindert zu häufiges Speichern)")]
    [SerializeField] private float minTimeBetweenSaves = 30f;
    
    [Header("UI Feedback")]
    [Tooltip("Zeige Nachricht beim Auto-Save")]
    [SerializeField] private bool showSaveMessage = true;
    
    private float timeSinceLastSave;
    private bool isSaving;
    private bool hasUnsavedChanges;
    
    private void Awake()
    {
        // Persistiere über Szenenwechsel
        DontDestroyOnLoad(gameObject);
    }
    
    private void Start()
    {
        timeSinceLastSave = 0f;
        
        // Registriere für Änderungen
        RegisterForChanges();
    }
    
    private void Update()
    {
        if (!enablePeriodicAutoSave || autoSaveInterval <= 0f)
            return;
            
        timeSinceLastSave += Time.deltaTime;
        
        // Auto-Save wenn Intervall erreicht und Änderungen vorhanden
        if (timeSinceLastSave >= autoSaveInterval && hasUnsavedChanges && !isSaving)
        {
            if (timeSinceLastSave >= minTimeBetweenSaves)
            {
                PerformAutoSave().Forget();
            }
        }
    }
    
    private void OnApplicationQuit()
    {
        if (saveOnQuit && !isSaving)
        {
            // Synchrones Speichern beim Beenden
            SaveOnQuitSync();
        }
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        // Auf mobilen Geräten: Speichern beim Pausieren
        if (pauseStatus && saveOnQuit && !isSaving)
        {
            PerformAutoSave().Forget();
        }
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        // Optional: Speichern wenn Fenster den Fokus verliert
        if (!hasFocus && saveOnQuit && !isSaving && hasUnsavedChanges)
        {
            PerformAutoSave().Forget();
        }
    }
    
    /// <summary>
    /// Führt ein Auto-Save durch (asynchron)
    /// </summary>
    public async UniTask PerformAutoSave()
    {
        if (isSaving)
        {
            Debug.LogWarning("[AutoSaveManager] Save bereits im Gange, überspringe.");
            return;
        }
        
        var saveSystem = ServiceContainer.Instance?.Get<SaveSystem>();
        if (saveSystem == null)
        {
            Debug.LogWarning("[AutoSaveManager] SaveSystem nicht verfügbar.");
            return;
        }
        
        isSaving = true;
        
        try
        {
            if (showSaveMessage)
            {
                HUDMessageBus.Post("Speichere...");
            }
            
            await saveSystem.SaveAsync(autoSaveSlotId);
            
            timeSinceLastSave = 0f;
            hasUnsavedChanges = false;
            
            if (showSaveMessage)
            {
                HUDMessageBus.Post("Gespeichert!");
            }
            
            Debug.Log($"[AutoSaveManager] Auto-Save erfolgreich: {autoSaveSlotId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AutoSaveManager] Fehler beim Auto-Save: {ex}");
            
            if (showSaveMessage)
            {
                HUDMessageBus.Post("Fehler beim Speichern!");
            }
        }
        finally
        {
            isSaving = false;
        }
    }
    
    /// <summary>
    /// Synchrones Speichern beim Beenden (blockierend)
    /// </summary>
    private void SaveOnQuitSync()
    {
        var saveSystem = ServiceContainer.Instance?.Get<SaveSystem>();
        if (saveSystem == null)
        {
            Debug.LogWarning("[AutoSaveManager] SaveSystem nicht verfügbar für Quit-Save.");
            return;
        }
        
        try
        {
            Debug.Log("[AutoSaveManager] Speichere beim Beenden...");
            
            // Verwende synchrones Speichern
            var save = new SaveGame
            {
                version = 2, // Sollte mit SaveSystem übereinstimmen
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                player = CapturePlayer(),
                entities = CaptureEntities(),
                hud = CaptureHUD()
            };
            
            string json = JsonUtility.ToJson(save, false);
            string path = GetSavePath(autoSaveSlotId);
            
            System.IO.File.WriteAllText(path, json);
            
            Debug.Log($"[AutoSaveManager] Quit-Save erfolgreich: {autoSaveSlotId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AutoSaveManager] Fehler beim Quit-Save: {ex}");
        }
    }
    
    /// <summary>
    /// Markiert, dass Änderungen vorliegen
    /// </summary>
    public void MarkUnsavedChanges()
    {
        hasUnsavedChanges = true;
    }
    
    /// <summary>
    /// Registriert für Änderungs-Events
    /// </summary>
    private void RegisterForChanges()
    {
        // Bei WorldRegistry-Änderungen markieren
        var worldRegistry = ServiceContainer.Instance?.Get<WorldRegistry>();
        if (worldRegistry != null)
        {
            // Markiere Änderungen bei Entity-Änderungen
            hasUnsavedChanges = true; // Initial als geändert markieren
        }
    }
    
    // Helper-Methoden (kopiert aus SaveSystem für Quit-Save)
    private PlayerSaveData CapturePlayer()
    {
        var player = FindAnyObjectByType<MonoBehaviour>() as IPlayerSavable;
        if (player == null) return null;
        return player.Capture();
    }
    
    private System.Collections.Generic.List<EntitySaveData> CaptureEntities()
    {
        var list = new System.Collections.Generic.List<EntitySaveData>();
        var registry = ServiceContainer.Instance?.Get<WorldRegistry>();
        if (registry == null) return list;
        
        foreach (var e in registry.All)
        {
            try { list.Add(e.Capture()); }
            catch (Exception ex) { Debug.LogError($"[AutoSaveManager] Capture fehlgeschlagen: {ex}"); }
        }
        return list;
    }
    
    private HUDLayoutSaveData CaptureHUD()
    {
        // Optional: HUD-Layout speichern
        return new HUDLayoutSaveData();
    }
    
    private string GetSavePath(string slotId)
    {
        string folder = System.IO.Path.Combine(Application.persistentDataPath, "SaveSlots");
        if (!System.IO.Directory.Exists(folder))
            System.IO.Directory.CreateDirectory(folder);
        return System.IO.Path.Combine(folder, $"{slotId}.json");
    }
}

