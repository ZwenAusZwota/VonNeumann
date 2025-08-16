// Assets/Scripts/Entities/ProbeController.cs
using System;
using UnityEngine;
using UnityEngine.InputSystem;

/* -----------------------------------------------------------------------------
 * ProbeController – v2.4.0 (2025‑08‑04)
 * ---------------------------------------------------------------------------
 * + NEW: Autopilot abort keeps translational velocity (momentum‑handover).
 * + NEW: Emergency brake (Numpad [-]) – high deceleration until stand‑still.
 * ---------------------------------------------------------------------------*/

[RequireComponent(typeof(Rigidbody))]
public class ProbeController : MonoBehaviour
{
    /*─────────────────────────────── Manual Flight Settings */
    [Header("Flight – Manual")]
    [Tooltip("Forward thrust in Unity units per second² (m/s²).")]
    public float thrustPower = 5f;
    public float maxDegPerSec = 90f;
    public float accelDegPerSec2 = 180f;
    public float resetDecelDeg2 = 360f;

    /*─────────────────────────────── Emergency Brake */
    [Header("Emergency Brake (Numpad [-])")]
    [Tooltip("Linear deceleration when emergency brake is engaged (Units / s²).")]
    public float brakeAccel = 50f;

    /*─────────────────────────────── Misc */
    [Header("Spawn & HUD")]
    public float spawnScale = 0.05f;

    /*────────────────────────────────────────── Runtime fields */
    public Transform navTarget;

    Vector2 rotateInput;
    float rollInput;
    float thrustInput;

    Vector3 localDegPerSec;
    bool isDampingReset;

    float desiredOrbitRadius;
    Vector3 orbitPlaneNormal;

    public float CurrentSpeed { get; private set; }
    public int Distance { get; private set; }
    private float _lastDistanceRaw = -1f;        // for speed calculation

    float radialSpeed;            // Units / s (for spiral‑approach)
    bool isBraking;              // true while emergency brake active

    /*────────────────────────────────────────── Cached references */
    InputController controls;
    //ProbeControls controls;
    Rigidbody rb;
    PlanetRegistry registry;
    HUDControllerModular hud;
    ProbeAutopilot autopilot;
    ProbeScanner scanner;
    ProbeMiner miner;
    ProbeInventory inventory;

    public event Action AutoPilotStarted;
    public event Action AutoPilotStopped;
    public event Action<string> StatusUpdated; // for HUD updates

    /*====================================================================*/
    #region Unity – initialisation
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        registry = PlanetRegistry.Instance;
        hud = FindFirstObjectByType<HUDControllerModular>();
        controls = new InputController();
        autopilot = GetComponent<ProbeAutopilot>();
        scanner = GetComponent<ProbeScanner>();
        miner = GetComponent<ProbeMiner>();
        inventory = GetComponent<ProbeInventory>();

        hud.SetProbe(this);

