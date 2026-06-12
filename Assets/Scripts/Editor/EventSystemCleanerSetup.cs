using UnityEngine;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Entfernt Szenen-EventSystems (ein globales wird zur Laufzeit erzeugt) und hält Guards als Fallback.
/// </summary>
[InitializeOnLoad]
public static class EventSystemCleanerSetup
{
    private const string BootstrapSceneName = "00_Bootstrap";

    static EventSystemCleanerSetup()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EnsureCleanerInBootstrap(scene);
    }

    [MenuItem("Tools/Fix EventSystems In All Scenes")]
    public static void FixEventSystemsInAllScenes()
    {
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
        int removed = 0;
        int cleanersAdded = 0;

        foreach (string guid in sceneGuids)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            if (EnsureCleanerInBootstrap(scene))
                cleanersAdded++;

            removed += RemoveSceneEventSystems(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log($"[EventSystemCleanerSetup] {removed} EventSystem(s) entfernt, {cleanersAdded} Cleaner in Bootstrap ergänzt.");
    }

    private static bool EnsureCleanerInBootstrap(Scene scene)
    {
        if (!scene.IsValid() || !scene.name.StartsWith("00_", System.StringComparison.OrdinalIgnoreCase))
            return false;

        if (Object.FindAnyObjectByType<EventSystemCleaner>() != null)
            return false;

        var cleanerGo = new GameObject("EventSystemCleaner");
        cleanerGo.AddComponent<EventSystemCleaner>();
        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    private static int RemoveSceneEventSystems(Scene scene)
    {
        if (!scene.IsValid())
            return 0;

        var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
        int removed = 0;

        foreach (var eventSystem in eventSystems)
        {
            if (eventSystem == null)
                continue;

            Object.DestroyImmediate(eventSystem.gameObject);
            removed++;
        }

        if (removed > 0)
            EditorSceneManager.MarkSceneDirty(scene);

        return removed;
    }
}
