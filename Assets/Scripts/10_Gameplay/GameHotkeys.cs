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
    /// M -> Management/Research öffnen
    /// ESC -> Pause als Single-Szene laden
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
            //if (HUDPanelRouter.Active != null) HUDPanelRouter.Active.ToggleNav();
            //else Debug.LogWarning("[GameHotkeys] Keine aktive HUDPanelRouter-Instanz (10_Game_UI nicht geladen?).");
        }

        public void OnManagement(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;

            if (SceneRouter.I == null)
            {
                Debug.LogError("[GameHotkeys] SceneRouter.I ist null – Management-Aufruf abgebrochen.");
                return;
            }
            // Öffnet deine Management/Research-Ebene additiv
            SceneRouter.I.ToggleManagement(true).Forget();
        }

        public void OnPause(InputAction.CallbackContext ctx)
        {
            if (!ctx.performed) return;

            if (SceneRouter.I == null)
            {
                Debug.LogError("[GameHotkeys] SceneRouter.I ist null – Pause-Aufruf abgebrochen.");
                return;
            }

            // Während des Szenenwechsels keine Gameplay-Inputs mehr
            _actions?.GamePlay.Disable();

            // Pause als Single-Szene (Kamera-Übergabe im Router)
            SceneRouter.I.ToPauseSingle(true).Forget();
        }

        public void OnQuickSave(InputAction.CallbackContext ctx) { }
        public void OnQuickLoad(InputAction.CallbackContext ctx) { }
        public void OnNavigation(InputAction.CallbackContext ctx, Vector2 _ignored) { } // falls dein Interface Overloads hat

        /// <summary>Vom Pause-Menü nach Resume aufrufen.</summary>
        public void ReenableGamePlay()
        {
            _actions?.GamePlay.Enable();
        }
    }
}
