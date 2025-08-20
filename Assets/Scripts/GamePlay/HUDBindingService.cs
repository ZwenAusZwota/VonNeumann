// Assets/Scripts/UI/HUDBindingController.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public static class HUDBindingService
{
    /* ───────────── Aktives Objekt & Module ───────────── */
    public static GameObject CurrentObject { get; private set; }
    public static FabricatorController CurrentFabricator { get; private set; }
    public static InventoryController CurrentInventory { get; private set; }

    public static event Action<GameObject> ActiveObjectChanged;
    public static event Action<FabricatorController> ActiveFabricatorChanged;
    public static event Action<InventoryController> ActiveInventoryChanged;

    /* ───────────── Scan-Ergebnisse (pro Liste) ───────────── */
    // Quelle (GameObject) + Ergebnisliste für Near-/Far-Scan
    public static event Action<GameObject, List<SystemObject>> NearScanResults;
    public static event Action<GameObject, List<SystemObject>> FarScanResults;

    /* ───────────── Navigation ───────────── */
    public static Transform CurrentNavTarget { get; private set; }
    // Meldet, dass vom HUD ein Ziel gewählt wurde – mit aktivem Objekt als Kontext
    public static event Action<GameObject, Transform> NavTargetSelected;

    public static void Select(GameObject go)
    {
        CurrentObject = go;

        FabricatorController fab = null;
        InventoryController inv = null;
        if (go != null)
        {
            fab = go.GetComponent<FabricatorController>()
               ?? go.GetComponentInChildren<FabricatorController>(true);
            inv = go.GetComponent<InventoryController>()
               ?? go.GetComponentInChildren<InventoryController>(true);
        }

        CurrentFabricator = fab;
        CurrentInventory = inv;

        ActiveObjectChanged?.Invoke(CurrentObject);
        ActiveFabricatorChanged?.Invoke(CurrentFabricator);
        ActiveInventoryChanged?.Invoke(CurrentInventory);
    }

    public static void Reannounce()
    {
        ActiveObjectChanged?.Invoke(CurrentObject);
        ActiveFabricatorChanged?.Invoke(CurrentFabricator);
        ActiveInventoryChanged?.Invoke(CurrentInventory);

        if (CurrentNavTarget != null)
            NavTargetSelected?.Invoke(CurrentObject, CurrentNavTarget);
    }

    /* ───────────── Schnittstelle für ScannerController ───────────── */
    public static void PublishNearScan(GameObject source, List<SystemObject> entries)
    {
        // nur weiterreichen, wenn das Source-Objekt aktuell gebunden ist
        if (source == CurrentObject) NearScanResults?.Invoke(source, entries);
    }

    public static void PublishFarScan(GameObject source, List<SystemObject> entries)
    {
        if (source == CurrentObject) FarScanResults?.Invoke(source, entries);
    }

    /* ───────────── Triggers vom HUD (Buttons) ───────────── */
    public static void RequestNearScan()
    {
        var sc = CurrentObject ?
            (CurrentObject.GetComponent<ScannerController>()
             ?? CurrentObject.GetComponentInChildren<ScannerController>(true))
            : null;
        if (sc) sc.PerformNearScan();
    }

    public static void RequestFarScan()
    {
        var sc = CurrentObject ?
            (CurrentObject.GetComponent<ScannerController>()
             ?? CurrentObject.GetComponentInChildren<ScannerController>(true))
            : null;
        if (sc) sc.PerformFarScan();
    }

    /* ───────────── Zielauswahl aus dem HUD ───────────── */
    public static void SelectNavTarget(Transform target)
    {
        CurrentNavTarget = target;
        NavTargetSelected?.Invoke(CurrentObject, CurrentNavTarget);
    }
}
