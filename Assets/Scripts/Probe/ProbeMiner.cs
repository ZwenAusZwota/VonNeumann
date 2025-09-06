using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(InventoryController))]
public class ProbeMiner : MonoBehaviour
{
    public Key mineKey = Key.M;

    InventoryController cargo;

    MineableAsteroid target;
    bool isMining;

    public event Action StatusUpdated;
    public string StatusText;

    void Awake()
    {
        cargo = GetComponent<InventoryController>();
    }

    void Update()
    {
        HandleInput();
        if (isMining) DoMining();
    }

    void HandleInput()
    {
        if (!Keyboard.current[mineKey].wasPressedThisFrame) return;

        if (isMining) { StopMining(); return; }
        StartMining();
    }

    void StopMining()
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

            // NEU: beim Start des Minings an die Oberfläche „anlegen“
            var ap = GetComponent<ProbeAutopilot>();
            if (ap != null) ap.SetSurfaceContact(ast.transform);

            isMining = true;
        }
    }

    void DoMining()
    {
        if (target == null) { StopMining(); return; }
        StatusText = "Mining in progress";
        var def = MaterialDatabase.Get(target.materialId);

        float uMat = def.mineRate * Time.deltaTime;
        float uVol = cargo.FreeVolume / def.volumePerUnit;
        float unitsWanted = Mathf.Min(uMat, uVol, target.UnitsRemaining);

        if (unitsWanted <= 0f) { StopMining(); return; }

        float removed = target.RemoveUnits(unitsWanted);
        cargo.Add(def.id, removed);

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
    }
}
