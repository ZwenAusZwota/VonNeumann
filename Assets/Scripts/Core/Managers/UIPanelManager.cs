using System.Collections.Generic;
using UnityEngine;

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

        private void Awake()
        {
            // Registriere bei GameEvents
            GameEvents.OnPanelToggled += HandlePanelToggle;
            GameEvents.OnScanPanelToggled += HandleScanPanelToggle;
        }

        private void Start()
        {
            // Initialisiere Panels
            InitializePanels();
        }

        private void OnDestroy()
        {
            // Deregistriere von GameEvents
            GameEvents.OnPanelToggled -= HandlePanelToggle;
            GameEvents.OnScanPanelToggled -= HandleScanPanelToggle;
        }

        private void InitializePanels()
        {
            _panels.Clear();
            _panelStates.Clear();

            // Suche nach HUDPanelStateAdapter Komponenten
            var adapters = FindObjectsByType<HUDPanelStateAdapter>(FindObjectsSortMode.None);
            foreach (var adapter in adapters)
            {
                string panelId = adapter.PanelId;
                GameObject panel = adapter.gameObject;
                
                _panels[panelId] = panel;
                _panelStates[panelId] = adapter.IsVisible();
                
                Debug.Log($"[UIPanelManager] Registered panel: {panelId} (visible: {adapter.IsVisible()})");
            }

            // Suche nach konfigurierten Panels
            foreach (var config in panelConfigs)
            {
                if (config.panelObject != null && !_panels.ContainsKey(config.panelId))
                {
                    _panels[config.panelId] = config.panelObject;
                    _panelStates[config.panelId] = config.panelObject.activeSelf;
                    
                    Debug.Log($"[UIPanelManager] Registered configured panel: {config.panelId}");
                }
            }
        }

        private void HandlePanelToggle(string panelId, bool isVisible)
        {
            if (_panels.TryGetValue(panelId, out var panel))
            {
                // Verwende HUDPanelStateAdapter falls vorhanden
                var adapter = panel.GetComponent<HUDPanelStateAdapter>();
                if (adapter != null)
                {
                    adapter.SetVisible(isVisible);
                }
                else
                {
                    panel.SetActive(isVisible);
                }
                
                _panelStates[panelId] = isVisible;
                Debug.Log($"[UIPanelManager] Panel {panelId} set to {(isVisible ? "visible" : "hidden")}");
            }
            else
            {
                Debug.LogWarning($"[UIPanelManager] Panel {panelId} not found!");
            }
        }

        /// <summary>
        /// Panel über Event-System toggeln (verwendet aktuellen Zustand)
        /// </summary>
        public void TogglePanelViaEvent(string panelId)
        {
            if (_panels.TryGetValue(panelId, out var panel))
            {
                bool currentState = IsPanelVisible(panelId);
                bool newState = !currentState;
                
                Debug.Log($"[UIPanelManager] Toggling panel {panelId} via event from {currentState} to {newState}");
                HandlePanelToggle(panelId, newState);
            }
            else
            {
                Debug.LogWarning($"[UIPanelManager] Panel {panelId} not found for event toggling!");
            }
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
            if (_panels.TryGetValue(panelId, out var panel))
            {
                // Ermittle den aktuellen Zustand des Panels
                bool currentState = IsPanelVisible(panelId);
                bool newState = !currentState;
                
                Debug.Log($"[UIPanelManager] Toggling panel {panelId} from {currentState} to {newState}");
                HandlePanelToggle(panelId, newState);
            }
            else
            {
                Debug.LogWarning($"[UIPanelManager] Panel {panelId} not found for toggling!");
            }
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
        public void ResetPanelStates()
        {
            InitializePanels();
        }

        [System.Serializable]
        public class PanelConfig
        {
            public string panelId;
            public GameObject panelObject;
        }
    }
}
