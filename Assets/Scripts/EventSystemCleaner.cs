using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Sofortige EventSystem-Bereinigung - läuft bei jedem Start
/// </summary>
public class EventSystemCleaner : MonoBehaviour
{
    private void Start()
    {
        CleanupEventSystems();
    }

    private void CleanupEventSystems()
    {
        EventSystem[] allEventSystems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        
        if (allEventSystems.Length <= 1)
        {
            Debug.Log($"[EventSystemCleaner] {allEventSystems.Length} EventSystem(s) gefunden - kein Konflikt");
            return;
        }
        
        Debug.LogWarning($"[EventSystemCleaner] {allEventSystems.Length} EventSystems gefunden - bereinige Duplikate");
        
        // Behalte nur das erste EventSystem und deaktiviere alle anderen
        EventSystem primaryEventSystem = allEventSystems[0];
        
        for (int i = 1; i < allEventSystems.Length; i++)
        {
            EventSystem duplicateEventSystem = allEventSystems[i];
            
            Debug.Log($"[EventSystemCleaner] Deaktiviere EventSystem: {duplicateEventSystem.name} in Szene {duplicateEventSystem.gameObject.scene.name}");
            duplicateEventSystem.gameObject.SetActive(false);
        }
        
        Debug.Log($"[EventSystemCleaner] Primäres EventSystem: {primaryEventSystem.name} in Szene {primaryEventSystem.gameObject.scene.name}");
    }
}
