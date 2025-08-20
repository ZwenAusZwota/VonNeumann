// Assets/Scripts/UI/ScanPanelController.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScanPanelController : MonoBehaviour
{
    [Header("UI Targets")]
    [SerializeField] private Transform nearContainer;         // ScrollView/Viewport/Content
    [SerializeField] private Transform farContainer;          // ScrollView/Viewport/Content
    [SerializeField] private GameObject listItemPrefabNear;   // Prefab mit ObjectItemUI
    [SerializeField] private GameObject listItemPrefabFar;    // Prefab mit ObjectItemUI
    [SerializeField] private TextMeshProUGUI txtNearScan;
    [SerializeField] private TextMeshProUGUI txtFarScan;

    void OnEnable()
    {
        HUDBindingService.NearScanResults += OnNearScanResults;
        HUDBindingService.FarScanResults += OnFarScanResults;
    }

    void OnDisable()
    {
        HUDBindingService.NearScanResults -= OnNearScanResults;
        HUDBindingService.FarScanResults -= OnFarScanResults;
    }

    /* ───────────── HUD-Buttons ───────────── */
    public void OnNearScanClicked() => HUDBindingService.RequestNearScan();   // nutzt CurrentObject-Scanner
    public void OnFarScanClicked() => HUDBindingService.RequestFarScan();    // s. HUDBindingService API 

    /* ───────────── Ergebnisverarbeitung ───────────── */
    private void OnNearScanResults(GameObject source, List<SystemObject> entries)
    {
        if (txtNearScan) txtNearScan.text = entries.Count > 0 ? $"NearScan: {entries.Count}" : "NearScan: –";
        RebuildList(nearContainer, listItemPrefabNear, entries);
    }

    private void OnFarScanResults(GameObject source, List<SystemObject> entries)
    {
        if (txtFarScan) txtFarScan.text = entries.Count > 0 ? $"FarScan: {entries.Count}" : "FarScan: –";
        RebuildList(farContainer, listItemPrefabFar, entries);
    }

    private void RebuildList(Transform container, GameObject prefab, List<SystemObject> entries)
    {
        if (!container || !prefab) return;

        // clear
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        // build
        foreach (var so in entries)
        {
            if (so == null || !so.GameObject) continue;

            var go = Instantiate(prefab, container);
            var item = go.GetComponent<ObjectItemUI>();
            if (item != null)
            {
                item.Init(so); // OnClick sendet HUDBindingService.SelectNavTarget(...)
            }
        }

        // Optional: Layout sofort aktualisieren, falls nötig
        LayoutRebuilder.ForceRebuildLayoutImmediate(container as RectTransform);
    }
}
