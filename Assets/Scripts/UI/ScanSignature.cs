// Assets/Scripts/HUD/ScanSignature.cs
using UnityEngine;

public class ScanSignature : MonoBehaviour
{
    public bool showAsSystemObject = true; // FarScan
    public string displayNameOverride;

    public string ComposeFarLabel()
    {
        int moonCount = 0;
        foreach (Transform child in transform)
            if (child.CompareTag("Moon")) moonCount++;
        string n = string.IsNullOrEmpty(displayNameOverride) ? name : displayNameOverride;
        return moonCount > 0 ? $"{n} (+{moonCount} Monde)" : n;
    }
}
