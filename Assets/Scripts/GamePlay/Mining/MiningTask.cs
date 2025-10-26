using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceGame.Mining
{
    public enum SearchMode { Any, Specific }
    public enum RegionType { Any, AsteroidBelt, GasGiant }

    // Beispiel-Enum – passe an deine Materialliste an
    public enum ResourceKind { Iron, Ice, Nickel, Silicates, Hydrogen, Helium }

    [Serializable]
    public class MiningTask
    {
        public string TaskId;                   // GUID
        public string Name;                     // vom Spieler vergeben
        public SearchMode Mode;
        public RegionType RegionPreference;
        public HashSet<ResourceKind> Wanted;    // leer oder null bei Any
        public string DropoffHubId;
        public Transform DropoffHub;            // Sonde oder Fabrik
        public bool LoopUntilStopped = true;
        public float ScanRadiusUnits = 100000f;
        public float ReScanCooldownSec = 15f;
        public int PreferredMinerCount = 1;     // optional

        public MiningTask(string name)
        {
            TaskId = Guid.NewGuid().ToString("N");
            Name = string.IsNullOrWhiteSpace(name) ? $"Task-{TaskId[..6]}" : name.Trim();
            Wanted = new HashSet<ResourceKind>();
        }

        public override string ToString()
        {
            var wanted = (Mode == SearchMode.Any || Wanted == null || Wanted.Count == 0)
                ? "Beliebig"
                : string.Join(",", Wanted);
            return $"{Name} • {Mode} • {RegionPreference} • {wanted}";
        }
    }
}
