using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Kontinuierliche EventSystem-Überwachung und sofortige Bereinigung
/// </summary>
public class EventSystemMonitor : MonoBehaviour
{
    private float checkInterval = 1f; // Alle 1 Sekunde prüfen
    private float lastCheckTime = 0f;

    private void Update()
    {
        if (Time.time - lastCheckTime >= checkInterval)
        {
            CheckAndCleanupEventSystems();
            lastCheckTime = Time.time;
        }
    }

    private void CheckAndCleanupEventSystems()
    {
        EventSystem[] allEventSystems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        
        if (allEventSystems.Length <= 1) return;
        
        Debug.LogWarning($"[EventSystemMonitor] {allEventSystems.Length} EventSystems gefunden - sofortige Bereinigung");
        
        // Finde das primäre EventSystem (das erste aktive)
        EventSystem primaryEventSystem = null;
        
        foreach (var eventSystem in allEventSystems)
        {
            if (eventSystem.gameObject.activeInHierarchy)
            {
                primaryEventSystem = eventSystem;
                break;
            }
        }
        
        // Falls kein aktives gefunden wurde, nimm das erste
        if (primaryEventSystem == null)
        {
            primaryEventSystem = allEventSystems[0];
            primaryEventSystem.gameObject.SetActive(true);
        }
        
        // Deaktiviere alle anderen sofort
        foreach (var eventSystem in allEventSystems)
        {
            if (eventSystem != primaryEventSystem)
            {
                eventSystem.gameObject.SetActive(false);
                Debug.Log($"[EventSystemMonitor] EventSystem deaktiviert: {eventSystem.name}");
            }
        }
    }

    private void OnEnable()
    {
        // Sofortige Prüfung beim Aktivieren
        CheckAndCleanupEventSystems();
    }
}
