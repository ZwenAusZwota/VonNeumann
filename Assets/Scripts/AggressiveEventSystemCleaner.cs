using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// AGGRESSIVE EventSystem-Bereinigung - läuft kontinuierlich und deaktiviert sofort
/// </summary>
public class AggressiveEventSystemCleaner : MonoBehaviour
{
    private void Start()
    {
        // Sofortige Bereinigung beim Start
        CleanupEventSystems();
        
        // Kontinuierliche Überwachung alle 0.5 Sekunden
        InvokeRepeating(nameof(CleanupEventSystems), 0.5f, 0.5f);
    }

    private void CleanupEventSystems()
    {
        EventSystem[] allEventSystems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        
        if (allEventSystems.Length <= 1) return;
        
        Debug.LogWarning($"[AggressiveEventSystemCleaner] {allEventSystems.Length} EventSystems - SOFORTIGE BEREINIGUNG!");
        
        // Finde das erste AKTIVE EventSystem
        EventSystem primaryEventSystem = null;
        
        foreach (var eventSystem in allEventSystems)
        {
            if (eventSystem.gameObject.activeInHierarchy && eventSystem.enabled)
            {
                primaryEventSystem = eventSystem;
                break;
            }
        }
        
        // Falls keines aktiv ist, aktiviere das erste
        if (primaryEventSystem == null)
        {
            primaryEventSystem = allEventSystems[0];
            primaryEventSystem.gameObject.SetActive(true);
            primaryEventSystem.enabled = true;
        }
        
        // DEAKTIVIERE ALLE ANDEREN SOFORT UND AGGRESSIV
        foreach (var eventSystem in allEventSystems)
        {
            if (eventSystem != primaryEventSystem)
            {
                // Mehrfache Deaktivierung für Sicherheit
                eventSystem.enabled = false;
                eventSystem.gameObject.SetActive(false);
                
                // Zusätzlich: InputModule deaktivieren falls vorhanden
                var inputModule = eventSystem.GetComponent<StandaloneInputModule>();
                if (inputModule != null)
                {
                    inputModule.enabled = false;
                }
                
                Debug.Log($"[AggressiveEventSystemCleaner] DEAKTIVIERT: {eventSystem.name}");
            }
        }
        
        Debug.Log($"[AggressiveEventSystemCleaner] AKTIV: {primaryEventSystem.name}");
    }

    // Manuelle Bereinigung über Context-Menü
    [ContextMenu("Force Cleanup EventSystems")]
    public void ForceCleanup()
    {
        CleanupEventSystems();
    }
}
