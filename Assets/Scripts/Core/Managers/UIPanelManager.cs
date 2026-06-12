using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpaceGame.UI;

namespace SpaceGame.Core.Managers
{
    /// <summary>
    /// Zentraler UI-Panel-Manager, der GameEvents.TogglePanel Events verarbeitet
    /// und die entsprechenden Panels anzeigt/versteckt.
    /// </summary>
    public class UIPanelManager : MonoBehaviour
    {
        [Header("Panel Configuration")]
        [SerializeField] private List<PanelConfig> panelConfigs = new();
        
        private Dictionary<string, GameObject> _panels = new();
        private Dictionary<string, bool> _panelStates = new();
        private bool _initialized;

        private void Awake()
        {
            GameEvents.OnPanelToggled += HandlePanelToggle;
            GameEvents.OnScanPanelToggled += HandleScanPanelToggle;
            SceneManager.sceneLoaded += OnSceneLoaded;
            InitializePanels();
        }

        private void OnDestroy()
        {
            GameEvents.OnPanelToggled -= HandlePanelToggle;
            GameEvents.OnScanPanelToggled -= HandleScanPanelToggle;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.name.Contains("Game_UI", System.StringComparison.OrdinalIgnoreCase))
                return;

            RefreshPanels();
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;
            InitializePanels();
        }

        public void RefreshPanels()
        {
            _initialized = false;
            InitializePanels();
        }

        private void InitializePanels()
        {
            _panels.Clear();
            _panelStates.Clear();

            foreach (var config in panelConfigs)
            {
                if (string.IsNullOrWhiteSpace(config.panelId)) continue;

                GameObject panel = config.panelObject != null
                    ? config.panelObject
                    : ResolvePanelObject(config.panelId);
                if (panel == null) continue;

                RegisterPanel(config.panelId, panel, applySavedLayout: true);
                Debug.Log($"[UIPanelManager] Registered configured panel: {config.panelId}");
            }

            var adapters = FindObjectsByType<HUDPanelStateAdapter>(FindObjectsInactive.Include);
            foreach (var adapter in adapters)
            {
                string panelId = adapter.PanelId;
                if (string.IsNullOrWhiteSpace(panelId) || _panels.ContainsKey(panelId))
                    continue;

                RegisterPanel(panelId, adapter.gameObject, applySavedLayout: false);
                Debug.Log($"[UIPanelManager] Registered panel: {panelId} (visible: {adapter.IsVisible()})");
            }

            HudPanelThemeApplier.ApplyToAllUnder(transform);
            _initialized = true;
        }

        private void RegisterPanel(string panelId, GameObject panel, bool applySavedLayout)
        {
            _panels[panelId] = panel;

            if (applySavedLayout)
            {
                var draggable = panel.GetComponent<DraggableHudPanel>();
                if (draggable == null)
                    draggable = panel.GetComponentInChildren<DraggableHudPanel>(true);
                draggable?.ApplyInitialLayoutFromSave();
            }

            _panelStates[panelId] = IsPanelObjectVisible(panel);
        }

        private GameObject ResolvePanelObject(string panelId)
        {
            foreach (var adapter in FindObjectsByType<HUDPanelStateAdapter>(FindObjectsInactive.Include))
            {
                if (adapter.PanelId == panelId)
                    return adapter.gameObject;
            }

            var named = GameObject.Find(panelId);
            if (named != null)
                return named;

            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == panelId)
                    return t.gameObject;
            }

            return null;
        }

        private static bool IsPanelObjectVisible(GameObject panel)
        {
            var adapter = panel.GetComponent<HUDPanelStateAdapter>();
            return adapter != null ? adapter.IsVisible() : panel.activeSelf;
        }

        private void HandlePanelToggle(string panelId, bool isVisible)
        {
            if (!TryGetPanel(panelId, out var panel))
            {
                Debug.LogWarning($"[UIPanelManager] Panel {panelId} not found!");
                return;
            }

            SetPanelVisible(panel, isVisible);
            _panelStates[panelId] = isVisible;
            Debug.Log($"[UIPanelManager] Panel {panelId} set to {(isVisible ? "visible" : "hidden")}");
        }

        private static void SetPanelVisible(GameObject panel, bool isVisible)
        {
            var adapter = panel.GetComponent<HUDPanelStateAdapter>();
            if (adapter == null)
                adapter = panel.GetComponentInChildren<HUDPanelStateAdapter>(true);

            if (adapter != null)
                adapter.SetVisible(isVisible);
            else
                panel.SetActive(isVisible);
        }

        /// <summary>
        /// Panel über Event-System toggeln (verwendet aktuellen Zustand)
        /// </summary>
        public void TogglePanelViaEvent(string panelId)
        {
            if (!TryTogglePanel(panelId))
                Debug.LogWarning($"[UIPanelManager] Panel {panelId} not found for event toggling!");
        }

        private void HandleScanPanelToggle(bool isVisible)
        {
            HandlePanelToggle("ScanPanel", isVisible);
        }

        /// <summary>
        /// Panel manuell toggeln
        /// </summary>
        public void TogglePanel(string panelId)
        {
            if (!TryTogglePanel(panelId))
                Debug.LogWarning($"[UIPanelManager] Panel {panelId} not found for toggling!");
        }

        public bool TryTogglePanel(string panelId)
        {
            EnsureInitialized();
            if (!TryGetPanel(panelId, out _))
            {
                RefreshPanels();
                if (!TryGetPanel(panelId, out _))
                    return false;
            }

            bool currentState = IsPanelVisible(panelId);
            bool newState = !currentState;
            Debug.Log($"[UIPanelManager] Toggling panel {panelId} from {currentState} to {newState}");
            HandlePanelToggle(panelId, newState);
            return true;
        }

        private bool TryGetPanel(string panelId, out GameObject panel)
        {
            if (_panels.TryGetValue(panelId, out panel) && panel != null)
                return true;

            panel = ResolvePanelObject(panelId);
            if (panel == null)
                return false;

            RegisterPanel(panelId, panel, applySavedLayout: false);
            return true;
        }

        /// <summary>
        /// Panel-Status abfragen
        /// </summary>
        public bool IsPanelVisible(string panelId)
        {
            if (_panels.TryGetValue(panelId, out var panel))
            {
                var adapter = panel.GetComponent<HUDPanelStateAdapter>();
                if (adapter != null)
                {
                    return adapter.IsVisible();
                }
                return panel.activeSelf;
            }
            return _panelStates.GetValueOrDefault(panelId, false);
        }

        /// <summary>
        /// Alle Panels verstecken
        /// </summary>
        public void HideAllPanels()
        {
            foreach (var kvp in _panels)
            {
                kvp.Value.SetActive(false);
                _panelStates[kvp.Key] = false;
            }
        }

        /// <summary>
        /// Panel-Status zurücksetzen
        /// </summary>
        public void ResetPanelStates() => RefreshPanels();

        [System.Serializable]
        public class PanelConfig
        {
            public string panelId;
            public GameObject panelObject;
        }
    }
}
