// Assets/Scripts/Entities/ProbeController.cs
using System;
using UnityEngine;
using UnityEngine.InputSystem;

/* -----------------------------------------------------------------------------
 * ProbeController – v2.3.0 (2025‑08‑03)
 * ---------------------------------------------------------------------------
 * + NEW: Stops automatically at the outer edge of an AsteroidBelt.
 * + NEW: Align phase (smooth rotation before spiral‑in).
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

    /*─────────────────────────────── Autopilot – Alignment */
    [Header("Autopilot – Alignment")]
    [Tooltip("Max angular speed during automatic alignment (deg / s).")]
    public float alignDegPerSec = 60f;

    [Tooltip("When remaining angle is below this threshold (deg), alignment is finished.")]
    public float alignToleranceDeg = 1f;

    /*─────────────────────────────── Autopilot – Orbit Capture */
    [Header("Autopilot – Orbit Capture")]
    [Tooltip("Orbit altitude as a multiple of the body's visual radius.")]
    [Range(1.05f, 10f)] public float orbitAltitudeFactor = 1.3f;

    [Tooltip("Absolute minimum orbit altitude in Unity units (safety for tiny bodies).")]
    public float minOrbitAltitudeUnits = 5f;

    [Tooltip("Fraction of remaining radial distance per second during approach.")]
    [Range(0.01f, 1f)] public float radialApproachFraction = 0.2f;

    [Tooltip("Time for one revolution once in orbit (seconds).")]
    public float orbitPeriod = 60f;

    /*─────────────────────────────── Misc */
    [Header("Spawn & HUD")]
    public float spawnScale = 0.05f;

    /*────────────────────────────────────────── Runtime fields */
    enum AutoState { None, Align, SpiralApproach, Orbit }

    AutoState autoState = AutoState.None;
    Transform navTarget;
    Transform lastTarget;

    Vector2 rotateInput;
    float rollInput;
    float thrustInput;

    Vector3 localDegPerSec;
    bool isDampingReset;

    float desiredOrbitRadius;
    Vector3 orbitPlaneNormal;

    /*────────────────────────────────────────── Cached references */
    ProbeControls controls;
    Rigidbody rb;
    PlanetRegistry registry;
    HUDController hud;

    public event Action AutoPilotStarted;
    public event Action AutoPilotStopped;

    /* helper */
    float OrbitDegPerSec => 360f / Mathf.Max(orbitPeriod, 1e-4f);

    /*====================================================================*/
    #region Unity – initialisation
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        registry = PlanetRegistry.Instance;
        hud = FindObjectOfType<HUDController>();
        controls = new ProbeControls();

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

        map.Reset.performed += _ => ResetProbe();
    }

    void OnDisable() => controls.Disable();
    #endregion

    /*====================================================================*/
    #region Update – manual autopilot trigger (legacy)
    void Update()
    {
        var kbd = Keyboard.current;
        if (kbd == null) return;

        /* Autopilot start via [+] – target is chosen through the HUD */
        if ((kbd.numpadPlusKey != null && kbd.numpadPlusKey.wasPressedThisFrame) ||
            (kbd[Key.NumpadPlus] != null && kbd[Key.NumpadPlus].wasPressedThisFrame))
            StartAutopilot();
    }
    #endregion

    /*====================================================================*/
    #region FixedUpdate – master state machine
    void FixedUpdate()
    {
        if (autoState != AutoState.None && navTarget != lastTarget)
        {
            ResetProbe();
            return;
        }
        lastTarget = navTarget;

        switch (autoState)
        {
            case AutoState.Align: AlignTick(); break;
            case AutoState.SpiralApproach: SpiralTick(); break;
            case AutoState.Orbit: OrbitTick(); break;
            default: ManualFlightTick(); break;
        }
    }
    #endregion

    /*====================================================================*/
    #region Manual flight
    void ManualFlightTick()
    {
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
    #endregion

    /*====================================================================*/
    #region Autopilot – public API
    public void SetNavTarget(Transform tgt)
    {
        navTarget = tgt;
        hud?.SetActiveBody(tgt ? tgt.name : "");
    }

    public void StartAutopilot()
    {
        if (navTarget == null) return;
        autoState = AutoState.Align;
        rb.isKinematic = true;          // suppress physics during auto‑phases
        rb.linearVelocity = rb.angularVelocity = Vector3.zero;
        isDampingReset = true;

        // Prepare belt detection
        //targetBelt = navTarget.GetComponent<AsteroidBelt>();

        AutoPilotStarted?.Invoke();
    }

    public void StopAutopilot() => ResetProbe();
    public bool IsAutopilotActive => autoState != AutoState.None;
    #endregion

    /*====================================================================*/
    #region Autopilot – alignment
    void AlignTick()
    {
        if (navTarget == null) { ResetProbe(); return; }

        Vector3 dirWorld = (navTarget.position - transform.position).normalized;
        Quaternion targetRot = Quaternion.LookRotation(dirWorld, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            alignDegPerSec * Time.fixedDeltaTime);

        float angle = Quaternion.Angle(transform.rotation, targetRot);
        if (angle <= alignToleranceDeg)
            StartSpiralApproach();
    }
    #endregion

    /*====================================================================*/
    #region Autopilot – spiral‑in approach
    void StartSpiralApproach()
    {
        if (navTarget == null) { ResetProbe(); return; }


        if (navTarget.CompareTag("AsteroidBelt")) {
            AsteroidBelt targetBelt = navTarget.GetComponent<AsteroidBelt>();
            desiredOrbitRadius = targetBelt.outerRadius;      // stop at belt edge
        }
        else
        {
            float bodyRadiusUnits = GuessBodyRadius(navTarget);
            desiredOrbitRadius = Mathf.Max(bodyRadiusUnits * orbitAltitudeFactor,
                                           minOrbitAltitudeUnits);
        }

        autoState = AutoState.SpiralApproach;
    }

    void SpiralTick()
    {
        if (navTarget == null) { ResetProbe(); return; }

        Vector3 tgtPos = navTarget.position;
        Vector3 radial = transform.position - tgtPos;
        float dist = radial.magnitude;

        Vector3 planeNormal = Vector3.up;
        transform.RotateAround(tgtPos, planeNormal, OrbitDegPerSec * Time.fixedDeltaTime);

        float shrink = dist * radialApproachFraction * Time.fixedDeltaTime;
        float newDist = Mathf.Max(dist - shrink, desiredOrbitRadius);
        Vector3 newRadial = (transform.position - tgtPos).normalized;
        transform.position = tgtPos + newRadial * newDist;
        transform.rotation = Quaternion.LookRotation(-newRadial, Vector3.up);

        /* -------- Stop at belt edge -------- */
        if (!navTarget.CompareTag("Planet") && newDist <= desiredOrbitRadius + 0.5f)
        {
            ResetProbe();                // full stop & exit autopilot
            return;
        }

        /* -------- Transition to orbit -------- */
        if (navTarget.CompareTag("Planet") && newDist <= desiredOrbitRadius + 1e-3f)
        {
            Vector3 tangent = Vector3.Cross(planeNormal, newRadial).normalized;
            orbitPlaneNormal = Vector3.Cross(newRadial, tangent).normalized;
            if (orbitPlaneNormal.sqrMagnitude < 1e-6f) orbitPlaneNormal = Vector3.up;

            autoState = AutoState.Orbit;
            transform.rotation = Quaternion.LookRotation(tangent, orbitPlaneNormal);
        }
    }
    #endregion

    /*====================================================================*/
    #region Autopilot – orbit
    void OrbitTick()
    {
        if (navTarget == null) { ResetProbe(); return; }

        Vector3 tgtPos = navTarget.position;
        transform.RotateAround(tgtPos, orbitPlaneNormal, OrbitDegPerSec * Time.fixedDeltaTime);

        Vector3 radial = (transform.position - tgtPos).normalized;
        transform.position = tgtPos + radial * desiredOrbitRadius;

        Vector3 dirTangent = Vector3.Cross(orbitPlaneNormal, radial).normalized;
        if (dirTangent.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(dirTangent, orbitPlaneNormal);
    }
    #endregion

    /*====================================================================*/
    #region Helpers & reset
    void ResetProbe()
    {
        hud?.SetActiveBody("");
        rb.isKinematic = false;
        rb.linearVelocity = rb.angularVelocity = Vector3.zero;

        autoState = AutoState.None;
        isDampingReset = true;

        AutoPilotStopped?.Invoke();
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
