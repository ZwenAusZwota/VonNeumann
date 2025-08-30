// Assets/Scripts/00_Manager/InputRouter.cs
using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-200)] // sehr früh initialisieren
public class InputRouter : MonoBehaviour
{
    public static InputRouter I { get; private set; }

    [Header("Action Asset & Maps")]
    [Tooltip("Dein InputActionAsset (neues Input System).")]
    [SerializeField] private InputActionAsset actions;

    [Tooltip("Name der Gameplay-Action-Map.")]
    [SerializeField] private string gameplayMapName = "Gameplay";

    [Tooltip("Name der UI-Action-Map.")]
    [SerializeField] private string uiMapName = "UI";

    [Header("Action-Namen (optional, wenn Events genutzt werden)")]
    [SerializeField] private string pauseActionName = "Pause";
    [SerializeField] private string inventoryActionName = "Inventory";
    [SerializeField] private string toggleTasksActionName = "ToggleTasks";
    [SerializeField] private string researchActionName = "Research";
    [SerializeField] private string quickSaveActionName = "QuickSave";
    [SerializeField] private string quickLoadActionName = "QuickLoad";

    [Header("Startzustand")]
    [Tooltip("Beim Start automatisch Gameplay-Map aktivieren, UI-Map deaktivieren.")]
    [SerializeField] private bool autoEnableGameplayOnStart = true;

    // ---- Public Events -------------------------------------------------------
    public event Action OnPause;        // z. B. Pausemenü öffnen
    public event Action OnInventory;    // Inventar umschalten
    public event Action OnToggleTasks;  // Tasks-Panel umschalten
    public event Action OnResearch;     // Forschung öffnen
    public event Action OnQuickSave;    // Schnellspeichern
    public event Action OnQuickLoad;    // Schnellladen

    // ---- Internals -----------------------------------------------------------
    private InputActionMap _gameplayMap;
    private InputActionMap _uiMap;

    private InputAction _pause;
    private InputAction _inventory;
    private InputAction _toggleTasks;
    private InputAction _research;
    private InputAction _quickSave;
    private InputAction _quickLoad;

    public bool GameplayEnabled => _gameplayMap != null && _gameplayMap.enabled;
    public bool UIEnabled => _uiMap != null && _uiMap.enabled;

    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        if (actions == null)
        {
            Debug.LogError("[InputRouter] Kein InputActionAsset zugewiesen.");
            return;
        }

        _gameplayMap = actions.FindActionMap(gameplayMapName, throwIfNotFound: false);
        _uiMap = actions.FindActionMap(uiMapName, throwIfNotFound: false);

        if (_gameplayMap == null) Debug.LogWarning($"[InputRouter] Gameplay-Map '{gameplayMapName}' nicht gefunden.");
        if (_uiMap == null) Debug.LogWarning($"[InputRouter] UI-Map '{uiMapName}' nicht gefunden.");

        // Actions (optional)
        _pause = FindActionSafe(_gameplayMap, pauseActionName);
        _inventory = FindActionSafe(_gameplayMap, inventoryActionName);
        _toggleTasks = FindActionSafe(_gameplayMap, toggleTasksActionName);
        _research = FindActionSafe(_gameplayMap, researchActionName);
        _quickSave = FindActionSafe(_gameplayMap, quickSaveActionName);
        _quickLoad = FindActionSafe(_gameplayMap, quickLoadActionName);

        // Fallback: Pause kann auch in UI-Map liegen (manche UIs erlauben Resume)
        if (_pause == null) _pause = FindActionSafe(_uiMap, pauseActionName);

