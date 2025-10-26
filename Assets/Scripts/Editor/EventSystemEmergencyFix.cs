using UnityEngine;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor-Tool für sofortige EventSystem-Bereinigung
/// </summary>
public class EventSystemEmergencyFix : EditorWindow
{
    [MenuItem("Tools/Emergency EventSystem Fix")]
    public static void ShowWindow()
    {
        GetWindow<EventSystemEmergencyFix>("EventSystem Emergency Fix");
    }

    private void OnGUI()
    {
        GUILayout.Label("EventSystem Emergency Fix", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        EventSystem[] allEventSystems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        
        GUILayout.Label($"Gefundene EventSystems: {allEventSystems.Length}");
        
        if (allEventSystems.Length > 1)
        {
            GUILayout.Space(10);
            GUILayout.Label("⚠️ KONFLIKT ERKANNT!", EditorStyles.boldLabel);
            
            GUILayout.Space(10);
            if (GUILayout.Button("SOFORT BEREINIGEN", GUILayout.Height(30)))
            {
                FixEventSystems();
            }
            
            GUILayout.Space(10);
            GUILayout.Label("EventSystems in der Szene:");
            
            foreach (var eventSystem in allEventSystems)
            {
                EditorGUILayout.BeginHorizontal();
                
                bool isActive = eventSystem.gameObject.activeInHierarchy;
                EditorGUILayout.LabelField($"{eventSystem.name} - {(isActive ? "AKTIV" : "INAKTIV")}");
                
                if (GUILayout.Button("Deaktivieren", GUILayout.Width(100)))
                {
                    eventSystem.gameObject.SetActive(false);
                    EditorUtility.SetDirty(eventSystem.gameObject);
                }
                
                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            GUILayout.Space(10);
            GUILayout.Label("✅ Kein Konflikt - nur ein EventSystem gefunden");
        }
        
        GUILayout.Space(20);
        if (GUILayout.Button("Szene neu laden"))
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }

    private void FixEventSystems()
    {
        EventSystem[] allEventSystems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        
        if (allEventSystems.Length <= 1) return;
        
        Debug.LogWarning($"[EventSystemEmergencyFix] Bereinige {allEventSystems.Length} EventSystems");
        
        // Behalte das erste EventSystem
        EventSystem primaryEventSystem = allEventSystems[0];
        
        // Deaktiviere alle anderen
        for (int i = 1; i < allEventSystems.Length; i++)
        {
            EventSystem duplicateEventSystem = allEventSystems[i];
            
            duplicateEventSystem.enabled = false;
            duplicateEventSystem.gameObject.SetActive(false);
            
            EditorUtility.SetDirty(duplicateEventSystem.gameObject);
            
            Debug.Log($"[EventSystemEmergencyFix] Deaktiviert: {duplicateEventSystem.name}");
        }
        
        // Stelle sicher, dass das primäre aktiv ist
        primaryEventSystem.gameObject.SetActive(true);
        primaryEventSystem.enabled = true;
        EditorUtility.SetDirty(primaryEventSystem.gameObject);
        
        Debug.Log($"[EventSystemEmergencyFix] Primäres EventSystem: {primaryEventSystem.name}");
        
        // Szene als geändert markieren
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        
        Debug.Log("[EventSystemEmergencyFix] Bereinigung abgeschlossen!");
    }
}
