using System;
using UnityEngine;

public static class FabricatorBindingService
{
    public static event Action<FabricatorController> ActiveFabricatorChanged;

    public static FabricatorController Current { get; private set; }

    public static void Select(GameObject go)
    {
        FabricatorController fab = null;
        if (go != null)
        {
            fab = go.GetComponent<FabricatorController>()
               ?? go.GetComponentInChildren<FabricatorController>(true);
        }

        Current = fab;
        ActiveFabricatorChanged?.Invoke(Current);
    }

    // optional: für Panel-Controller beim OnEnable:
    public static void Reannounce() => ActiveFabricatorChanged?.Invoke(Current);
}


