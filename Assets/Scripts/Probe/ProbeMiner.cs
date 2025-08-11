using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(ProbeInventory))]
public class ProbeMiner : MonoBehaviour
{
    public Key mineKey = Key.M;

    //HUDControllerModular hud;          // liefert Item der Scan-Liste
    ProbeInventory cargo;

    MineableAsteroid target;
    bool isMining;

    public event Action StatusUpdated;
    public string StatusText;

    void Awake()
    {
        //hud = FindFirstObjectByType<HUDControllerModular>();
        cargo = GetComponent<ProbeInventory>();
    }

    void Update()
    {
        HandleInput();
        if (isMining) DoMining();
    }

    /* -------------------------------------------------- */

    void HandleInput()
    {
        if (!Keyboard.current[mineKey].wasPressedThisFrame) return;

        // Taste gedrückt ⇒ Mining toggeln
        if (isMining) { StopMining(); return; }
        StartMining();

    }

    void StopMining()            // zentraler „Aus‐Schalter“
    {
        StatusText = "Mining stopped";
        isMining = false;
        target = null;
        StatusUpdated?.Invoke();
    }


    public void StartMining()
    {
        var sel = GetComponent<ProbeController>().navTarget;
        StatusText = "Mining started";
        StatusUpdated?.Invoke();
        if (sel != null && sel.TryGetComponent(out MineableAsteroid ast))
        {
            Debug.Log($"Mining {ast.name} ({ast.materialId})");
            target = ast;
            isMining = true;
        }
    }
    /* -------------------------------------------------- */
    void DoMining()
    {
        if (target == null) { StopMining(); return; }
        StatusText = "Mining in progress";
        var def = MaterialRegistry.Get(target.materialId);
        // a) max. was Material hergibt
        float uMat = def.mineRate * Time.deltaTime;

        // b) max. was Laderaum zulässt
        float uVol = cargo.FreeVolume / def.volumePerUnit;

        // c) max. was noch im Asteroiden steckt
        float unitsWanted = Mathf.Min(uMat, uVol, target.UnitsRemaining);

        if (unitsWanted <= 0f) { StopMining(); return; }

        float removed = target.RemoveUnits(unitsWanted);
        cargo.Add(def.id, removed);

        // Ziel evtl. fertig?
        if (target == null || target.Equals(null))
            StopMining();
        StatusUpdated?.Invoke();
    }

    public void SetTarget(MineableAsteroid newTarget)
    {
        if (newTarget == null || newTarget.Equals(null))
        {
            StopMining();
            return;
        }
        target = newTarget;
        isMining = true;
        StatusText = "Mining target set";
        StatusUpdated?.Invoke();
        // Optional: Update HUD or other UI elements
        //hud?.UpdateMiningStatus($"Mining {target.name} ({target.materialId})");
    }

}
