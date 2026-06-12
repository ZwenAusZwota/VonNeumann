using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Zeigt beim Spielstart im Message-Panel eine Initialisierungsmeldung
/// und listet aktive/inaktive Sondermodule mit ihren Tasten.
/// </summary>
[DisallowMultipleComponent]
public class ProbeStartupBriefing : MonoBehaviour
{
    [SerializeField] private float probeWaitSeconds = 15f;
    [SerializeField] private int initializePauseMs = 2500;
    [SerializeField] private int messageDelayMs = 60;

    private static bool _completed;
    private CancellationTokenSource _cts;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSessionState() => _completed = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name.Contains("MainMenu") || scene.name.Contains("Loading"))
            _completed = false;

        if (!scene.name.Contains("Game_UI"))
            return;

        var logUi = FindAnyObjectByType<HUDMessageLogUI>(FindObjectsInactive.Include);
        if (logUi == null || logUi.GetComponent<ProbeStartupBriefing>() != null)
            return;

        logUi.gameObject.AddComponent<ProbeStartupBriefing>();
    }

    private void Start()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        RunBriefingAsync(_cts.Token).Forget();
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async UniTaskVoid RunBriefingAsync(CancellationToken token)
    {
        if (_completed)
            return;

        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);

        HUDMessageBus.Post("Initialisiere...");
        if (initializePauseMs > 0)
            await UniTask.Delay(initializePauseMs, cancellationToken: token);

        var probe = await WaitForProbeAsync(token);
        if (probe == null)
        {
            HUDMessageBus.Post("Keine Sonde gefunden.");
            return;
        }

        using var input = new InputController();
        var modules = BuildModuleReport(probe.gameObject, input);

        if (modules.active.Count > 0)
        {
            HUDMessageBus.Post("Aktive Module:");
            foreach (var line in modules.active)
            {
                HUDMessageBus.Post("  " + line);
                await Delay(token);
            }
        }

        if (modules.inactive.Count > 0)
        {
            HUDMessageBus.Post("Inaktive Module:");
            foreach (var line in modules.inactive)
            {
                HUDMessageBus.Post("  " + line);
                await Delay(token);
            }
        }

        _completed = true;
    }

    private async UniTask<ProbeController> WaitForProbeAsync(CancellationToken token)
    {
        var deadline = Time.realtimeSinceStartup + probeWaitSeconds;

        while (Time.realtimeSinceStartup < deadline)
        {
            token.ThrowIfCancellationRequested();

            var probe = ResolveProbe();
            if (probe != null)
                return probe;

            await UniTask.Delay(200, cancellationToken: token);
        }

        return null;
    }

    private static ProbeController ResolveProbe()
    {
        if (HUDBindingService.I?.SelectedItem?.Transform != null)
        {
            var fromHud = HUDBindingService.I.SelectedItem.Transform
                .GetComponentInParent<ProbeController>();
            if (fromHud != null)
                return fromHud;
        }

        return FindAnyObjectByType<ProbeController>(FindObjectsInactive.Include);
    }

    private async UniTask Delay(CancellationToken token)
    {
        if (messageDelayMs > 0)
            await UniTask.Delay(messageDelayMs, cancellationToken: token);
    }

    private static (List<string> active, List<string> inactive) BuildModuleReport(
        GameObject probe,
        InputController input)
    {
        var active = new List<string>();
        var inactive = new List<string>();

        foreach (var entry in ModuleCatalog)
        {
            if (!entry.IsPresent(probe))
                continue;

            var line = $"{entry.DisplayName} — {entry.GetHotkey(input)}";
            if (entry.IsActive(probe))
                active.Add(line);
            else
                inactive.Add(line);
        }

        return (active, inactive);
    }

    private readonly struct ModuleEntry
    {
        public string DisplayName { get; }
        public Func<GameObject, bool> IsPresent { get; }
        public Func<GameObject, bool> IsActive { get; }
        public Func<InputController, string> GetHotkey { get; }

        public ModuleEntry(
            string displayName,
            Func<GameObject, bool> isPresent,
            Func<GameObject, bool> isActive,
            Func<InputController, string> getHotkey)
        {
            DisplayName = displayName;
            IsPresent = isPresent;
            IsActive = isActive;
            GetHotkey = getHotkey;
        }
    }

    private static readonly ModuleEntry[] ModuleCatalog =
    {
        new(
            "Manueller Flug",
            probe => probe.GetComponentInChildren<ProbeController>(true) != null,
            probe => IsBehaviourActive<ProbeController>(probe),
            ic => FormatProbeFlightKeys(ic)),
        new(
            "Scanner",
            HasScanner,
            IsScannerActive,
            ic => KeyLabel(ic.GamePlay.Scan)),
        new(
            "Inventar",
            probe => probe.GetComponentInChildren<InventoryController>(true) != null,
            probe => IsBehaviourActive<InventoryController>(probe),
            ic => KeyLabel(ic.GamePlay.Inventory)),
        new(
            "Navigation",
            probe => probe.GetComponentInChildren<ProbeAutopilot>(true) != null,
            probe => IsBehaviourActive<ProbeAutopilot>(probe),
            ic => KeyLabel(ic.GamePlay.Navigation)),
        new(
            "Mining",
            probe => probe.GetComponentInChildren<ProbeMiner>(true) != null,
            probe => IsBehaviourActive<ProbeMiner>(probe),
            ic => KeyLabel(ic.GamePlay.Mining)),
        new(
            "Fabrikator",
            probe => probe.GetComponentInChildren<FabricatorController>(true) != null,
            probe => IsBehaviourActive<FabricatorController>(probe),
            ic => KeyLabel(ic.GamePlay.Management)),
    };

    private static bool IsBehaviourActive<T>(GameObject root) where T : Behaviour
    {
        var components = root.GetComponentsInChildren<T>(true);
        foreach (var component in components)
        {
            if (component != null && component.isActiveAndEnabled)
                return true;
        }

        return false;
    }

    private static bool HasScanner(GameObject probe) =>
        probe.GetComponentInChildren<NearScannerController>(true) != null
        || probe.GetComponentInChildren<FarScannerController>(true) != null;

    private static bool IsScannerActive(GameObject probe)
    {
        var near = probe.GetComponentInChildren<NearScannerController>(true);
        if (near != null && near.isActiveAndEnabled)
            return true;

        var far = probe.GetComponentInChildren<FarScannerController>(true);
        return far != null && far.isActiveAndEnabled;
    }

    private static string KeyLabel(InputAction action)
    {
        if (action == null || action.bindings.Count == 0)
            return "—";

        return GetKeyboardBindingLabel(action);
    }

    private static string FormatProbeFlightKeys(InputController ic)
    {
        var rotate = GetKeyboardBindingLabel(ic.Probe.Rotate);
        var thrust = GetKeyboardBindingLabel(ic.Probe.Thrust);
        var roll = GetKeyboardBindingLabel(ic.Probe.Roll);

        return $"Steuerung {rotate}, Schub {thrust}, Roll {roll}";
    }

    private static string GetKeyboardBindingLabel(InputAction action)
    {
        if (action == null || action.bindings.Count == 0)
            return "—";

        for (var i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (binding.isComposite)
                return action.GetBindingDisplayString(i);
        }

        for (var i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (!binding.isComposite && binding.path.Contains("Keyboard"))
                return action.GetBindingDisplayString(i);
        }

        return action.GetBindingDisplayString();
    }
}
