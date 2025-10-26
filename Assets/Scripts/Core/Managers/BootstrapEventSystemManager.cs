using UnityEngine;

namespace SpaceGame.Core.Managers
{
    /// <summary>
    /// Automatischer EventSystem-Manager für die Bootstrap-Szene.
    /// Wird automatisch aktiviert und überwacht EventSystem-Konflikte.
    /// </summary>
    public class BootstrapEventSystemManager : MonoBehaviour
    {
        private void Awake()
        {
            // Stelle sicher, dass der EventSystemManager existiert
            if (EventSystemManager.Instance == null)
            {
                GameObject managerGO = new GameObject("EventSystemManager");
                managerGO.AddComponent<EventSystemManager>();
                DontDestroyOnLoad(managerGO);
            }
        }

        private void Start()
        {
            // Initiale Bereinigung nach dem Start
            if (EventSystemManager.Instance != null)
            {
                EventSystemManager.Instance.OnSceneLoaded();
            }
        }
    }
}