        transform.localScale = Vector3.one * spawnScale;
        rb.maxAngularVelocity = maxDegPerSec * Mathf.Deg2Rad * 1.5f;
    }

    void OnEnable()
    {
        var map = controls.Probe;
        map.Enable();

        map.Rotate.performed += ctx => rotateInput = ctx.ReadValue<Vector2>();
        map.Rotate.canceled += _ => rotateInput = Vector2.zero;

        map.Roll.performed += ctx => rollInput = ctx.ReadValue<float>();
        map.Roll.canceled += _ => rollInput = 0f;

        map.Thrust.performed += ctx => thrustInput = ctx.ReadValue<float>();
        map.Thrust.canceled += _ => thrustInput = 0f;

        /* legacy ‚reset‘ ⇒ bleibt erhalten */
        map.Reset.performed += _ => HandleMinusKey();

        autopilot.AutoPilotStarted += () => AutoPilotStarted?.Invoke();
        autopilot.AutoPilotStopped += () => AutoPilotStopped?.Invoke();
        autopilot.StatusUpdated += (status) => StatusUpdated?.Invoke(status);

        scanner.ScanUpdated += (scanObjects) =>
        {
            if (hud != null)
                hud.UpdateNearScan(scanObjects);
        };

        miner.StatusUpdated += () => 
        {
            if (hud != null) {
                hud.UpdateMiningStatus(miner.StatusText);
                //hud.UpdateCargoStatus(inventory.Cargo);
            }
        };

        inventory.CargoChanged += (used, max) =>
        {
            if (hud != null)
                hud.UpdateCargoStatus(used, max);
        };

    }

    void OnDisable()
    {
        controls.Disable();

        autopilot.AutoPilotStarted -= () => AutoPilotStarted?.Invoke();
        autopilot.AutoPilotStopped -= () => AutoPilotStopped?.Invoke();
        autopilot.StatusUpdated -= (status) => StatusUpdated?.Invoke(status);

    }
        #endregion

        /*====================================================================*/
        #region Update – manual autopilot trigger & minus‑key
        void Update()
    {
        var kbd = Keyboard.current;
        if (kbd == null) return;

        /* Autopilot start via [+] */
        if ((kbd.numpadPlusKey != null && kbd.numpadPlusKey.wasPressedThisFrame) ||
            (kbd[Key.NumpadPlus] != null && kbd[Key.NumpadPlus].wasPressedThisFrame))
            StartAutopilot();

        /* Emergency brake / Autopilot abort via [–] */
        if ((kbd.numpadMinusKey != null && kbd.numpadMinusKey.wasPressedThisFrame) ||
            (kbd[Key.NumpadMinus] != null && kbd[Key.NumpadMinus].wasPressedThisFrame))
            HandleMinusKey();
    }
    #endregion

    /*====================================================================*/
    #region FixedUpdate – master state machine
    void FixedUpdate()
    {
        if (navTarget == null)
        {
            Distance = 0;
            CurrentSpeed = 0f;
            _lastDistanceRaw = -1f;
            return;
        }

        // 1) Rohdistanz als float (ohne Runden)
        float distanceRaw = Vector3.Distance(navTarget.position, transform.position);

        // 2) Für HUD/Anzeige weiterhin gerundet (optional)
        Distance = Mathf.RoundToInt(distanceRaw);

        float delta = Mathf.Abs(distanceRaw - _lastDistanceRaw);

        // 3) Geschwindigkeit nur berechnen, wenn sich die Distanz merklich ändert
        if (delta > 0f)
        {
             CurrentSpeed = delta / Time.fixedDeltaTime; // Vorzeichen gibt Richtung an (annähern/entfernen)
        }
        else
        {
            // Erste Initialisierung – noch keine Geschwindigkeit ableitbar
            CurrentSpeed = 0f;
        }

        _lastDistanceRaw = distanceRaw;
    }
    #endregion

    #region Autopilot
    public void SetNavTarget(Transform tgt)
    {
        navTarget = tgt;
        autopilot.SetNavTarget(tgt);
    }
    public void StartAutopilot() => autopilot.StartAutopilot();
    public void StopAutopilot() => autopilot.StopAutopilot();
    public bool IsAutopilotActive => autopilot.IsAutopilotActive;
    #endregion

    /*====================================================================*/
    #region Manual flight & emergency brake
    void ManualFlightTick()
    {
        /* --- Emergency brake overrides manual input --- */
        if (isBraking)
        {
            ApplyEmergencyBrake();
            return;                       // keine weitere Steuerung während Bremse
        }

        /* --- regulärer manueller Flug --- */
        if (thrustInput > 0f)
            rb.AddForce(transform.forward * thrustPower * thrustInput, ForceMode.Acceleration);

        if (isDampingReset)
        {
            float step = resetDecelDeg2 * Time.fixedDeltaTime;
            localDegPerSec = Vector3.MoveTowards(localDegPerSec, Vector3.zero, step);
            if (localDegPerSec == Vector3.zero) isDampingReset = false;
        }
        else
        {
            Vector3 accelLocal = new Vector3(
                -rotateInput.y,
                 rotateInput.x,
                -rollInput) * accelDegPerSec2;

            localDegPerSec += accelLocal * Time.fixedDeltaTime;
            localDegPerSec.x = Mathf.Clamp(localDegPerSec.x, -maxDegPerSec, maxDegPerSec);
            localDegPerSec.y = Mathf.Clamp(localDegPerSec.y, -maxDegPerSec, maxDegPerSec);
            localDegPerSec.z = Mathf.Clamp(localDegPerSec.z, -maxDegPerSec, maxDegPerSec);
        }

        rb.angularVelocity = transform.TransformDirection(localDegPerSec * Mathf.Deg2Rad);
    }

    void ApplyEmergencyBrake()
    {
        Vector3 v = rb.linearVelocity;
        if (v.sqrMagnitude < 0.01f)
        {
            rb.linearVelocity = Vector3.zero;
            isBraking = false;
            isDampingReset = true;    // rotatorisch auslaufen lassen
            return;
        }

        rb.AddForce(-v.normalized * brakeAccel, ForceMode.Acceleration);
    }
    #endregion

   
    #region Helpers & reset / abort

    void ResetProbe()
    {
        rb.isKinematic = false;
        rb.linearVelocity = rb.angularVelocity = Vector3.zero;

        isDampingReset = true;

        AutoPilotStopped?.Invoke();
    }

    void HandleMinusKey()
    {
        ResetProbe();
        autopilot.AbortAutopilot(keepMomentum: true);
    }

    static float GuessBodyRadius(Transform body)
    {
        if (body.TryGetComponent<SphereCollider>(out var col))
            return body.lossyScale.x * col.radius;
        if (body.TryGetComponent<Renderer>(out var rend))
            return rend.bounds.extents.magnitude;
        return body.lossyScale.x * 0.5f;   // fallback
    }
    #endregion
}
