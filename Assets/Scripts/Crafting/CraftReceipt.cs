// Assets/Scripts/Crafting/CraftRecipe.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCraftRecipe", menuName = "SpaceGame/Craft Recipe")]
public class CraftRecipe : ScriptableObject
{
    public string id;                     // z. B. "small_drone"
    public string displayName;            // UI-Titel
    public GameObject prefab;             // Endprodukt
    public float buildTime = 5f;          // Sekunden (optional 0 = instant)

    [Serializable] public struct Cost { public string resource; public int amount; }
    public List<Cost> costs = new();      // z. B. {"iron", 120}, {"water", 50}
}
