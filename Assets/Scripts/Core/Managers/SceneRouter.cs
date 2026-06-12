// Assets/Scripts/00_Manager/SceneRouter.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
public enum AppScene
{
    Bootstrap,
    Splash,
    MainMenu,
    Loading,
    Game,
    GameUI,
    Pause,
    Management,
    Science,
    Fabricator
}

/// <summary>
/// Zentraler Szenenrouter fr additiven Flow:
/// Bootstrap (persistent) -> Splash -> MainMenu -> (Loading -> Game + GameUI)
/// Whrend des Spiels: Pause/Management additiv ODER als Single-Set laden.
/// </summary>
public class SceneRouter : MonoBehaviour
{
    private static SceneRouter _instance;

    public static SceneRouter I
    {
        get
        {
            if (_instance != null)
                return _instance;

            return CreateFallbackInstance();
        }
        private set => _instance = value;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _instance = null;

    [Header("Erkennung von Bootstrap-Szenen")]
    [Tooltip("Alle Szenen, deren Name mit diesem Prefix beginnt, werden beim Wechsel NICHT entladen.")]
    [SerializeField] private string bootstrapPrefix = "00_";

    [Header("Optional: automatischer Start")]
    [Tooltip("Wenn nur Bootstrap geladen ist, automatisch ins MainMenu wechseln.")]
    [SerializeField] private bool autoGoToMainMenuOnStart = false;

    //[Header("Additive UI-Politik")]
    //[Tooltip("Wenn ein Set die Game-Szene ldt, wird die UI-Szene behalten (falls geladen) bzw. nachgeladen (falls nicht).")]
    //[SerializeField] private bool keepGameUIWithGame = true;

    /// <summary>Verhindert Doppel-Loads.</summary>
    public bool IsBusy { get; private set; }

    // Events (optional abonnierbar)
    public event Action<AppScene[]> OnBeforeLoadSet;
    public event Action<AppScene[]> OnAfterLoadSet;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            // Laufzeit-Fallback (nur SceneRouter-GO) weicht dem Bootstrap-AppRoot.
            if (IsRuntimeFallback(_instance) && !IsRuntimeFallback(this))
            {
                Destroy(_instance.gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private static bool IsRuntimeFallback(SceneRouter router)
    {
        return router != null && router.gameObject.name == nameof(SceneRouter);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    /// <summary>
    /// Liefert den Singleton-SceneRouter (erzeugt bei Bedarf einen persistenten Fallback).
    /// </summary>
    public static SceneRouter EnsureInstance() => I;

    private static SceneRouter CreateFallbackInstance()
    {
        var existing = UnityEngine.Object.FindAnyObjectByType<SceneRouter>(FindObjectsInactive.Include);
        if (existing != null)
        {
            _instance = existing;
            if (existing.gameObject.scene.name != "DontDestroyOnLoad")
                DontDestroyOnLoad(existing.gameObject);
            return existing;
        }

        var go = new GameObject(nameof(SceneRouter));
        return go.AddComponent<SceneRouter>();
    }

    private async void Start()
    {
        if (!autoGoToMainMenuOnStart) return;

        bool hasNonBootstrapLoaded = false;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded) continue;
            if (!s.name.StartsWith(bootstrapPrefix, StringComparison.OrdinalIgnoreCase))
            {
                hasNonBootstrapLoaded = true;
                break;
            }
        }

        if (!hasNonBootstrapLoaded)
        {
            await UniTask.Yield();
            await ToMainMenu();
        }
    }

    // ------------------------------------------------------------------------
    // ffentliche API
    // ------------------------------------------------------------------------

    public UniTask ToSplash() => LoadSet(new[] { AppScene.Splash });
    public UniTask ToMainMenu() => LoadSet(new[] { AppScene.MainMenu });
    public UniTask ToNewGame() => LoadSet(new[] { AppScene.Loading });   // Loader kmmert sich danach um 10_Game + 10_Game_UI
    public UniTask ToLoadGame() => LoadSet(new[] { AppScene.Loading });

    /// <summary>Additiv: Pausen-/Optionsszene ein-/ausblenden.</summary>
    public UniTask TogglePause(bool on) => ToggleScene(AppScene.Pause, on);
    /// <summary>Additiv: Management ein-/ausblenden.</summary>
    public UniTask ToggleManagement(bool on) => ToggleScene(AppScene.Management, on);

    /// <summary>
    /// Nur pausieren (keine Szene laden/entladen).
    /// </summary>
    public UniTask ToPauseSingle(bool adoptCurrentCamera = true)
    {
        Time.timeScale = 0f;
        return UniTask.CompletedTask;
    }

    /// <summary>
    /// Overlay als Single-Set laden. Achtung:
    /// - Fr Pause ? Time.timeScale = 0 (echter Freeze).
    /// - Fr Management ? Time.timeScale = 1 (kein Freeze; Produktion luft weiter).
    /// </summary>
    public async UniTask ToOverlaySingle(AppScene overlay)
    {
        if (overlay == AppScene.Management || overlay == AppScene.Fabricator)
        {
            Time.timeScale = 1f;
            await LoadOverlayWithGameWorld(overlay);
            return;
        }

        if (overlay != AppScene.Pause && overlay != AppScene.Science)
        {
            Debug.LogWarning($"[SceneRouter] ToOverlaySingle: '{overlay}' ist kein Overlay. Abgebrochen.");
            return;
        }

        Time.timeScale = 0f;
        await LoadSet(new[] { overlay });
    }

    // Komfort-Wrapper
    public UniTask ToPauseOverlaySingle() => ToOverlaySingle(AppScene.Pause);
    public UniTask ToManagementOverlaySingle() => ToOverlaySingle(AppScene.Management);
    public UniTask ToScienceOverlaySingle() => ToOverlaySingle(AppScene.Science);
    public UniTask ToFabricatorOverlaySingle() => ToOverlaySingle(AppScene.Fabricator);

    /// <summary>
    /// Lädt Management/Fabrikator additiv, hält 10_Game + 10_Game_UI geladen.
    /// </summary>
    private async UniTask LoadOverlayWithGameWorld(AppScene overlay)
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            var set = new[] { AppScene.Game, AppScene.GameUI, overlay };
            OnBeforeLoadSet?.Invoke(set);

            foreach (var sc in new[] { AppScene.Game, AppScene.GameUI })
            {
                string name = SceneName(sc);
                var scene = SceneManager.GetSceneByName(name);
                if (!scene.isLoaded)
                    await EventSystemCleaner.LoadSceneAdditiveAsync(name);
            }

            string overlayName = SceneName(overlay);
            if (!SceneManager.GetSceneByName(overlayName).isLoaded)
                await EventSystemCleaner.LoadSceneAdditiveAsync(overlayName);

            foreach (var other in new[] { AppScene.Pause, AppScene.Management, AppScene.Science, AppScene.Fabricator })
            {
                if (other == overlay) continue;
                string otherName = SceneName(other);
                var otherScene = SceneManager.GetSceneByName(otherName);
                if (otherScene.IsValid() && otherScene.isLoaded && CanUnloadScene(otherName))
                    await SceneManager.UnloadSceneAsync(otherName).ToUniTask();
            }

            var target = SceneManager.GetSceneByName(overlayName);
            if (target.IsValid())
                SceneManager.SetActiveScene(target);

            OnAfterLoadSet?.Invoke(set);
            EventSystemCleaner.EnsureSingleEventSystem();
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ------------------------------------------------------------------------
    // Interne Helpers
    // ------------------------------------------------------------------------

    public async UniTask LoadSet(AppScene[] set)
    {
        if (IsBusy || set == null || set.Length == 0) return;
        IsBusy = true;

        try
        {
            OnBeforeLoadSet?.Invoke(set);

            var setSceneNames = new HashSet<string>();
            foreach (var sc in set)
                setSceneNames.Add(SceneName(sc));

            // 1) Ziel-Szenen ZUERST laden  sonst wrde beim Entladen der letzten Spielszene
            //    (z. B. nur 10_Game + 10_Game_UI geladen) Unity einen Fehler werfen.
            foreach (var sc in set)
            {
                string name = SceneName(sc);
                var scene = SceneManager.GetSceneByName(name);
                if (!scene.isLoaded)
                    await EventSystemCleaner.LoadSceneAdditiveAsync(name);
            }

            // 2) Nicht bentigte Szenen entladen (Bootstrap und Ziel-Set bleiben)
            var toUnload = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                if (s.name.StartsWith(bootstrapPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (setSceneNames.Contains(s.name)) continue;

                toUnload.Add(s.name);
            }

            foreach (var name in toUnload)
            {
                if (!CanUnloadScene(name))
                {
                    Debug.LogWarning($"[SceneRouter] berspringe Entladen von '{name}'  mindestens eine Szene muss geladen bleiben.");
                    continue;
                }

                var op = SceneManager.UnloadSceneAsync(name);
                if (op != null) await op.ToUniTask();
            }

            // 2b) Falls Game geladen wird, UI aber nicht im Set war: sicher additiv anfgen
            //if (keepGameUIWithGame && setContainsGame && !setContainsGameUI)
            //{
            //    var uiScene = SceneManager.GetSceneByName(uiSceneName);
            //    if (!uiScene.IsValid() || !uiScene.isLoaded)
            //    {
            //        var op = SceneManager.LoadSceneAsync(uiSceneName, LoadSceneMode.Additive);
            //        await op.ToUniTask();
            //    }
            //}

            // 3) Aktive Szene auf die letzte setzen (Game bleibt ActiveScene, UI bleibt additiv)
            string activeName = SceneName(set[^1]);
            var target = SceneManager.GetSceneByName(activeName);
            if (target.IsValid())
                SceneManager.SetActiveScene(target);

            OnAfterLoadSet?.Invoke(set);
            
            EventSystemCleaner.EnsureSingleEventSystem();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static bool CanUnloadScene(string sceneName)
    {
        int loadedCount = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isLoaded)
                loadedCount++;
        }

        if (loadedCount <= 1)
            return false;

        var target = SceneManager.GetSceneByName(sceneName);
        return target.IsValid() && target.isLoaded;
    }

    private async UniTask ToggleScene(AppScene scene, bool on)
    {
        if (IsBusy) return;

        string name = SceneName(scene);
        var sc = SceneManager.GetSceneByName(name);

        if (on)
        {
            if (!sc.isLoaded)
            {
                IsBusy = true;
                try
                {
                    await EventSystemCleaner.LoadSceneAdditiveAsync(name);
                }
                finally { IsBusy = false; }
            }
        }
        else
        {
            if (sc.isLoaded && CanUnloadScene(name))
            {
                IsBusy = true;
                try
                {
                    await SceneManager.UnloadSceneAsync(name).ToUniTask();
                }
                finally { IsBusy = false; }
            }
        }
    }

    private static string SceneName(AppScene sc) => sc switch
    {
        AppScene.Bootstrap => "00_Bootstrap",
        AppScene.Splash => "01_Splash",
        AppScene.MainMenu => "02_MainMenu",
        AppScene.Loading => "03_Loading",
        AppScene.Game => "10_Game",
        AppScene.GameUI => "10_Game_UI",   // <- konsistenter Name mit Unterstrich
        AppScene.Pause => "11_PauseOptions",
        AppScene.Management => "12_Management",
        AppScene.Science => "13_ScienceTree",
        AppScene.Fabricator => "14_Fabricator",
        _ => string.Empty
    };

#if UNITY_EDITOR
    [ContextMenu("Editor: To MainMenu")]   private void EditorToMainMenu()  => ToMainMenu().Forget();
    [ContextMenu("Editor: New Game")]      private void EditorToNewGame()   => ToNewGame().Forget();
    [ContextMenu("Editor: Load Game")]     private void EditorToLoadGame()  => ToLoadGame().Forget();

    [ContextMenu("Editor: Toggle Pause")]
    private void EditorTogglePause()
    {
        string name = SceneName(AppScene.Pause);
        var sc = SceneManager.GetSceneByName(name);
        TogglePause(!sc.isLoaded).Forget();
    }

    [ContextMenu("Editor: Toggle Management")]
    private void EditorToggleManagement()
    {
        string name = SceneName(AppScene.Management);
        var sc = SceneManager.GetSceneByName(name);
        ToggleManagement(!sc.isLoaded).Forget();
    }
#endif
}
