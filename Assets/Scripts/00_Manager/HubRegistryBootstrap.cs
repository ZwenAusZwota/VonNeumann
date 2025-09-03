// Assets/Scripts/Bootstrap/HubRegistryBootstrap.cs
using UnityEngine;

[DefaultExecutionOrder(-10000)] // sehr fr�h, noch vor den meisten Awakes
public class HubRegistryBootstrap : MonoBehaviour
{
    [Tooltip("Objekt �ber Szenenwechsel behalten.")]
    public bool dontDestroyOnLoad = true;

    void Awake()
    {
        Ensure();
        if (dontDestroyOnLoad) DontDestroyOnLoad(HubRegistry.Instance.gameObject);
    }

    /// <summary>Kann �berall aufgerufen werden, um eine vorhandene HubRegistry sicherzustellen.</summary>
    public static void Ensure()
    {
        if (HubRegistry.Instance != null) return;

        var go = new GameObject("HubRegistry");
        // Wichtig: Deine bestehende HubRegistry-Klasse muss ein MonoBehaviour sein,
        // das in Awake o.�. 'Instance = this' setzt.
        go.AddComponent<HubRegistry>();
    }
}
