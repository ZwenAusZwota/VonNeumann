using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks; // für .Forget()

namespace SpaceGame.Input
{
    /// <summary>
    /// Hotkeys für Gameplay:
    /// S -> Scan-Panel toggeln (über HUDPanelRouter)
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
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (_actions == null)
            {
                _actions = new @InputController();
                _actions.GamePlay.SetCallbacks(this);
            }
            _actions.GamePlay.Enable();
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
            _actions?.Dispose();
            if (_instance == this) _instance = null;
        }

        // -------- IGamePlayActions --------

        public void OnScan(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            if (HUDPanelRouter.Active != null) HUDPanelRouter.Active.ToggleScan();
            else Debug.LogWarning("[GameHotkeys] Keine aktive HUDPanelRouter-Instanz (10_Game_UI nicht geladen?).");
        }

        public void OnInventory(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            if (HUDPanelRouter.Active != null) HUDPanelRouter.Active.ToggleInventory();
            else Debug.LogWarning("[GameHotkeys] Keine aktive HUDPanelRouter-Instanz (10_Game_UI nicht geladen?).");
        }

        public void OnNavigation(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;
            if (HUDPanelRouter.Active != null) HUDPanelRouter.Active.ToggleNav();
            else Debug.LogWarning("[GameHotkeys] Keine aktive HUDPanelRouter-Instanz (10_Game_UI nicht geladen?).");
        }

        public void OnMining(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;

#if UNITY_2023_1_OR_NEWER
            var miner = Object.FindFirstObjectByType<ProbeMiner>(FindObjectsInactive.Include);
#else
            var miner = Object.FindObjectOfType<ProbeMiner>(true);
#endif
            if (miner == null)
            {
                Debug.LogWarning("[GameHotkeys] Kein ProbeMiner gefunden – Mining nicht möglich.");
                return;
            }

            miner.ToggleMining(); // kümmert sich um HUD-Meldung & Inventar
        }

        /// <summary>
        /// F10/M: Pausieren + Management als Single-Set laden (entlädt 10_Game / 10_Game_UI).
        /// </summary>
        public void OnManagement(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;

            if (SceneRouter.I == null)
            {
                Debug.LogError("[GameHotkeys] SceneRouter.I ist null – Management-Aufruf abgebrochen.");
                return;
            }

            _actions?.GamePlay.Disable();
            SceneRouter.I.ToManagementOverlaySingle().Forget();
        }

        /// <summary>
        /// ESC/F11: Pausieren + Pause als Single-Set laden (entlädt 10_Game / 10_Game_UI).
        /// </summary>
        public void OnPause(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;

            if (SceneRouter.I == null)
            {
                Debug.LogError("[GameHotkeys] SceneRouter.I ist null – Pause-Aufruf abgebrochen.");
                return;
            }

            _actions?.GamePlay.Disable();
            SceneRouter.I.ToPauseOverlaySingle().Forget();
        }

        public void OnQuickSave(InputAction.CallbackContext ctx) { }
        public void OnQuickLoad(InputAction.CallbackContext ctx) { }

        /// <summary>Vom Overlay (Resume) wieder ins Spiel zurück.</summary>
        public void ReenableGamePlay()
        {
            _actions?.GamePlay.Enable();
        }
    }
}