        // Events binden (performed)
        Bind(_pause, () => OnPause?.Invoke());
        Bind(_inventory, () => OnInventory?.Invoke());
        Bind(_toggleTasks, () => OnToggleTasks?.Invoke());
        Bind(_research, () => OnResearch?.Invoke());
        Bind(_quickSave, () => OnQuickSave?.Invoke());
        Bind(_quickLoad, () => OnQuickLoad?.Invoke());
    }

    private void Start()
    {
        if (autoEnableGameplayOnStart)
        {
            SetGameplayEnabled(true);
            SetUIEnabled(false);
        }
    }

    private void OnDestroy()
    {
        Unbind(_pause, () => OnPause?.Invoke());
        Unbind(_inventory, () => OnInventory?.Invoke());
        Unbind(_toggleTasks, () => OnToggleTasks?.Invoke());
        Unbind(_research, () => OnResearch?.Invoke());
        Unbind(_quickSave, () => OnQuickSave?.Invoke());
        Unbind(_quickLoad, () => OnQuickLoad?.Invoke());

        if (I == this) I = null;
    }

    // -------------------------------------------------------------------------
    // Öffentliche API
    // -------------------------------------------------------------------------

    /// <summary>Aktiviert/Deaktiviert die Gameplay-Action-Map.</summary>
    public void SetGameplayEnabled(bool enabled)
    {
        if (_gameplayMap == null) return;
        if (enabled)
        {
            if (!_gameplayMap.enabled) _gameplayMap.Enable();
        }
        else
        {
            if (_gameplayMap.enabled) _gameplayMap.Disable();
        }
    }

    /// <summary>Aktiviert/Deaktiviert die UI-Action-Map.</summary>
    public void SetUIEnabled(bool enabled)
    {
        if (_uiMap == null) return;
        if (enabled)
        {
            if (!_uiMap.enabled) _uiMap.Enable();
        }
        else
        {
            if (_uiMap.enabled) _uiMap.Disable();
        }
    }

    /// <summary>Schaltet bequem in den Gameplay-Modus (Gameplay an, UI aus).</summary>
    public void SwitchToGameplay()
    {
        SetUIEnabled(false);
        SetGameplayEnabled(true);
    }

    /// <summary>Schaltet bequem in den UI-Modus (UI an, Gameplay aus).</summary>
    public void SwitchToUI()
    {
        SetGameplayEnabled(false);
        SetUIEnabled(true);
    }

    /// <summary>Wechselt beide Maps entsprechend Pause-Status (typisch beim Öffnen/Schließen des Pausenmenüs).</summary>
    public void SetPausedInput(bool paused)
    {
        if (paused)
        {
            // Gameplay-Eingaben aus, UI-Eingaben an
            SetGameplayEnabled(false);
            SetUIEnabled(true);
        }
        else
        {
            // Zurück ins Spiel
            SetUIEnabled(false);
            SetGameplayEnabled(true);
        }
    }

    /// <summary>Ermittelt eine Action aus einer Map (z. B. für Rebind-UI).</summary>
    public InputAction FindAction(string mapName, string actionName)
    {
        var map = actions?.FindActionMap(mapName, false);
        return map?.FindAction(actionName, false);
    }

    /// <summary>Direkter Zugriff auf die Map-Objekte (z. B. für Rebinding-Menüs).</summary>
    public InputActionMap GameplayMap => _gameplayMap;
    public InputActionMap UIMap => _uiMap;

    // -------------------------------------------------------------------------
    // Hilfsfunktionen
    // -------------------------------------------------------------------------
    private static InputAction FindActionSafe(InputActionMap map, string actionName)
    {
        if (map == null || string.IsNullOrWhiteSpace(actionName)) return null;
        var a = map.FindAction(actionName, throwIfNotFound: false);
        return a;
    }

    private static void Bind(InputAction action, Action callback)
    {
        if (action == null || callback == null) return;
        action.performed += OnPerformed;

        void OnPerformed(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) callback.Invoke();
        }
    }

    private static void Unbind(InputAction action, Action callback)
    {
        // Unity erlaubt kein einfaches -= mit gleicher Lambda-Instanz wie oben,
        // daher hier kein explizites Unbind. In der Praxis ist das unkritisch,
        // da der Router persistent ist. Wenn du strikt unbinden willst, nutze
        // benannte Handler statt Lambdas.
    }
}
