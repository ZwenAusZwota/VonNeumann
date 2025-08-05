// Assets/Scripts/Entities/ProbeAutopilot.cs
using System;
using UnityEngine;

/* -----------------------------------------------------------------------------
 * ProbeAutopilot – v1.0.0  (2025‑08‑04)
 * ---------------------------------------------------------------------------
 * Extracted from ProbeController v2.4.0.
 * Handles all automatic navigation: alignment → spiral‑approach → orbit.
 * Works alongside ProbeController, which now manages only manual flight
 * and the emergency brake.
 * ---------------------------------------------------------------------------*/

[RequireComponent(typeof(Rigidbody))]
public class ProbeAutopilot : MonoBehaviour
{

    /*─────────────────────────────── Autopilot – Alignment */
    [Header("Autopilot – Alignment")]
    [Tooltip("Max angular speed during automatic alignment (deg / s).")]
    public float alignDegPerSec = 60f;

    [Tooltip("When remaining angle is below this threshold (deg), alignment is finished.")]
    public float alignToleranceDeg = 1f;

    /*─────────────────────────────── Autopilot – Approach */
    [Header("Autopilot – Approach")]
    [Tooltip("Radial acceleration during spiral‑approach (Units / s²).")]
    public float radialAccel = 0.5f;      // Feintuning im Inspector

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

    /*──────────────────────────── Runtime */
    enum AutoState { None, Align, SpiralApproach, Orbit }
    AutoState autoState = AutoState.None;

    Transform navTarget;
    Transform lastTarget;

    float desiredOrbitRadius;
    Vector3 orbitPlaneNormal;

    Vector3 _prevPos;
    Vector3 _lastMove;
    float radialSpeed;

    /*──────────────────────────── Cached */
    Rigidbody rb;
    PlanetRegistry registry;

    /*──────────────────────────── Events */
    public event Action AutoPilotStarted;
    public event Action AutoPilotStopped;

    /* helper */
    float OrbitDegPerSec => 360f / Mathf.Max(orbitPeriod, 1e-4f);

