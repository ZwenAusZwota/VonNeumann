using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Einfaches Volumen-Inventar für die Sonde.</summary>
public class ProbeInventory : MonoBehaviour
{
    [Tooltip("Maximales Füllvolumen in m³")]
    public float maxVolume = 500f;

    float usedVolume;
    readonly Dictionary<string, float> store = new();   // MaterialId → Einheiten

    public float FreeVolume => maxVolume - usedVolume;

    public event Action<float, float> CargoChanged;   // used, max

    /// <returns> Tatsächlich eingelagerten Einheiten </returns>
    public float Add(string materialId, float units)
    {
        var def = MaterialRegistry.Get(materialId);
        float vReq = units * def.volumePerUnit;

        if (vReq > FreeVolume)
            units = FreeVolume / def.volumePerUnit;     // nur Teil passt rein

        if (units <= 0f) return 0f;

        usedVolume += units * def.volumePerUnit;
        store[materialId] = store.TryGetValue(materialId, out var cur) ? cur + units : units;

        //HUDController hud = FindObjectOfType<HUDController>();
        // if (hud) hud.SetCargoText(usedVolume, maxVolume);
        CargoChanged?.Invoke(usedVolume, maxVolume);
        return units;
    }
}
