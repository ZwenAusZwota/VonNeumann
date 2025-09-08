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
    Management
}

/// <summary>
/// Zentraler Szenenrouter für additiven Flow:
/// Bootstrap (persistent) -> Splash -> MainMenu -> (Loading -> Game + GameUI)
/// Während des Spiels: Pause/Management additiv ODER als Single-Set laden.
/// </summary>
public class SceneRouter : MonoBehaviour
{
    public static SceneRouter I { get; private set; }

    [Header("Erkennung von Bootstrap-Szenen")]
    [Tooltip("Alle Szenen, deren Name mit diesem Prefix beginnt, werden beim Wechsel NICHT entladen.")]
    [SerializeField] private string bootstrapPrefix = "00_";

    [Header("Optional: automatischer Start")]
    [Tooltip("Wenn nur Bootstrap geladen ist, automatisch ins MainMenu wechseln.")]
    [SerializeField] private bool autoGoToMainMenuOnStart = false;

    /// <summary>Verhindert Doppel-Loads.</summary>
    public bool IsBusy { get; private set; }

    // Events (optional abonnierbar)
    public event Action<AppScene[]> OnBeforeLoadSet;
    public event Action<AppScene[]> OnAfterLoadSet;

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
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
    // Öffentliche API
    // ------------------------------------------------------------------------

    public UniTask ToSplash() => LoadSet(new[] { AppScene.Splash });
    public UniTask ToMainMenu() => LoadSet(new[] { AppScene.MainMenu });
    public UniTask ToNewGame() => LoadSet(new[] { AppScene.Loading });   // Loader kümmert sich danach um 10_Game + 10_Game_UI
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
    /// Pausiert das Spiel und lädt ein gewünschtes Overlay (Pause oder Management)
    /// als Single-Set. Dabei werden 10_Game / 10_Game_UI zuverlässig entladen.
    /// </summary>
    public async UniTask ToOverlaySingle(AppScene overlay)
    {
        // Sicherheit: Nur erlaubte Overlays
        if (overlay != AppScene.Pause && overlay != AppScene.Management)
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

            // 1) Alle nicht-Bootstrap-Szenen entladen
            var toUnload = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                if (s.name.StartsWith(bootstrapPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                toUnload.Add(s.name);
            }
            foreach (var name in toUnload)
            {
                var op = SceneManager.UnloadSceneAsync(name);
                if (op != null) await op.ToUniTask();
            }

            // 2) Gewünschte Szenen in Reihenfolge laden (additiv)
            foreach (var sc in set)
            {
                string name = SceneName(sc);
                var scene = SceneManager.GetSceneByName(name);
                if (!scene.isLoaded)
                {
                    var op = SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
                    await op.ToUniTask();
                }
            }

            // 3) Aktive Szene auf die letzte setzen
            string activeName = SceneName(set[^1]);
            var target = SceneManager.GetSceneByName(activeName);
            if (target.IsValid())
                SceneManager.SetActiveScene(target);

            OnAfterLoadSet?.Invoke(set);
        }
        finally
        {
            IsBusy = false;
        }
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
                    await SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive).ToUniTask();
                }
                finally { IsBusy = false; }
            }
        }
        else
        {
            if (sc.isLoaded)
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
        AppScene.GameUI => "10_Game_UI",
        AppScene.Pause => "11_PauseOptions",
        AppScene.Management => "12_Management",
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