    /*====================================================================*/
    #region Unity – init
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        registry = PlanetRegistry.Instance;
    }
    #endregion

    /*====================================================================*/
    #region FixedUpdate – State‑Machine
    void FixedUpdate()
    {
        if (autoState == AutoState.None) return;

        Vector3 before = transform.position;

        /* Target changed while AP active ⇒ abort */
        if (navTarget != lastTarget)
        {
            AbortAutopilot(keepMomentum: true);
            return;
        }
        lastTarget = navTarget;

        switch (autoState)
        {
            case AutoState.Align: AlignTick(); break;
            case AutoState.SpiralApproach: SpiralTick(); break;
            case AutoState.Orbit: OrbitTick(); break;
        }

        /* Geschwindigkeits‑Delta für Momentum‑Handover */
        _lastMove = transform.position - before;
        _prevPos = before;
    }
    #endregion

    /*====================================================================*/
    #region Public API
    public void SetNavTarget(Transform tgt) => navTarget = tgt;

    public void StartAutopilot()
    {
        if (navTarget == null || autoState != AutoState.None) return;

        rb.linearVelocity = rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true; // Physics off during autopilot phases

        autoState = AutoState.Align;
        radialSpeed = 0f;

        AutoPilotStarted?.Invoke();
    }

    public void StopAutopilot() => AbortAutopilot(false);

    public bool IsAutopilotActive => autoState != AutoState.None;
    #endregion

    /*====================================================================*/
    #region Alignment
    void AlignTick()
    {
        if (navTarget == null) { AbortAutopilot(true); return; }

        Vector3 dirWorld = (navTarget.position - transform.position).normalized;
        Quaternion targetRot = Quaternion.LookRotation(dirWorld, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            alignDegPerSec * Time.fixedDeltaTime);

        if (Quaternion.Angle(transform.rotation, targetRot) <= alignToleranceDeg)
            StartSpiralApproach();
    }
    #endregion

    /*====================================================================*/
    #region Spiral‑in Approach (triangular radial profile)
    void StartSpiralApproach()
    {
        if (navTarget == null) { AbortAutopilot(true); return; }

        if (navTarget.CompareTag("AsteroidBelt"))
        {
            AsteroidBelt belt = navTarget.GetComponent<AsteroidBelt>();
            desiredOrbitRadius = belt.outerRadius; // stop at belt edge
        }
        else
        {
            float bodyRadiusUnits = GuessBodyRadius(navTarget);
            desiredOrbitRadius = Mathf.Max(bodyRadiusUnits * orbitAltitudeFactor,
                                           minOrbitAltitudeUnits);
        }

        radialSpeed = 0f;
        autoState = AutoState.SpiralApproach;
    }

    void SpiralTick()
    {
        if (navTarget == null) { AbortAutopilot(true); return; }

        Vector3 tgtPos = navTarget.position;
        Vector3 radial = transform.position - tgtPos;
        float dist = radial.magnitude;
        float dt = Time.fixedDeltaTime;

        /* 1) tangential rotation around target */
        Vector3 planeNormal = Vector3.up;
        transform.RotateAround(tgtPos, planeNormal, OrbitDegPerSec * dt);

        /* 2) triangular radial profile (accelerate → decelerate) */
        float remaining = dist - desiredOrbitRadius;
        float stopDist = (radialSpeed * radialSpeed) / (2f * radialAccel);

        radialSpeed += (stopDist >= remaining ? -radialAccel : radialAccel) * dt;
        radialSpeed = Mathf.Max(radialSpeed, 0f);

        float move = radialSpeed * dt;
        float newDist = Mathf.Max(dist - move, desiredOrbitRadius);

        /* 3) new position & orientation */
        Vector3 newRadial = (transform.position - tgtPos).normalized;
        transform.position = tgtPos + newRadial * newDist;
        transform.rotation = Quaternion.LookRotation(-newRadial, Vector3.up);

        /* 4) Stop / Orbit transition */
        if (!navTarget.CompareTag("Planet") && newDist <= desiredOrbitRadius + 0.5f)
        {
            AbortAutopilot(true); // belt edge reached
            return;
        }

        if (navTarget.CompareTag("Planet") && newDist <= desiredOrbitRadius + 1e-3f)
        {
            Vector3 tangent = Vector3.Cross(planeNormal, newRadial).normalized;
            orbitPlaneNormal = Vector3.Cross(newRadial, tangent).normalized;
            if (orbitPlaneNormal.sqrMagnitude < 1e-6f) orbitPlaneNormal = Vector3.up;

            autoState = AutoState.Orbit;
            transform.rotation = Quaternion.LookRotation(tangent, orbitPlaneNormal);
            radialSpeed = 0f;
        }
    }
    #endregion

    /*====================================================================*/
    #region Orbit
    void OrbitTick()
    {
        if (navTarget == null) { AbortAutopilot(true); return; }

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
    #region Abort / Helpers
    void AbortAutopilot(bool keepMomentum)
    {
        Vector3 carriedVel = keepMomentum ? (_lastMove / Time.fixedDeltaTime) : Vector3.zero;

        rb.isKinematic = false;
        rb.linearVelocity = carriedVel;
        rb.angularVelocity = Vector3.zero;

        autoState = AutoState.None;
        radialSpeed = 0f;

        AutoPilotStopped?.Invoke();
    }

    //static float GuessBodyRadius(Transform body)
    //{
    //    if (body.TryGetComponent<SphereCollider>(out var col))
    //        return body.lossyScale.x * col.radius;
    //    if (body.TryGetComponent<Renderer>(out var rend))
    //        return rend.bounds.extents.magnitude;
    //    return body.lossyScale.x * 0.5f; // fallback
    //}
    #endregion
}
