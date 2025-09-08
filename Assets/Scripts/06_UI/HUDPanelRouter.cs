using System.Linq;
using UnityEngine;
using SpaceGame.UI; // für DraggableHudPanel

public class HUDPanelRouter : MonoBehaviour
{
    /// <summary>Aktive UI-Instanz der 10_Game_UI Szene.</summary>
    public static HUDPanelRouter Active { get; private set; }

    [Header("Panels aus der 10_Game_UI Szene")]
    [SerializeField] private GameObject scanPanel;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject navPanel;

    private void Awake() => Active = this;

    private void OnDestroy()
    {
        if (Active == this) Active = null;
    }

    // ---- API für Hotkeys / andere Systeme ----
    public void ToggleScan() => ToggleEffective(scanPanel, "ScanPanel");
    public void ToggleInventory() => ToggleEffective(inventoryPanel, "InventoryPanel");
    public void ToggleNav() => ToggleEffective(navPanel, "NavPanel");

    public void ShowScan(bool on) => EnsureOnState(scanPanel, on, "ScanPanel");
    public void ShowInventory(bool on) => EnsureOnState(inventoryPanel, on, "InventoryPanel");
    public void ShowNav(bool on) => EnsureOnState(navPanel, on, "NavPanel");

    /*────────────────────────────────────────────────────────────────*/

    private static void ToggleEffective(GameObject root, string nameForLog)
    {
        if (!root)
        {
            Debug.LogWarning($"[HUDPanelRouter] {nameForLog} ist nicht zugewiesen.");
            return;
        }

        bool anyChildVisible = HasAnyVisibleChild(root);

        if (anyChildVisible)
        {
            // Effektiv sichtbar -> alles schließen
            CloseAllChildren(root);
            root.SetActive(false);
        }
        else
        {
            // Effektiv unsichtbar -> Container an + alle Kinder zeigen
            root.SetActive(true);
            ShowAllChildren(root);
        }
    }

    private static void EnsureOnState(GameObject root, bool on, string nameForLog)
    {
        if (!root)
        {
            Debug.LogWarning($"[HUDPanelRouter] {nameForLog} fehlt.");
            return;
        }

        if (on)
        {
            root.SetActive(true);
            ShowAllChildren(root);
        }
        else
        {
            // zuerst Kinder sauber schließen (PlayerPrefs-Flags etc.), dann Container aus
            CloseAllChildren(root);
            root.SetActive(false);
        }
    }

    private static bool HasAnyVisibleChild(GameObject root)
    {
        if (!root || !root.activeSelf) return false;
        var panels = root.GetComponentsInChildren<DraggableHudPanel>(true);
        return panels != null && panels.Any(p => p != null && p.gameObject.activeSelf);
    }

    private static void ShowAllChildren(GameObject root)
    {
        var panels = root.GetComponentsInChildren<DraggableHudPanel>(true);
        if (panels == null) return;
        foreach (var p in panels)
        {
            if (p == null) continue;
            p.ShowPanel(); // setzt ggf. rememberVisibility=1 und aktiviert das GameObject
        }
    }

    private static void CloseAllChildren(GameObject root)
    {
        var panels = root.GetComponentsInChildren<DraggableHudPanel>(true);
        if (panels == null) return;
        foreach (var p in panels)
        {
            if (p == null) continue;
            p.ClosePanel(); // setzt ggf. rememberVisibility=0 und deaktiviert das GameObject
        }
    }
}
