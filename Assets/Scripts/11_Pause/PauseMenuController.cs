// Assets/Scripts/11_Pause/PauseMenuController.cs
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using System;
using System.Reflection;
using System.Threading.Tasks; // nur falls dein Save-System Tasks nutzt

public class PauseMenuController : MonoBehaviour
{
    [Header("Save – vor Exit ausführen")]
    [Tooltip("Hier deine Save-Methode im Inspector anhängen (z. B. SaveSystem.SaveNow).")]
    [SerializeField] private UnityEvent onBeforeExitSave;

    [Header("Optional: Reflection-Fallback nutzen, falls kein UnityEvent gesetzt ist.")]
    [SerializeField] private bool useReflectionFallback = true;

    private bool _isBusy;

    // ===== Helpers ============================================================

    private SpaceGame.Input.GameHotkeys GetHotkeys()
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<SpaceGame.Input.GameHotkeys>(UnityEngine.FindObjectsInactive.Include);
#else
        return UnityEngine.Object.FindObjectOfType<SpaceGame.Input.GameHotkeys>();
#endif
    }

    private static UnityEngine.Object FindAnyInstanceByType(Type type)
    {
#if UNITY_2023_1_OR_NEWER
        var objs = Resources.FindObjectsOfTypeAll(type);
        return (objs != null && objs.Length > 0) ? objs[0] : null;
#else
        return UnityEngine.Object.FindObjectOfType(type);
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
        var pause = SceneManager.GetSceneByName("11_PauseOptions");
        if (pause.IsValid() && pause.isLoaded)
            await SceneManager.UnloadSceneAsync(pause).ToUniTask();
    }

    // ===== Exit ===============================================================

    public void OnExit()
    {
        if (_isBusy) return;
        ExitFlowAsync().Forget();
    }

    private async UniTask ExitFlowAsync()
    {
        _isBusy = true;
        try
        {
            // 1) Speichern
            await SaveGameAsync();

            // 2) Timescale zurücksetzen (für OnQuit-Handler etc.)
            Time.timeScale = 1f;

            // 3) Beenden / Playmode verlassen
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        finally
        {
            _isBusy = false;
        }
    }

    // ===== Save ===============================================================

    private async UniTask SaveGameAsync()
    {
        // a) UnityEvent-Hook zuerst (empfohlen)
        if (onBeforeExitSave != null && onBeforeExitSave.GetPersistentEventCount() > 0)
        {
            try
            {
                onBeforeExitSave.Invoke();
                // kurzer Yield, falls das Event Coroutines/UniTasks anstößt
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                return;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PauseMenuController] Save-Event Fehler: {e.Message}");
            }
        }

        // b) Optionaler Fallback via Reflection (best effort)
        if (!useReflectionFallback) return;

        try
        {
            // Suche bekannte Kandidaten: SaveSystem / GameManager / SaveManager
            var candidates = new[] { "SaveSystem", "GameManager", "SaveManager" };
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in asm.GetTypes())
                {
                    if (Array.IndexOf(candidates, type.Name) < 0) continue;

                    // Singleton-Instanz holen: I / Instance / Current (falls vorhanden)
                    object inst = TryGetSingleton(type);

                    // sonst irgendeine Szene-Instanz versuchen (ohne Obsolete-API)
                    if (inst == null)
                    {
                        var any = FindAnyInstanceByType(type);
                        var mb = any as MonoBehaviour;
                        inst = mb;
                    }

                    // Mögliche Methodennamen
                    var methodNames = new[] { "SaveAsync", "SaveGameAsync", "Save", "SaveGame" };
                    foreach (var mName in methodNames)
                    {
                        var mi = type.GetMethod(mName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        if (mi == null) continue;

                        object target = mi.IsStatic ? null : inst;
                        if (!mi.IsStatic && target == null) continue;

                        var result = mi.GetParameters().Length == 0
                            ? mi.Invoke(target, null)
                            : null; // nur parameterlose Varianten unterstützen wir hier

                        await AwaitMaybeAsync(result);
                        return; // sobald eine Methode erfolgreich war → raus
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PauseMenuController] Reflection-Fallback fürs Speichern fehlgeschlagen: {e.Message}");
        }
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
