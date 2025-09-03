// Assets/Scripts/00_Manager/SceneRouter.cs
using System;
using System.Collections.Generic;
using System.Linq;
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
/// Während des Spiels: Pause/Research additiv ein-/ausblenden.
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

        // Prüfen, ob außer Bootstrap nichts geladen ist
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
            // Let a frame pass to ensure bootstrap objects are initialized
            await UniTask.Yield();
            await ToMainMenu();
        }
    }

    // ------------------------------------------------------------------------
    // Öffentliche API
    // ------------------------------------------------------------------------

    public UniTask ToSplash() => LoadSet(new[] { AppScene.Splash });
    public UniTask ToMainMenu() => LoadSet(new[] { AppScene.MainMenu });
    //public UniTask ToNewGame() => LoadSet(new[] { AppScene.Loading, AppScene.Game, AppScene.GameUI });
    //public UniTask ToLoadGame() => LoadSet(new[] { AppScene.Loading, AppScene.Game, AppScene.GameUI });

    // SceneRouter.cs
    public UniTask ToNewGame() => LoadSet(new[] { AppScene.Loading });
    public UniTask ToLoadGame() => LoadSet(new[] { AppScene.Loading });


    /// <summary>Pausen-/Optionsszene additiv ein-/ausblenden.</summary>
    public UniTask TogglePause(bool on) => ToggleScene(AppScene.Pause, on);

    /// <summary>Management additiv ein-/ausblenden.</summary>
    public UniTask ToggleManagement(bool on) => ToggleScene(AppScene.Management, on);

    /// <summary>
    /// Lädt ein Set von Szenen additiv in Reihenfolge, entlädt alle Nicht-Bootstrap-Szenen vorher.
    /// Die aktive Szene wird auf das letzte Element gesetzt.
    /// </summary>
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

    // ------------------------------------------------------------------------
    // Hilfsfunktionen
    // ------------------------------------------------------------------------

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

    // SceneRouter.cs – Ergänzung in der Klasse SceneRouter
    public async UniTask ToPauseSingle(bool adoptCurrentCamera = true)
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            // Events informieren (optional)
            OnBeforeLoadSet?.Invoke(new[] { AppScene.Pause });

            // Zeit anhalten (Physik/FixedUpdate stoppen)
            Time.timeScale = 0f;

            // Aktuelle Kamera sichern und überlebensfähig machen
            Camera cam = null;
            if (adoptCurrentCamera && Camera.main != null)
            {
                cam = Camera.main;
                DontDestroyOnLoad(cam.gameObject);
            }

            // Pausen-Szene als einzelne Szene laden (Single!)
            string pauseName = SceneName(AppScene.Pause);
            await SceneManager.LoadSceneAsync(pauseName, LoadSceneMode.Single).ToUniTask();

            // Kamera in die neu geladene Szene „verschieben“
            if (cam != null)
            {
                var pauseScene = SceneManager.GetSceneByName(pauseName);
                if (pauseScene.IsValid())
                    SceneManager.MoveGameObjectToScene(cam.gameObject, pauseScene);
            }

            // aktive Szene setzen
            var target = SceneManager.GetSceneByName(pauseName);
            if (target.IsValid())
                SceneManager.SetActiveScene(target);

            OnAfterLoadSet?.Invoke(new[] { AppScene.Pause });
        }
        finally
        {
            IsBusy = false;
        }
    }


    // Debug-Hilfen im Editor
#if UNITY_EDITOR
    [ContextMenu("Editor: To MainMenu")]
    private void EditorToMainMenu() => ToMainMenu().Forget();

    [ContextMenu("Editor: New Game")]
    private void EditorToNewGame() => ToNewGame().Forget();

    [ContextMenu("Editor: Load Game")]
    private void EditorToLoadGame() => ToLoadGame().Forget();

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
