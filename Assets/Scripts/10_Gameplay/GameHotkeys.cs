using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks; // f?r .Forget()
using SpaceGame.Core.Managers;

namespace SpaceGame.Input
{
    /// <summary>
    /// Hotkeys f?r Gameplay:
    /// S -> Scan-Panel toggeln
    /// I -> Inventar-Panel toggeln
    /// Navigation -> Nav-Panel toggeln
    /// F10/M -> Management als Single-Set (pausiert + 12_Management; 10_Game & 10_Game_UI werden entladen)
    /// ESC/F11 -> Pause als Single-Set (pausiert + 11_PauseOptions; 10_Game & 10_Game_UI werden entladen)
    /// </summary>
    public class GameHotkeys : MonoBehaviour, @InputController.IGamePlayActions
    {
        private @InputController _actions;
        private static GameHotkeys _instance;

        private void Awake()
        {
            Debug.Log("[GameHotkeys] Awake called");
            if (_instance != null && _instance != this)
            {
                Debug.Log("[GameHotkeys] Destroying duplicate instance");
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[GameHotkeys] Instance set and DontDestroyOnLoad applied");
        }

        private void Start()
        {
            Debug.Log("[GameHotkeys] Start called");
            CheckInputActions();
        }

        /// <summary>
        /// Pr?ft, ob die Input-Actions korrekt registriert sind
        /// </summary>
        private void CheckInputActions()
        {
            if (_actions == null)
            {
                Debug.LogError("[GameHotkeys] InputController is null!");
                return;
            }

            var gameplayMap = _actions.GamePlay;
            if (!gameplayMap.enabled)
            {
                Debug.LogError("[GameHotkeys] GamePlay map is not enabled!");
                return;
            }

            Debug.Log($"[GameHotkeys] GamePlay map enabled: {gameplayMap.enabled}");
            
            // Pr?fe spezifische Actions
            var scanAction = gameplayMap.Scan;
            var inventoryAction = gameplayMap.Inventory;
            var navigationAction = gameplayMap.Navigation;
            
            Debug.Log($"[GameHotkeys] Scan action: {(scanAction != null ? "found" : "null")}");
            Debug.Log($"[GameHotkeys] Inventory action: {(inventoryAction != null ? "found" : "null")}");
            Debug.Log($"[GameHotkeys] Navigation action: {(navigationAction != null ? "found" : "null")}");
        }

        /// <summary>
        /// Test-Methode f?r Debugging - kann manuell aufgerufen werden
        /// </summary>
        [ContextMenu("Test Scan Panel")]
        public void TestScanPanel()
        {
            Debug.Log("[GameHotkeys] TestScanPanel called manually");
            OnScan(new UnityEngine.InputSystem.InputAction.CallbackContext());
        }

        /// <summary>
        /// Test-Methode f?r Debugging - kann manuell aufgerufen werden
        /// </summary>
        [ContextMenu("Test Inventory Panel")]
        public void TestInventoryPanel()
        {
            Debug.Log("[GameHotkeys] TestInventoryPanel called manually");
            OnInventory(new UnityEngine.InputSystem.InputAction.CallbackContext());
        }

        /// <summary>
        /// Test-Methode f?r Debugging - kann manuell aufgerufen werden
        /// </summary>
        [ContextMenu("Test Navigation Panel")]
        public void TestNavigationPanel()
        {
            Debug.Log("[GameHotkeys] TestNavigationPanel called manually");
            OnNavigation(new UnityEngine.InputSystem.InputAction.CallbackContext());
        }

        private void OnEnable()
        {
            Debug.Log("[GameHotkeys] OnEnable called");
            if (_actions == null)
            {
                _actions = new @InputController();
                _actions.GamePlay.SetCallbacks(this);
                Debug.Log("[GameHotkeys] InputController created and callbacks set");
            }
            _actions.GamePlay.Enable();
            Debug.Log("[GameHotkeys] GamePlay actions enabled");
        }

        private void OnDisable()
        {
            if (_actions != null)
            {
                _actions.GamePlay.RemoveCallbacks(this);
                _actions.GamePlay.Disable();
            }
        }

        private void OnDestroy()
        {
            if (_actions != null)
            {
                _actions.GamePlay.RemoveCallbacks(this);
                if (_actions.GamePlay.enabled)
                    _actions.GamePlay.Disable();
                _actions.Dispose();
                _actions = null;
            }
            if (_instance == this) _instance = null;
        }

        // -------- IGamePlayActions --------

        public void OnScan(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            ToggleHudPanel("ScanPanel");
        }

        public void OnInventory(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            ToggleHudPanel("InventoryPanel");
        }

        public void OnNavigation(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            ToggleHudPanel("NavPanel");
        }

        private void ToggleHudPanel(string panelId)
        {
            var uiPanelManager = FindAnyObjectByType<UIPanelManager>(FindObjectsInactive.Include);
            if (uiPanelManager != null && uiPanelManager.TryTogglePanel(panelId))
                return;

            var panel = FindPanel(panelId);
            if (panel == null)
            {
                Debug.LogWarning($"[GameHotkeys] Panel {panelId} not found (UI scene loaded?)");
                return;
            }

            var adapter = panel.GetComponent<HUDPanelStateAdapter>();
            if (adapter != null)
                adapter.SetVisible(!adapter.IsVisible());
            else
                panel.SetActive(!panel.activeSelf);
        }

        public void OnMining(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;

#if UNITY_2023_1_OR_NEWER
            var miner = Object.FindAnyObjectByType<ProbeMiner>(FindObjectsInactive.Include);
#else
            var miner = Object.FindAnyObjectByType<ProbeMiner>(FindObjectsInactive.Include);
#endif
            if (miner == null)
            {
                GameEvents.PostHUDMessage("Kein ProbeMiner gefunden ? Mining nicht m?glich.");
                return;
            }

            miner.ToggleMining(); // k?mmert sich um HUD-Meldung & Inventar
        }

        /// <summary>
        /// F10/M: Pausieren + Management als Single-Set laden (entl?dt 10_Game / 10_Game_UI).
        /// </summary>
        public void OnManagement(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;

            _actions?.GamePlay.Disable();
            SceneRouter.I.ToManagementOverlaySingle().Forget();
        }

        /// <summary>
        /// ESC/F11: Pausieren + Pause als Single-Set laden (entl?dt 10_Game / 10_Game_UI).
        /// </summary>
        public void OnPause(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;

            _actions?.GamePlay.Disable();
            SceneRouter.I.ToPauseOverlaySingle().Forget();
        }

        public void OnQuickSave(InputAction.CallbackContext ctx) { }
        public void OnQuickLoad(InputAction.CallbackContext ctx) { }

        /// <summary>Vom Overlay (Resume) wieder ins Spiel zur?ck.</summary>
        public void ReenableGamePlay()
        {
            _actions?.GamePlay.Enable();
        }

        /// <summary>
        /// Findet ein Panel anhand des Namens oder der HUDPanelStateAdapter ID
        /// </summary>
        private GameObject FindPanel(string panelId)
        {
            Debug.Log($"[GameHotkeys] Searching for panel: {panelId}");
            
            // Suche nach GameObject mit dem Namen
            var panel = GameObject.Find(panelId);
            if (panel != null) 
            {
                Debug.Log($"[GameHotkeys] Found panel by name: {panelId}");
                return panel;
            }

            // Suche nach HUDPanelStateAdapter mit der entsprechenden ID
            var adapters = FindObjectsByType<HUDPanelStateAdapter>(FindObjectsInactive.Include);
            Debug.Log($"[GameHotkeys] Found {adapters.Length} HUDPanelStateAdapter components");
            
            foreach (var adapter in adapters)
            {
                Debug.Log($"[GameHotkeys] Checking adapter with ID: {adapter.PanelId}");
                if (adapter.PanelId == panelId)
                {
                    Debug.Log($"[GameHotkeys] Found panel by adapter: {panelId}");
                    return adapter.gameObject;
                }
            }

            Debug.LogWarning($"[GameHotkeys] Panel not found: {panelId}");
            return null;
        }
    }
}
