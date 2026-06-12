using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Stellt ein einziges persistentes EventSystem bereit und entfernt Szenen-Duplikate,
/// bevor deren OnEnable die Unity-Warnung auslösen kann.
/// </summary>
[DefaultExecutionOrder(-32000)]
public class EventSystemCleaner : MonoBehaviour
{
    private static EventSystemCleaner _instance;
    private static EventSystem _primary;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AfterFirstSceneLoad()
    {
        EnsureSingleEventSystem();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureSingleEventSystem();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _instance = null;
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureSingleEventSystem();
    }

    /// <summary>
    /// Lädt eine Szene additiv und entfernt doppelte EventSystems vor der Aktivierung.
    /// </summary>
    public static async UniTask LoadSceneAdditiveAsync(string sceneName)
    {
        await LoadSceneAsyncPrepared(sceneName, LoadSceneMode.Additive);
    }

    /// <summary>
    /// Lädt eine Szene als Single-Load und entfernt doppelte EventSystems vor der Aktivierung.
    /// </summary>
    public static async UniTask LoadSceneSingleAsync(string sceneName)
    {
        await LoadSceneAsyncPrepared(sceneName, LoadSceneMode.Single);
    }

    private static async UniTask LoadSceneAsyncPrepared(string sceneName, LoadSceneMode mode)
    {
        var op = SceneManager.LoadSceneAsync(sceneName, mode);
        if (op == null)
            throw new InvalidOperationException($"Szene '{sceneName}' konnte nicht geladen werden.");

        op.allowSceneActivation = false;
        while (op.progress < 0.9f)
            await UniTask.Yield();

        var scene = FindLoadedScene(sceneName);
        if (scene.IsValid())
            PrepareSceneBeforeActivation(scene);

        op.allowSceneActivation = true;
        while (!op.isDone)
            await UniTask.Yield();

        EnsureSingleEventSystem();
    }

    private static Scene FindLoadedScene(string sceneName)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid() && scene.isLoaded)
            return scene;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var candidate = SceneManager.GetSceneAt(i);
            if (candidate.IsValid() && candidate.isLoaded &&
                string.Equals(candidate.name, sceneName, StringComparison.Ordinal))
                return candidate;
        }

        return default;
    }

    /// <summary>
    /// Entfernt EventSystem-Duplikate aus einer Szene (auch vor scene activation).
    /// </summary>
    public static void PrepareSceneBeforeActivation(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        EnsurePrimaryExists();

        var cleanersToDestroy = new List<GameObject>();
        var eventSystems = new List<EventSystem>();

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root == null)
                continue;

            foreach (var cleaner in root.GetComponentsInChildren<EventSystemCleaner>(true))
            {
                if (cleaner == null || cleaner == _instance)
                    continue;

                var cleanerObject = cleaner.gameObject;
                if (cleanerObject != null)
                    cleanersToDestroy.Add(cleanerObject);
            }

            foreach (var eventSystem in root.GetComponentsInChildren<EventSystem>(true))
            {
                if (eventSystem != null)
                    eventSystems.Add(eventSystem);
            }
        }

        foreach (var cleanerObject in cleanersToDestroy)
        {
            if (cleanerObject == null)
                continue;

            DestroyObject(cleanerObject, immediate: true);
        }

        foreach (var eventSystem in eventSystems)
        {
            if (eventSystem == null)
                continue;

            var eventSystemObject = eventSystem.gameObject;
            if (eventSystemObject == null)
                continue;

            EnsureGuard(eventSystemObject);

            if (_primary == null)
            {
                AdoptPrimary(eventSystem);
                continue;
            }

            if (eventSystem == _primary)
                continue;

            DestroyEventSystem(eventSystemObject, immediate: true);
        }
    }

    public static void EnsureSingleEventSystem()
    {
        EnsurePrimaryExists();

        var all = FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
        foreach (var eventSystem in all)
        {
            if (eventSystem == null)
                continue;

            EnsureGuard(eventSystem.gameObject);

            if (_primary == null)
            {
                AdoptPrimary(eventSystem);
                continue;
            }

            if (eventSystem == _primary)
                continue;

            DestroyEventSystem(eventSystem.gameObject, immediate: false);
        }
    }

    private static void EnsurePrimaryExists()
    {
        if (_primary != null && _primary)
            return;

        _primary = null;
        EventSystemDuplicateGuard.ResetClaim();

        var all = FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
        if (all.Length == 0)
        {
            _primary = CreateGlobalEventSystem();
            return;
        }

        _primary = SelectPrimary(all);
        if (_primary != null)
            AdoptPrimary(_primary);
    }

    private static void AdoptPrimary(EventSystem eventSystem)
    {
        if (eventSystem == null)
            return;

        _primary = eventSystem;
        EventSystemDuplicateGuard.RegisterPrimary(eventSystem);
        EnsureGuard(eventSystem.gameObject);

        if (eventSystem.gameObject.scene.name != "DontDestroyOnLoad")
            DontDestroyOnLoad(eventSystem.gameObject);
    }

    private static EventSystem SelectPrimary(EventSystem[] all)
    {
        foreach (var eventSystem in all)
        {
            if (eventSystem != null && eventSystem.gameObject.scene.name == "DontDestroyOnLoad")
                return eventSystem;
        }

        foreach (var eventSystem in all)
        {
            if (eventSystem != null &&
                eventSystem.gameObject.scene.name.StartsWith("00_", StringComparison.OrdinalIgnoreCase))
                return eventSystem;
        }

        return all.Length > 0 ? all[0] : null;
    }

    private static EventSystem CreateGlobalEventSystem()
    {
        var go = new GameObject("EventSystem");
        go.SetActive(false);

        var eventSystem = go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif

        EventSystemDuplicateGuard.RegisterPrimary(eventSystem);
        EnsureGuard(go);
        DontDestroyOnLoad(go);
        go.SetActive(true);
        return eventSystem;
    }

    private static void EnsureGuard(GameObject eventSystemObject)
    {
        if (eventSystemObject == null)
            return;

        if (eventSystemObject.GetComponent<EventSystemDuplicateGuard>() == null)
            eventSystemObject.AddComponent<EventSystemDuplicateGuard>();
    }

    private static void DestroyEventSystem(GameObject target, bool immediate)
    {
        if (target == null)
            return;

        var eventSystem = target.GetComponent<EventSystem>();
        if (eventSystem != null)
            eventSystem.enabled = false;

        target.SetActive(false);
        DestroyObject(target, immediate);
    }

    private static void DestroyObject(GameObject target, bool immediate)
    {
        if (target == null)
            return;

        if (immediate || !Application.isPlaying)
            UnityEngine.Object.DestroyImmediate(target);
        else
            UnityEngine.Object.Destroy(target);
    }
}
