// Assets/Scripts/Bootstrap/HubRegistryBootstrap.cs
using UnityEngine;

[DefaultExecutionOrder(-10000)] // sehr früh, noch vor den meisten Awakes
public class HubRegistryBootstrap : MonoBehaviour
{
    [Tooltip("Objekt über Szenenwechsel behalten.")]
    public bool dontDestroyOnLoad = true;

    void Awake()
    {
        Ensure();
        if (dontDestroyOnLoad) DontDestroyOnLoad(HubRegistry.Instance.gameObject);
    }

    /// <summary>Kann überall aufgerufen werden, um eine vorhandene HubRegistry sicherzustellen.</summary>
    public static void Ensure()
    {
        if (HubRegistry.Instance != null) return;

        var go = new GameObject("HubRegistry");
        // Wichtig: Deine bestehende HubRegistry-Klasse muss ein MonoBehaviour sein,
        // das in Awake o.ä. 'Instance = this' setzt.
        go.AddComponent<HubRegistry>();
    }
}
