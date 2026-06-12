using UnityEngine;
using UnityEngine.EventSystems;

namespace SpaceGame.Core.Managers
{
    /// <summary>
    /// Manager für EventSystem-Konflikte bei additiven UI-Szenen.
    /// Stellt sicher, dass nur ein EventSystem aktiv ist.
    /// </summary>
    public class EventSystemManager : MonoBehaviour
    {
        public static EventSystemManager Instance { get; private set; }
        
        private EventSystem primaryEventSystem;
        private bool isInitialized = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            InitializeEventSystem();
        }

        /// <summary>
        /// Initialisiert das primäre EventSystem
        /// </summary>
        private void InitializeEventSystem()
        {
            if (isInitialized) return;

            // Finde das erste verfügbare EventSystem
            primaryEventSystem = FindAnyObjectByType<EventSystem>();
            
            if (primaryEventSystem != null)
            {
                Debug.Log($"[EventSystemManager] Primäres EventSystem gefunden: {primaryEventSystem.name}");
                isInitialized = true;
            }
            else
            {
                Debug.LogWarning("[EventSystemManager] Kein EventSystem gefunden - erstelle eines");
                CreateEventSystem();
            }
        }

        /// <summary>
        /// Erstellt ein neues EventSystem falls keines existiert
        /// </summary>
        private void CreateEventSystem()
        {
            GameObject eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.transform.SetParent(transform);
            
            primaryEventSystem = eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<StandaloneInputModule>();
            
            Debug.Log("[EventSystemManager] Neues EventSystem erstellt");
            isInitialized = true;
        }

        /// <summary>
        /// Wird aufgerufen, wenn eine neue Szene geladen wird
        /// </summary>
        public void OnSceneLoaded()
        {
            // Prüfe auf doppelte EventSystems
            EventSystem[] allEventSystems = FindObjectsByType<EventSystem>();
            
            if (allEventSystems.Length > 1)
            {
                Debug.LogWarning($"[EventSystemManager] {allEventSystems.Length} EventSystems gefunden - bereinige Duplikate");
                CleanupDuplicateEventSystems(allEventSystems);
            }
            
            // Stelle sicher, dass das primäre EventSystem aktiv ist
            if (primaryEventSystem != null && !primaryEventSystem.gameObject.activeInHierarchy)
            {
                primaryEventSystem.gameObject.SetActive(true);
                Debug.Log("[EventSystemManager] Primäres EventSystem reaktiviert");
            }
        }

        /// <summary>
        /// Entfernt doppelte EventSystems und behält nur das primäre
        /// </summary>
        private void CleanupDuplicateEventSystems(EventSystem[] eventSystems)
        {
            for (int i = 0; i < eventSystems.Length; i++)
            {
                EventSystem eventSystem = eventSystems[i];
                
                // Behalte das primäre EventSystem
                if (eventSystem == primaryEventSystem)
                {
                    continue;
                }
                
                // Deaktiviere oder zerstöre andere EventSystems
                if (eventSystem.gameObject.scene.name != "DontDestroyOnLoad")
                {
                    // EventSystem aus einer geladenen Szene - deaktiviere es
                    eventSystem.gameObject.SetActive(false);
                    Debug.Log($"[EventSystemManager] EventSystem deaktiviert: {eventSystem.name} in Szene {eventSystem.gameObject.scene.name}");
                }
                else
                {
                    // EventSystem im DontDestroyOnLoad - zerstöre es
                    Destroy(eventSystem.gameObject);
                    Debug.Log($"[EventSystemManager] EventSystem zerstört: {eventSystem.name}");
                }
            }
        }

        /// <summary>
        /// Öffentliche Methode zum manuellen Bereinigen von EventSystems
        /// </summary>
        [ContextMenu("Cleanup EventSystems")]
        public void ManualCleanup()
        {
            EventSystem[] allEventSystems = FindObjectsByType<EventSystem>();
            CleanupDuplicateEventSystems(allEventSystems);
        }

        /// <summary>
        /// Prüft, ob das EventSystem korrekt funktioniert
        /// </summary>
        public bool IsEventSystemWorking()
        {
            return primaryEventSystem != null && 
                   primaryEventSystem.gameObject.activeInHierarchy && 
                   primaryEventSystem.enabled;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
