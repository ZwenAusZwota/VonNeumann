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

    private void OnEnable()
    {
        if (HUDBindingService.I != null)
        {
            HUDBindingService.I.OnSelectionChanged += HandleSelectionChanged;
            HUDBindingService.I.OnItemChanged += HandleItemChanged;
            HUDBindingService.I.OnListReset += _ => RefreshAll();
        }
        RefreshAll();
    }

    private void OnDisable()
    {
        if (HUDBindingService.I != null)
        {
            HUDBindingService.I.OnSelectionChanged -= HandleSelectionChanged;
            HUDBindingService.I.OnItemChanged -= HandleItemChanged;
            HUDBindingService.I.OnListReset -= _ => RefreshAll();
        }
    }

    // ───────────────────── HUD-Buttons ─────────────────────
    public void OnNearScanClicked()
    {
        var sel = HUDBindingService.I?.SelectedItem;
        var tr = sel?.Transform;
        if (!tr) return;

        var near = tr.GetComponent<NearScannerController>();
        if (near != null)
        {
            near.PerformScan(); // Ergebnisse landen im NearScanViewModel, HUD wird via Registry.NotifyChanged getriggert
        }
    }

    public void OnFarScanClicked()
    {
        var sel = HUDBindingService.I?.SelectedItem;
        var tr = sel?.Transform;
        if (!tr) return;

        var far = tr.GetComponent<FarScannerController>();
        if (far != null)
        {
            far.PerformScan(); // Ergebnisse landen im FarScanViewModel, HUD wird via Registry.NotifyChanged getriggert
        }
    }

    // ───────────────────── Ereignisse ─────────────────────
    private void HandleSelectionChanged(HUDItem item)
    {
        RefreshAll();
    }

    private void HandleItemChanged(HUDItem item)
    {
        // Nur aktualisieren, wenn das geänderte Item die Selektion ist
        var sel = HUDBindingService.I?.SelectedItem;
        if (sel != null && item != null && sel.Id == item.Id)
            RefreshAll();
    }

    // ───────────────────── Rendering ─────────────────────
    private void RefreshAll()
    {
        var sel = HUDBindingService.I?.SelectedItem;
        var tr = sel?.Transform;

        if (!tr)
        {
            SetNearCount(0);
            SetFarCount(0);
            ClearContainer(nearContainer);
            ClearContainer(farContainer);
            return;
        }

        var nearVM = tr.GetComponent<NearScanViewModel>();
        var farVM = tr.GetComponent<FarScanViewModel>();

        var nearEntries = nearVM != null ? new List<SystemObject>(nearVM.LatestEntries) : new List<SystemObject>();
        var farEntries = farVM != null ? new List<SystemObject>(farVM.LatestEntries) : new List<SystemObject>();

        SetNearCount(nearEntries.Count);
        SetFarCount(farEntries.Count);

        RebuildList(nearContainer, listItemPrefabNear, nearEntries);
        RebuildList(farContainer, listItemPrefabFar, farEntries);
    }

    private void SetNearCount(int count)
    {
        if (txtNearScan) txtNearScan.text = count > 0 ? $"NearScan: {count}" : "NearScan: –";
    }

    private void SetFarCount(int count)
    {
        if (txtFarScan) txtFarScan.text = count > 0 ? $"FarScan: {count}" : "FarScan: –";
    }

    private void ClearContainer(Transform container)
    {
        if (!container) return;
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
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
                item.Init(so);
            }
        }

        // Optional: Layout sofort aktualisieren (falls nötig)
        var rect = container as RectTransform;
        if (rect) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }
}
