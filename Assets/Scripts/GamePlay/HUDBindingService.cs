// Assets/Scripts/UI/HUDBindingController.cs
using System;
using UnityEngine;

public static class HUDBindingService
{
    public static GameObject CurrentObject { get; private set; }
    public static FabricatorController CurrentFabricator { get; private set; }
    public static InventoryController CurrentInventory { get; private set; }

    public static Transform CurrentNavTarget { get; private set; }

    public static event Action<GameObject> ActiveObjectChanged;
    public static event Action<FabricatorController> ActiveFabricatorChanged;
    public static event Action<InventoryController> ActiveInventoryChanged;

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
    }
}
