using UnityEngine;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-Script, das automatisch EventSystemCleaner zu Szenen mit EventSystems hinzufügt
/// </summary>
[InitializeOnLoad]
public static class EventSystemCleanerSetup
{
    static EventSystemCleanerSetup()
    {
        // Registriere Callback für Szenenwechsel
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        // Prüfe, ob die Szene EventSystems hat
        EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        
        if (eventSystems.Length > 0)
        {
            // Prüfe, ob bereits ein EventSystemCleaner existiert
            EventSystemCleaner existingCleaner = Object.FindFirstObjectByType<EventSystemCleaner>();
            
            if (existingCleaner == null)
            {
                // Erstelle einen EventSystemCleaner
                GameObject cleanerGO = new GameObject("EventSystemCleaner");
                cleanerGO.AddComponent<EventSystemCleaner>();
                
                // Markiere die Szene als geändert
                EditorSceneManager.MarkSceneDirty(scene);
                
                Debug.Log($"[EventSystemCleanerSetup] EventSystemCleaner zu Szene '{scene.name}' hinzugefügt");
            }
        }
    }

    [MenuItem("Tools/Add EventSystemCleaner to All Scenes")]
    public static void AddEventSystemCleanerToAllScenes()
    {
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
        
        foreach (string guid in sceneGuids)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            
            EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            
            if (eventSystems.Length > 0)
            {
                EventSystemCleaner existingCleaner = Object.FindFirstObjectByType<EventSystemCleaner>();
                
                if (existingCleaner == null)
                {
                    GameObject cleanerGO = new GameObject("EventSystemCleaner");
                    cleanerGO.AddComponent<EventSystemCleaner>();
                    EditorSceneManager.MarkSceneDirty(scene);
                    
                    Debug.Log($"[EventSystemCleanerSetup] EventSystemCleaner zu Szene '{scene.name}' hinzugefügt");
                }
            }
            
            EditorSceneManager.CloseScene(scene, true);
        }
        
        Debug.Log("[EventSystemCleanerSetup] EventSystemCleaner zu allen Szenen mit EventSystems hinzugefügt");
    }
}
