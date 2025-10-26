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
        public static MiningTaskManager Instance { get; private set; }

        // TaskId -> Task
        readonly Dictionary<string, MiningTask> _tasks = new();

        public event Action TasksChanged; // UI refresher

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
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

        // Platzhalter für später: Assign/Unassign Miner, Persistenz etc.
    }
}
