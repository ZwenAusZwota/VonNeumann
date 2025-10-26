using System;
using UnityEngine;
using SpaceGame.Core.Managers;

/// <summary>
/// Zentrales Event-System für das VonNeumann-Sonde Bobiverse-Spiel.
/// Ersetzt alle fragmentierten Event-Systeme durch eine einheitliche Lösung.
/// </summary>
#pragma warning disable CS0414 // Field is assigned but never used (Events may be used by future features)
public static class GameEvents
{
    // ==================== HUD Events ====================
    
    /// <summary>HUD-Nachricht anzeigen</summary>
    public static event Action<string> OnHUDMessage;
    
    /// <summary>HUD-Panel geändert</summary>
    public static event Action<string, bool> OnPanelToggled; // panelName, isVisible
    
    /// <summary>HUD-Layout geändert</summary>
    public static event Action OnHUDLayoutChanged;
    
    // ==================== Inventory Events ====================
    
    /// <summary>Inventar geändert</summary>
    public static event Action<InventoryItem> OnInventoryChanged;
    
    /// <summary>Inventar-Volumen geändert</summary>
    public static event Action<float, float> OnCargoChanged; // used, max
    
    /// <summary>Material hinzugefügt/entfernt</summary>
    public static event Action<string, float> OnMaterialChanged; // materialId, amount
    
    // ==================== Scanning Events ====================
    
    /// <summary>Objekt gescannt</summary>
    public static event Action<SystemObject> OnObjectScanned;
    
    /// <summary>Scan abgeschlossen</summary>
    public static event Action<ScanResult> OnScanCompleted;
    
    /// <summary>Scan-Panel geöffnet/geschlossen</summary>
    public static event Action<bool> OnScanPanelToggled;
    
    // ==================== Mining Events ====================
    
    /// <summary>Mining gestartet/gestoppt</summary>
    public static event Action<bool> OnMiningStateChanged; // isMining
    
    /// <summary>Material abgebaut</summary>
    public static event Action<string, float> OnMaterialMined; // materialId, amount
    
    /// <summary>Asteroid erschöpft</summary>
    public static event Action<MineableAsteroid> OnAsteroidExhausted;
    
    // ==================== Crafting Events ====================
    
    /// <summary>Produktion gestartet</summary>
    public static event Action<ProductBlueprint> OnProductionStarted;
    
    /// <summary>Produktion abgeschlossen</summary>
    public static event Action<ProductBlueprint> OnProductionCompleted;
    
    /// <summary>Fabricator-Queue geändert</summary>
    public static event Action OnFabricatorQueueChanged;
    
    // ==================== Probe Events ====================
    
    /// <summary>Sonde ausgewählt</summary>
    public static event Action<GameObject> OnProbeSelected;
    
    /// <summary>Sonde-Status geändert</summary>
    public static event Action<string> OnProbeStatusChanged;
    
    /// <summary>Autopilot gestartet/gestoppt</summary>
    public static event Action<bool> OnAutopilotStateChanged; // isActive
    
    // ==================== World Events ====================
    
    /// <summary>Welt-Objekt hinzugefügt</summary>
    public static event Action<SystemObject> OnWorldObjectAdded;
    
    /// <summary>Welt-Objekt entfernt</summary>
    public static event Action<SystemObject> OnWorldObjectRemoved;
    
    /// <summary>Welt-Objekt geändert</summary>
    public static event Action<SystemObject> OnWorldObjectChanged;
    
    // ==================== Scene Events ====================
    
    /// <summary>Szene gewechselt</summary>
    public static event Action<string> OnSceneChanged; // sceneName
    
    /// <summary>Spiel pausiert/fortgesetzt</summary>
    public static event Action<bool> OnGamePaused; // isPaused
    
    // ==================== Utility Methods ====================
    
    /// <summary>HUD-Nachricht senden</summary>
    public static void PostHUDMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            OnHUDMessage?.Invoke(message);
    }
    
    /// <summary>Panel toggeln</summary>
    public static void TogglePanel(string panelName, bool isVisible)
    {
        OnPanelToggled?.Invoke(panelName, isVisible);
    }
    
    /// <summary>Panel toggeln (verwendet aktuellen Zustand)</summary>
    public static void TogglePanel(string panelName)
    {
        // Finde den UIPanelManager und verwende dessen Toggle-Funktionalität
        var uiPanelManager = UnityEngine.Object.FindFirstObjectByType<UIPanelManager>();
        if (uiPanelManager != null)
        {
            uiPanelManager.TogglePanelViaEvent(panelName);
        }
        else
        {
            Debug.LogWarning($"[GameEvents] UIPanelManager not found for toggling panel {panelName}");
        }
    }
    
    /// <summary>Scan-Panel toggeln</summary>
    public static void ToggleScanPanel(bool isVisible)
    {
        OnScanPanelToggled?.Invoke(isVisible);
    }
    
    /// <summary>Sonde ausgewählt</summary>
    public static void SelectProbe(GameObject probe)
    {
        OnProbeSelected?.Invoke(probe);
    }
    
    /// <summary>Mining-Status ändern</summary>
    public static void ChangeMiningState(bool isMining)
    {
        OnMiningStateChanged?.Invoke(isMining);
    }
    
    /// <summary>Alle Events löschen (für Tests oder Neustart)</summary>
    public static void ClearAllEvents()
    {
        OnHUDMessage = null;
        OnPanelToggled = null;
        OnHUDLayoutChanged = null;
        OnInventoryChanged = null;
        OnCargoChanged = null;
        OnMaterialChanged = null;
        OnObjectScanned = null;
        OnScanCompleted = null;
        OnScanPanelToggled = null;
        OnMiningStateChanged = null;
        OnMaterialMined = null;
        OnAsteroidExhausted = null;
        OnProductionStarted = null;
        OnProductionCompleted = null;
        OnFabricatorQueueChanged = null;
        OnProbeSelected = null;
        OnProbeStatusChanged = null;
        OnAutopilotStateChanged = null;
        OnWorldObjectAdded = null;
        OnWorldObjectRemoved = null;
        OnWorldObjectChanged = null;
        OnSceneChanged = null;
        OnGamePaused = null;
    }
}

// ==================== Event Data Classes ====================

/// <summary>Scan-Ergebnis für Event-System</summary>
[System.Serializable]
public class ScanResult
{
    public SystemObject[] objects;
    public float scanRadius;
    public Vector3 scanPosition;
    public float scanTime;
}

/// <summary>Inventar-Item für Event-System</summary>
[System.Serializable]
public class InventoryItem
{
    public string materialId;
    public float amount;
    public float volume;
    public string displayName;
}
#pragma warning restore CS0414
