using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PreloadCatalog", menuName = "Game/Preload Catalog")]
public class PreloadCatalog : ScriptableObject
{
    [Tooltip("Addressables-Keys oder -Labels, die beim Start vorab geladen werden sollen.")]
    public List<string> Keys = new List<string>();
}
