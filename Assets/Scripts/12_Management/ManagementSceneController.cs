using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System;
using System.Reflection;
using System.Threading.Tasks; // nur falls dein Save-System Tasks nutzt

public class ManagementSceneController : MonoBehaviour
{
    [SerializeField] Button btnClose;      // „Zurück ins Spiel“ (oder „Schließen“)
    [SerializeField] GameObject taskPanel; // dein TaskPanel-Root
    [SerializeField] GameObject fabPanel;  // optional: Fabrikatorliste

    [Header("Optional: Reflection-Fallback nutzen, falls kein UnityEvent gesetzt ist.")]
    [SerializeField] private bool useReflectionFallback = true;

    private bool _isBusy;


    void Start()
    {
        if (btnClose) btnClose.onClick.AddListener(() => OnClickResume());
        // Panels initial sichtbar?
        if (taskPanel) taskPanel.SetActive(true);
        if (fabPanel) fabPanel.SetActive(true);
    }

    // ===== Helpers ============================================================

    private SpaceGame.Input.GameHotkeys GetHotkeys()
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<SpaceGame.Input.GameHotkeys>(UnityEngine.FindObjectsInactive.Include);
#else
        return UnityEngine.Object.FindFirstObjectByType<SpaceGame.Input.GameHotkeys>();
#endif
    }

    private static UnityEngine.Object FindAnyInstanceByType(Type type)
    {
#if UNITY_2023_1_OR_NEWER
        var objs = Resources.FindObjectsOfTypeAll(type);
        return (objs != null && objs.Length > 0) ? objs[0] : null;
#else
        return UnityEngine.Object.FindFirstObjectByType(type);
#endif
    }

    // ===== Resume =============================================================

    public async void OnClickResume()
    {
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            // Wichtig: NICHT TogglePause(false) aufrufen – bei Single-Load liefert UnloadSceneAsync null!
            // Stattdessen: erst ent-pausieren, dann gewünschtes Set laden, zuletzt evtl. Rest-Pausenszene entfernen.

            // 1) Zeit wieder starten
            Time.timeScale = 1f;

            // 2) Ziel-Set laden (entlädt Nicht-Bootstrap-Szenen und lädt Game + UI)
            if (SceneRouter.I != null)
                await SceneRouter.I.LoadSet(new[] { AppScene.Game, AppScene.GameUI });

            // 3) Sicherheit: Falls die Pause-Szene noch geladen sein sollte → hart entladen
            await CleanupPauseSceneIfLeftoverAsync();

            // 4) Gameplay-Inputs wieder aktivieren (falls beim Pausieren deaktiviert)
            var hk = GetHotkeys();
            hk?.ReenableGamePlay();
        }
        finally { _isBusy = false; }
    }

    private async UniTask CleanupPauseSceneIfLeftoverAsync()
    {
        var pause = SceneManager.GetSceneByName("12_PauseOptions");
        if (pause.IsValid() && pause.isLoaded)
            await SceneManager.UnloadSceneAsync(pause).ToUniTask();
    }

    private static object TryGetSingleton(Type t)
    {
        // Versuche gängige Singleton-Properties
        var propNames = new[] { "I", "Instance", "Current" };
        foreach (var pn in propNames)
        {
            var pi = t.GetProperty(pn, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (pi != null) return pi.GetValue(null, null);
        }
        return null;
    }

    private static async UniTask AwaitMaybeAsync(object result)
    {
        if (result == null) return;

        // UniTask
        if (result is UniTask ut)
        {
            await ut;
            return;
        }

        // Task
        if (result is Task t)
        {
            await t;
            return;
        }

        // Coroutine/void – gib einen Frame, falls intern noch was anstößt
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
    }
}