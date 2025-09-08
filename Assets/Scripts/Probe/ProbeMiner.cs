using System;
using UnityEngine;

/// <summary>
/// Manuelles Mining der Sonde (über GameHotkeys getoggelt).
/// - Verwendet das Nav-Target vom ProbeAutopilot (oder notfalls ProbeController).
/// - Findet bei Belt-Targets automatisch den nächstgelegenen MineableAsteroid.
/// - Lagert in InventoryController ein und meldet HUD-Status über HUDMessageBus.
/// - Bietet StatusUpdated für bestehende ProbeController-Hooks.
/// </summary>
[RequireComponent(typeof(InventoryController))]
public class ProbeMiner : MonoBehaviour
{
    [Header("Förder-Parameter")]
    [Tooltip("Fallback-Miningrate in Einheiten/Sekunde, falls Material-Definition keine Rate liefert.")]
    public float defaultMineRate = 5f;

    private InventoryController cargo;
    private MineableAsteroid target;
    private bool isMining;

    public event Action StatusUpdated; // für ProbeController

    private void Awake()
    {
        cargo = GetComponent<InventoryController>();
    }

    private void Update()
    {
        if (isMining) DoMining();
    }

    public void ToggleMining()
    {
        if (isMining) StopMining();
        else StartMining();
    }

    public void StartMining()
    {
        if (isMining) return;

        var ast = ResolveCurrentAsteroidTarget();
        if (ast == null)
        {
            HUDMessageBus.Post("Kein abbaufähiges Ziel ausgewählt.");
            StatusUpdated?.Invoke();
            return;
        }

        if (ast.IsFullyMined)
        {
            HUDMessageBus.Post("Asteroid ist erschöpft.");
            StatusUpdated?.Invoke();
            return;
        }

        target = ast;

        // An Oberfläche „anlegen“, falls Autopilot vorhanden:
        var ap = GetComponent<ProbeAutopilot>();
        if (ap != null) { ap.SetSurfaceContact(ast.transform); } // hält die Sonde außen an der Nav-Sphäre
        try { target.StartMining(); } catch { /* robust bleiben */ }

        isMining = true;
        HUDMessageBus.Post("Mining gestartet");
        StatusUpdated?.Invoke();
    }

    public void StopMining()
    {
        if (!isMining) return;

        try { if (target != null) target.StopMining(); } catch { }

        isMining = false;
        target = null;

        HUDMessageBus.Post("Mining gestoppt");
        StatusUpdated?.Invoke();
    }

    private void DoMining()
    {
        if (target == null)
        {
            StopMining();
            return;
        }

        var def = MaterialDatabase.Get(target.MaterialId);
        if (def == null)
        {
            HUDMessageBus.Post("Unbekanntes Material – Mining abgebrochen.");
            StopMining();
            return;
        }

        float rate = def.mineRate > 0f ? def.mineRate : defaultMineRate;
        float unitsFromRate = rate * Time.deltaTime;

        float freeVol = cargo.FreeVolume;
        if (freeVol <= 0f)
        {
            HUDMessageBus.Post("Inventar voll – Mining gestoppt.");
            StopMining();
            return;
        }

        float unitsByCargo = freeVol / Mathf.Max(1e-6f, def.volumePerUnit);
        float unitsByAsteroid = target.UnitsRemaining;

        float unitsWanted = Mathf.Min(unitsFromRate, unitsByCargo, unitsByAsteroid);
        if (unitsWanted <= 0f)
        {
            StopMining();
            return;
        }

        float removed = target.RemoveUnits(unitsWanted);
        if (removed > 0f)
        {
            cargo.Add(def.id, removed);
            StatusUpdated?.Invoke();
        }
        else
        {
            //Wahrscheinlich erschöpft
            HUDMessageBus.Post("Asteroid erschöpft.");
            StopMining();
        }

        if (target.IsFullyMined)
        {
            HUDMessageBus.Post("Asteroid erschöpft.");
            StopMining();
        }
    }

    private MineableAsteroid ResolveCurrentAsteroidTarget()
    {
        // 1) Bevorzugt: Nav-Target aus ProbeAutopilot
        var ap = GetComponent<ProbeAutopilot>();
        Transform navT = ap != null ? ap.NavTarget : null;

        // 2) Fallback: ProbeController-navTarget (falls vorhanden)
        if (navT == null)
        {
            var pc = GetComponent<ProbeController>();
            if (pc != null) navT = pc.navTarget; // entspricht deiner bisherigen Logik
        }

        if (navT == null) return null;

        // a) Wenn ein AsteroidBelt anvisiert ist → nächsten Asteroiden holen
        var belt = navT.GetComponent<AsteroidBelt>();
        if (belt != null)
        {
            var closest = belt.GetClosestAsteroid(transform.position);
            if (closest != null)
            {
                // Root/Parent mit MineableAsteroid auflösen (High/Low LOD-Kinder etc.)
                var ma = closest.GetComponentInParent<MineableAsteroid>();
                if (ma != null) return ma;
            }
            return null; // Belt ohne Kind gefunden
        }

        // b) Direkt ein Asteroid(-Kind) als Target? → Parent prüfen
        var directMa = navT.GetComponentInParent<MineableAsteroid>();
        if (directMa != null) return directMa;

        return null;
    }
}
