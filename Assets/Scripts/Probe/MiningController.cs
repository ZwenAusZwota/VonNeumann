using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(ProbeInventory))]
public class MiningController : MonoBehaviour
{
    public Key mineKey = Key.M;

    HUDControllerModular hud;          // liefert Item der Scan-Liste
    ProbeInventory cargo;

    MineableAsteroid target;
    bool isMining;

    void Awake()
    {
        hud = FindFirstObjectByType<HUDControllerModular>();
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
        isMining = false;
        target = null;
    }


    public void StartMining()
    {
        /*var sel = hud?.CurrentSelection?.GameObject;
        if (sel != null && sel.TryGetComponent(out MineableAsteroid ast))
        {
            Debug.Log($"Mining {ast.name} ({ast.materialId})");
            target = ast;
            isMining = true;
        }*/
    }
    /* -------------------------------------------------- */
    void DoMining()
    {
        if (target == null) { StopMining(); return; }

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
    }
}
