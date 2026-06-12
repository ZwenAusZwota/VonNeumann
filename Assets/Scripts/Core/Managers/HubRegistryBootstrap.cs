// Assets/Scripts/Bootstrap/HubRegistryBootstrap.cs
using UnityEngine;

[DefaultExecutionOrder(-10000)] // sehr frh, noch vor den meisten Awakes
public class HubRegistryBootstrap : MonoBehaviour
{
    [Tooltip("Objektber Szenenwechsel behalten.")]
    public bool dontDestroyOnLoad = true;

    void Awake()
    {
        Ensure();
        var hubRegistry = ServiceContainer.Instance?.Get<HubRegistry>();
        if (dontDestroyOnLoad && hubRegistry != null) 
            DontDestroyOnLoad(hubRegistry.gameObject);
    }

    /// <summary>Kannberall aufgerufen werden, um eine vorhandene HubRegistry sicherzustellen.</summary>
    public static void Ensure()
    {
        // Versuche Service Container zu nutzen
        var existingFromContainer = ServiceContainer.Instance?.Get<HubRegistry>();
        if (existingFromContainer != null) return;

        // Fallback: Suche nach vorhandenem HubRegistry
        var existing = FindAnyObjectByType<HubRegistry>();
        if (existing != null) return;

        // Erstelle neue HubRegistry
        var go = new GameObject("HubRegistry");
        go.AddComponent<HubRegistry>();
    }
}
