using UnityEngine;

public class HUDPanelRouter : MonoBehaviour
{
    /// <summary>Aktive UI-Instanz der 10_Game_UI Szene.</summary>
    public static HUDPanelRouter Active { get; private set; }

    [Header("Panels aus der 10_Game_UI Szene")]
    [SerializeField] private GameObject scanPanel;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject navPanel;

    private void Awake()
    {
        // Die zuletzt geladene UI setzt sich als Active.
        Active = this;
    }

    private void OnDestroy()
    {
        if (Active == this) Active = null;
    }

    // ---- API für Hotkeys / andere Systeme ----
    public void ToggleScan() => Toggle(scanPanel, "ScanPanel");
    public void ToggleInventory() => Toggle(inventoryPanel, "InventoryPanel");
    public void ToggleNav() => Toggle(navPanel, "NavPanel");

    public void ShowScan(bool on) { if (scanPanel) scanPanel.SetActive(on); }
    public void ShowInventory(bool on) { if (inventoryPanel) inventoryPanel.SetActive(on); }
    public void ShowNav(bool on) { if (navPanel) navPanel.SetActive(on); }

    private static void Toggle(GameObject go, string nameForLog)
    {
        if (!go)
        {
            Debug.LogWarning($"[HUDPanelRouter] {nameForLog} ist nicht zugewiesen.");
            return;
        }
        go.SetActive(!go.activeSelf);
    }
}
