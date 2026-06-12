using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// SOFORTIGE EventSystem-Bereinigung - deaktiviert alle bis auf das erste
/// </summary>
public class ImmediateEventSystemFix : MonoBehaviour
{
    private void Awake()
    {
        FixEventSystems();
    }

    private void Start()
    {
        FixEventSystems();
    }

    private void FixEventSystems()
    {
        EventSystem[] allEventSystems = FindObjectsByType<EventSystem>();
        
        Debug.Log($"[ImmediateEventSystemFix] Gefunden: {allEventSystems.Length} EventSystems");
        
        if (allEventSystems.Length <= 1) return;
        
        Debug.LogWarning($"[ImmediateEventSystemFix] KONFLIKT! Deaktiviere alle bis auf das erste EventSystem");
        
        // Behalte nur das ERSTE EventSystem
        EventSystem primaryEventSystem = allEventSystems[0];
        
        // Deaktiviere ALLE anderen sofort
        for (int i = 1; i < allEventSystems.Length; i++)
        {
            EventSystem duplicateEventSystem = allEventSystems[i];
            
            Debug.Log($"[ImmediateEventSystemFix] DEAKTIVIERE: {duplicateEventSystem.name}");
            duplicateEventSystem.gameObject.SetActive(false);
            
            // Zusätzlich: Komponente deaktivieren
            duplicateEventSystem.enabled = false;
        }
        
        // Stelle sicher, dass das primäre EventSystem aktiv ist
        primaryEventSystem.gameObject.SetActive(true);
        primaryEventSystem.enabled = true;
        
        Debug.Log($"[ImmediateEventSystemFix] PRIMÄRES EventSystem: {primaryEventSystem.name}");
    }
}
