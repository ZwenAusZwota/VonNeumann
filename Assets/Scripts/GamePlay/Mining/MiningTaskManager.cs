using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpaceGame.Mining
{
    /// <summary>
    /// Zentraler Speicher/Orchestrator für Tasks (UI-nahe Ebene).
    /// Kümmert sich zunächst nur um Anlegen/Löschen/Events.
    /// Später: Persistenz, Zuweisungen, Telemetrie.
    /// </summary>
    public class MiningTaskManager : MonoBehaviour
    {
        [System.Obsolete("Use ServiceContainer.Instance.Get<MiningTaskManager>() instead")]
        public static MiningTaskManager Instance { get; private set; }

        // TaskId -> Task
        readonly Dictionary<string, MiningTask> _tasks = new();

        public event Action TasksChanged; // UI refresher

        void Awake()
        {
            // Service Container Registrierung
            if (ServiceContainer.Instance != null)
            {
                ServiceContainer.Instance.RegisterSingleton<MiningTaskManager>(this);
            }

            // Singleton-Absicherung (für Rückwärtskompatibilität)
            var existingInstance = GetExistingInstance();
            if (existingInstance != null && existingInstance != this) 
            { 
                Destroy(gameObject); 
                return; 
            }
            SetInstance(this);
            DontDestroyOnLoad(gameObject);
        }

        // Hilfsmethoden zur Vermeidung der Warnung
        private static MiningTaskManager GetExistingInstance()
        {
            return ServiceContainer.Instance?.Get<MiningTaskManager>();
        }

        private static void SetInstance(MiningTaskManager instance)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            Instance = instance;
#pragma warning restore CS0618
        }

        public IReadOnlyList<MiningTask> GetAll() => _tasks.Values.ToList();

        public MiningTask CreateTask(
            string name,
            SearchMode mode,
            RegionType region,
            IEnumerable<ResourceKind> wanted,
            Transform dropoff,
            bool loopUntilStopped,
            float scanRadiusUnits,
            float reScanCooldownSec,
            int preferredMinerCount)
        {
            var task = new MiningTask(name)
            {
                Mode = mode,
                RegionPreference = region,
                DropoffHub = dropoff,
                LoopUntilStopped = loopUntilStopped,
                ScanRadiusUnits = scanRadiusUnits,
                ReScanCooldownSec = reScanCooldownSec,
                PreferredMinerCount = Mathf.Max(1, preferredMinerCount)
            };

            if (mode == SearchMode.Specific && wanted != null)
            {
                foreach (var w in wanted) task.Wanted.Add(w);
            }

            _tasks[task.TaskId] = task;
            TasksChanged?.Invoke();
            return task;
        }

        public bool RemoveTask(string taskId)
        {
            var ok = _tasks.Remove(taskId);
            if (ok) TasksChanged?.Invoke();
            return ok;
        }

        public MiningTask GetById(string taskId)
            => _tasks.TryGetValue(taskId, out var t) ? t : null;

        // Platzhalter f�r sp�ter: Assign/Unassign Miner, Persistenz etc.
    }
}
