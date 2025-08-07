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
    //enum AutoState { None, Align, SpiralApproach, Orbit }

    Transform navTarget;
    Transform lastTarget;

    Vector2 rotateInput;
    float rollInput;
    float thrustInput;

    Vector3 localDegPerSec;
    bool isDampingReset;

    float desiredOrbitRadius;
    Vector3 orbitPlaneNormal;

    /* ───────────── New runtime helpers */
    Vector3 _prevPos;
    Vector3 _lastMove;              // delta from previous FixedUpdate
    public float CurrentSpeed { get; private set; }
    public int Distance { get; private set; }

    float radialSpeed;            // Units / s (for spiral‑approach)
    bool isBraking;              // true while emergency brake active

    /*────────────────────────────────────────── Cached references */
    ProbeControls controls;
    Rigidbody rb;
    PlanetRegistry registry;
    HUDControllerModular hud;
    ProbeAutopilot autopilot;

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
        controls = new ProbeControls();
        autopilot = GetComponent<ProbeAutopilot>();

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
        Vector3 before = transform.position;

        /* Target changed while AP active ⇒ abort */
        //if (autoState != AutoState.None && navTarget != lastTarget)
        //{
        //    AbortAutopilot(keepMomentum: true);
        //    return;
        //}
        lastTarget = navTarget;

        //switch (autoState)
        //{
        //    case AutoState.Align: AlignTick(); break;
        //    case AutoState.SpiralApproach: SpiralTick(); break;
        //    case AutoState.Orbit: OrbitTick(); break;
        //    default: ManualFlightTick(); break;
        //}

        /* ----- Geschwindigkeit errechnen ----- */
        Vector3 delta =  transform.position - before;
        CurrentSpeed = delta.magnitude / Time.fixedDeltaTime;
        _lastMove = delta;


        /* ----- Entfernung zum Ziel befüllen ----- */
        if (navTarget != null)
            Distance = Mathf.RoundToInt((navTarget.position - transform.position).magnitude);
        else
            Distance = 0;

        /* PhysX‑Velocity nur aktualisieren, wenn Body dynamisch */
        if (!rb.isKinematic)
            rb.linearVelocity = delta / Time.fixedDeltaTime;

        _prevPos = before;
    }
    #endregion

    #region Autopilot
    public void SetNavTarget(Transform tgt) => autopilot.SetNavTarget(tgt);
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

    ///*====================================================================*/
    //#region Autopilot – public API
    //public void SetNavTarget(Transform tgt)
    //{
    //    navTarget = tgt;
    //}

    //public void StartAutopilot()
    //{
    //    if (navTarget == null) return;

    //    /* Geschwindigkeit nullen solange noch dynamisch */
    //    rb.linearVelocity = rb.angularVelocity = Vector3.zero;
    //    rb.isKinematic = true;          // PhysX aus

    //    autoState = AutoState.Align;
    //    isDampingReset = true;
    //    isBraking = false;

    //    AutoPilotStarted?.Invoke();
    //}

    //public void StopAutopilot() => AbortAutopilot(keepMomentum: false);
    //public bool IsAutopilotActive => autoState != AutoState.None;
    //#endregion

    ///*====================================================================*/
    //#region Autopilot – alignment (unchanged)
    //void AlignTick()
    //{
    //    if (navTarget == null) { AbortAutopilot(true); return; }

    //    Vector3 dirWorld = (navTarget.position - transform.position).normalized;
    //    Quaternion targetRot = Quaternion.LookRotation(dirWorld, Vector3.up);
    //    transform.rotation = Quaternion.RotateTowards(
    //        transform.rotation,
    //        targetRot,
    //        alignDegPerSec * Time.fixedDeltaTime);

    //    if (Quaternion.Angle(transform.rotation, targetRot) <= alignToleranceDeg)
    //        StartSpiralApproach();
    //}
    //#endregion

    ///*====================================================================*/
    //#region Autopilot – spiral-in approach (unchanged except radialSpeed reset)
    //void StartSpiralApproach()
    //{
    //    if (navTarget == null) { AbortAutopilot(true); return; }

    //    if (navTarget.CompareTag("AsteroidBelt"))
    //    {
    //        AsteroidBelt targetBelt = navTarget.GetComponent<AsteroidBelt>();
    //        desiredOrbitRadius = targetBelt.outerRadius;      // stop at belt edge
    //    }
    //    else
    //    {
    //        float bodyRadiusUnits = GuessBodyRadius(navTarget);
    //        desiredOrbitRadius = Mathf.Max(bodyRadiusUnits * orbitAltitudeFactor,
    //                                       minOrbitAltitudeUnits);
    //    }

    //    radialSpeed = 0f;
    //    autoState = AutoState.SpiralApproach;
    //}

    //void SpiralTick()
    //{
    //    if (navTarget == null) { AbortAutopilot(true); return; }

    //    Vector3 tgtPos = navTarget.position;
    //    Vector3 radial = transform.position - tgtPos;
    //    float dist = radial.magnitude;
    //    float dt = Time.fixedDeltaTime;

    //    /* 1) tangentiale Drehung */
    //    Vector3 planeNormal = Vector3.up;
    //    transform.RotateAround(tgtPos, planeNormal, OrbitDegPerSec * dt);

    //    /* 2) radiales Dreiecksprofil */
    //    float remaining = dist - desiredOrbitRadius;
    //    float stopDist = (radialSpeed * radialSpeed) / (2f * radialAccel);

    //    radialSpeed += (stopDist >= remaining ? -radialAccel : radialAccel) * dt;
    //    radialSpeed = Mathf.Max(radialSpeed, 0f);

    //    float move = radialSpeed * dt;
    //    float newDist = Mathf.Max(dist - move, desiredOrbitRadius);

    //    /* 3) neue Position & Ausrichtung */
    //    Vector3 newRadial = (transform.position - tgtPos).normalized;
    //    transform.position = tgtPos + newRadial * newDist;
    //    transform.rotation = Quaternion.LookRotation(-newRadial, Vector3.up);

    //    /* 4) Stop / Orbit transition */
    //    if (!navTarget.CompareTag("Planet") && newDist <= desiredOrbitRadius + 0.5f)
    //    {
    //        AbortAutopilot(true);          // Gürtel‑Rand erreicht
    //        return;
    //    }

    //    if (navTarget.CompareTag("Planet") && newDist <= desiredOrbitRadius + 1e-3f)
    //    {
    //        Vector3 tangent = Vector3.Cross(planeNormal, newRadial).normalized;
    //        orbitPlaneNormal = Vector3.Cross(newRadial, tangent).normalized;
    //        if (orbitPlaneNormal.sqrMagnitude < 1e-6f) orbitPlaneNormal = Vector3.up;

    //        autoState = AutoState.Orbit;
    //        transform.rotation = Quaternion.LookRotation(tangent, orbitPlaneNormal);
    //        radialSpeed = 0f;
    //    }
    //}
    //#endregion

    ///*====================================================================*/
    //#region Autopilot – orbit (unchanged)
    //void OrbitTick()
    //{
    //    if (navTarget == null) { AbortAutopilot(true); return; }

    //    Vector3 tgtPos = navTarget.position;
    //    transform.RotateAround(tgtPos, orbitPlaneNormal, OrbitDegPerSec * Time.fixedDeltaTime);

    //    Vector3 radial = (transform.position - tgtPos).normalized;
    //    transform.position = tgtPos + radial * desiredOrbitRadius;

    //    Vector3 dirTangent = Vector3.Cross(orbitPlaneNormal, radial).normalized;
    //    if (dirTangent.sqrMagnitude > 1e-6f)
    //        transform.rotation = Quaternion.LookRotation(dirTangent, orbitPlaneNormal);
    //}
    //#endregion

    /*====================================================================*/
    #region Helpers & reset / abort

    void ResetProbe()
    {
        //hud?.SetActiveBody("");
        rb.isKinematic = false;
        rb.linearVelocity = rb.angularVelocity = Vector3.zero;

        //autoState = AutoState.None;
        isDampingReset = true;

        AutoPilotStopped?.Invoke();
    }

    void HandleMinusKey()
    {
        /* Autopilot aktiv? ⇒ mit Schwung verlassen & bremsen */
        //if (autoState != AutoState.None)
        //{
        //    AbortAutopilot(keepMomentum: true);
        //    isBraking = true;       // sofort Bremsmodus aktivieren
        //}
        //else if (!isBraking)        // manuelles Not‑Bremsen
        //{
        //    isBraking = true;
        //}

        ResetProbe();
        autopilot.AbortAutopilot(keepMomentum: true);
    }

    //void AbortAutopilot(bool keepMomentum)
    //{
    //    /* 1) aktuelle Transl.-Geschwindigkeit aus letztem Frame berechnen */
    //    Vector3 carriedVel = keepMomentum ? (_lastMove / Time.fixedDeltaTime) : Vector3.zero;

    //    /* 2) PhysX wieder einschalten */
    //    rb.isKinematic = false;
    //    rb.linearVelocity = carriedVel;
    //    rb.angularVelocity = Vector3.zero;

    //    /* 3) Zustand zurücksetzen */
    //    autoState = AutoState.None;
    //    radialSpeed = 0f;
    //    isDampingReset = true;

    //    AutoPilotStopped?.Invoke();
    //}

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
