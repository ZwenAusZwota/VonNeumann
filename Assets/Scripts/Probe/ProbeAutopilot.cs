// Assets/Scripts/Entities/ProbeAutopilot.cs
using System;
using UnityEngine;

/* -----------------------------------------------------------------------------
 * ProbeAutopilot – v1.3.0  (2025‑08‑09)
 * -----------------------------------------------------------------------------
 * - Kleinere Planeten-Orbitradien (reduzierter Altitude-Faktor & Min-Höhe).
 * - Separater Anflugmodus für einzelne Asteroiden (kein Orbit, definierte Nähe).
 * - Fix: Rigidbody.velocity statt linearVelocity.
 * - Belt: Ausrichtung & dynamisches Nachführen des nächstgelegenen Kantenpunkts.
 * ---------------------------------------------------------------------------*/

[RequireComponent(typeof(Rigidbody))]
public class ProbeAutopilot : MonoBehaviour
{
    /*─────────────────────────────── Autopilot – Alignment */
    [Header("Autopilot – Alignment")]
    [Tooltip("Maximale Winkelgeschwindigkeit beim Ausrichten (deg/s).")]
    public float alignDegPerSec = 60f;

    [Tooltip("Wenn der Restwinkel unter diesen Wert fällt (deg), ist Alignment fertig.")]
    public float alignToleranceDeg = 1f;

    /*─────────────────────────────── Autopilot – Approach */
    [Header("Autopilot – Approach")]
    [Tooltip("Radiale Beschleunigung beim Spiral-Approach (Units/s²).")]
    public float radialAccel = 0.5f;

    [Tooltip("Unbenutzt (Reserviert für spätere Kurven-Profile).")]
    [Range(0.01f, 1f)] public float radialApproachFraction = 0.2f;

    /*─────────────────────────────── Autopilot – Orbit Capture (Planeten) */
    [Header("Autopilot – Orbit (Planeten)")]
    [Tooltip("Orbit-Höhe relativ zum Körperradius.")]
    [Range(1.02f, 5f)] public float orbitAltitudeFactor = 1.1f;   // reduziert

    [Tooltip("Absolute Mindest-Orbit-Höhe (Units).")]
    public float minOrbitAltitudeUnits = 2f;                       // geringer

    [Tooltip("Umlaufdauer im Orbit (Sekunden).")]
    public float orbitPeriod = 60f;

    /*─────────────────────────────── Autopilot – Asteroid Approach */
    [Header("Autopilot – Asteroid Approach")]
    [Tooltip("Zielabstand zur Oberfläche eines Einzel-Asteroiden (Units).")]
    public float asteroidApproachDistance = 8f;

    [Tooltip("Toleranz zum Erreichen des Asteroiden-Zielabstands (Units).")]
    public float asteroidStopTolerance = 0.5f;

    /*──────────────────────────── Runtime */
    enum AutoState { None, Align, SpiralApproach, DirectApproach, Orbit }
    AutoState autoState = AutoState.None;

    Transform navTarget;
    Transform lastTarget;

    float desiredOrbitRadius;        // Zielabstand vom Zielzentrum
    Vector3 orbitPlaneNormal;

    Vector3 _prevPos;
    Vector3 _lastMove;
    float radialSpeed;

    // Belt-bezogene Felder
    private Vector3 _beltAimPoint;   // nächster Punkt auf inner/outer
    private bool _hasBeltAimPoint = false;

    private int _beltAimRecalcCounter = 0;
    [Tooltip("Alle X FixedUpdates Belt-Zielpunkt neu bestimmen.")]
    public int beltAimRecalcEvery = 10;

    // Direct-flight Halfway Profile
    bool _directInit = false;
    bool _directDecelPhase = false;
    float _directInitialBoundary = 0f;
    float _directHalfBoundary = 0f;


    /*──────────────────────────── Cached */
    Rigidbody rb;
    PlanetRegistry registry;

    /*──────────────────────────── Events */
    public event Action AutoPilotStarted;
    public event Action AutoPilotStopped;
    public event Action<string> StatusUpdated;

    /* helper */
    float OrbitDegPerSec => 360f / Mathf.Max(orbitPeriod, 1e-4f);

    /*====================================================================*/

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        registry = PlanetRegistry.Instance;
    }

    void OnEnable()
    {
        HUDBindingService.NavTargetSelected += OnHudNavTargetSelected; // neu
    }

    void OnDisable()
    {
        HUDBindingService.NavTargetSelected -= OnHudNavTargetSelected; // neu
    }

    private void OnHudNavTargetSelected(GameObject contextObject, Transform target)
    {
        // Nur reagieren, wenn diese Sonde im HUD gebunden/aktiv ist
        if (contextObject == this.gameObject && target != null)
        {
            SetNavTarget(target); // nur Ziel setzen; StartAutopilot bleibt weiter in deiner Kontrolle
            StatusUpdated?.Invoke($"Nav target set to {target.name}");
        }
    }

    /*====================================================================*/
    #region FixedUpdate – State-Machine
    void FixedUpdate()
    {
        if (autoState == AutoState.None) return;

        Vector3 before = transform.position;

        // Target-Änderung während AP aktiv → sauber abbrechen (Momentum übernehmen)
        if (lastTarget != null && navTarget != lastTarget)
        {
            Debug.LogWarning($"Autopilot: Target changed from {lastTarget.name} to {navTarget?.name}. Aborting.");
            AbortAutopilot(keepMomentum: true);
            return;
        }

        switch (autoState)
        {
            case AutoState.Align: AlignTick(); break;
            case AutoState.SpiralApproach: SpiralTick(); break;
            case AutoState.DirectApproach: DirectTick(); break;
            case AutoState.Orbit: OrbitTick(); break;
        }

        _lastMove = transform.position - before;
        _prevPos = before;
    }
    #endregion

    /*====================================================================*/
    #region Public API
    public void SetNavTarget(Transform tgt)
    {
        navTarget = tgt;
        AbortAutopilot(keepMomentum: true);
    }

    public void StartAutopilot()
    {
        if (navTarget == null || autoState != AutoState.None) return;

        // Physik anhalten, danach in kinematischen Modus wechseln
        //rb.linearVelocity = Vector3.zero;
        rb.linearVelocity = Vector3.zero;      // statt rb.linearVelocity
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        autoState = AutoState.Align;
        radialSpeed = 0f;
        lastTarget = navTarget;

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


        Debug.Log($"Aligning to {navTarget.name}");

        Vector3 dirWorld;

        if (navTarget.CompareTag("AsteroidBelt"))
        {
            var belt = navTarget.GetComponent<AsteroidBelt>();
            ComputeBeltNearestPoint(belt, transform.position, out _beltAimPoint, out desiredOrbitRadius);
            _hasBeltAimPoint = true;
            dirWorld = (_beltAimPoint - transform.position).normalized;
        }
        else if (navTarget.CompareTag("Asteroid"))
        {
            // Einzel-Asteroid: definierte Annäherung statt Orbit
            float bodyRadius = GuessBodyRadius(navTarget);
            float targetDistFromCenter = Mathf.Max(bodyRadius + asteroidApproachDistance, asteroidApproachDistance);
            desiredOrbitRadius = targetDistFromCenter;
            dirWorld = (navTarget.position - transform.position).normalized;
        }
        else
        {
            // Planet / generischer Körper → Orbit
            float bodyRadius = GuessBodyRadius(navTarget);
            desiredOrbitRadius = Mathf.Max(bodyRadius * orbitAltitudeFactor, bodyRadius + minOrbitAltitudeUnits);
            dirWorld = (navTarget.position - transform.position).normalized;
        }

        StatusUpdated?.Invoke("Aligning");

        if (dirWorld.sqrMagnitude < 1e-10f) dirWorld = transform.forward;
        Quaternion targetRot = Quaternion.LookRotation(dirWorld, Vector3.up);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            alignDegPerSec * Time.fixedDeltaTime);

        if (Quaternion.Angle(transform.rotation, targetRot) <= alignToleranceDeg)
        {
            StatusUpdated?.Invoke("Aligned");
            StartApproach();
        }
    }
    #endregion

    void StartApproach()
    {
        if (navTarget == null) { AbortAutopilot(true); return; }
        StatusUpdated?.Invoke("Approaching");
        // Asteroid-Belt: Spiral-Approach
        if (navTarget.CompareTag("AsteroidBelt"))
        {
            autoState = AutoState.DirectApproach;
            _beltAimRecalcCounter = 0;
        }
        // Einzel-Asteroid: Direkte Annäherung
        else if (navTarget.CompareTag("Asteroid"))
        {
            autoState = AutoState.DirectApproach;
            radialSpeed = 0f;
        }
        // Planet / generisches Orbital-Ziel: Orbit Capture
        else
        {
            autoState = AutoState.Orbit;
            radialSpeed = 0f;
        }

        if(autoState == AutoState.DirectApproach)
        {
            _directInit = false;
            _directDecelPhase = false;

        }
    }

    /*====================================================================*/
    #region Approach (dynamisch für Belts, Nähe für Asteroiden)

    void DirectTick()
    {
        if (navTarget == null) { AbortAutopilot(true); return; }
        StatusUpdated?.Invoke("Direct flight");

        float dt = Time.fixedDeltaTime;

        // --- Zielposition & Stop-Grenze bestimmen ---
        Vector3 targetPos = navTarget.position;
        float stopTol = asteroidStopTolerance;

        if (navTarget.CompareTag("AsteroidBelt"))
        {
            // Kantenpunkt initialisieren / periodisch nachführen
            if (!_hasBeltAimPoint)
            {
                var belt0 = navTarget.GetComponent<AsteroidBelt>();
                if (belt0 != null)
                {
                    ComputeBeltNearestPoint(belt0, transform.position, out _beltAimPoint, out desiredOrbitRadius);
                    _hasBeltAimPoint = true;
                    _beltAimRecalcCounter = 0;
                }
            }
            if (++_beltAimRecalcCounter % Mathf.Max(1, beltAimRecalcEvery) == 0)
            {
                var belt = navTarget.GetComponent<AsteroidBelt>();
                if (belt != null)
                {
                    ComputeBeltNearestPoint(belt, transform.position, out _beltAimPoint, out desiredOrbitRadius);
                }
            }
            targetPos = _beltAimPoint;
            stopTol = 0.5f; // knapp vor der Kante stoppen
        }

        // Richtung & Distanzen
        Vector3 toTarget = targetPos - transform.position;
        float centerDist = toTarget.magnitude;

        // Reststrecke bis zur Stop-Grenze je nach Zieltyp
        float boundaryNow;
        if (navTarget.CompareTag("AsteroidBelt"))
        {
            boundaryNow = Mathf.Max(0f, centerDist - stopTol);
        }
        else if (navTarget.CompareTag("Asteroid"))
        {
            float bodyRadius = GuessBodyRadius(navTarget);
            float desired = Mathf.Max(bodyRadius + asteroidApproachDistance, asteroidApproachDistance);
            boundaryNow = Mathf.Max(0f, centerDist - (desired + asteroidStopTolerance));
        }
        else
        {
            // Planeten sollten hier selten landen; Sicherheitslogik:
            float bodyRadius = GuessBodyRadius(navTarget);
            float desired = Mathf.Max(bodyRadius * orbitAltitudeFactor, bodyRadius + minOrbitAltitudeUnits);
            boundaryNow = Mathf.Max(0f, centerDist - (desired + 1e-3f));
        }

        // Halfway-Profil initialisieren
        if (!_directInit)
        {
            _directInit = true;
            _directDecelPhase = false;
            _directInitialBoundary = boundaryNow;
            _directHalfBoundary = 0.5f * _directInitialBoundary;
        }

        // Fortschritt messen und ggf. in Bremsphase wechseln (Sticky-Schalter)
        float progress = Mathf.Max(0f, _directInitialBoundary - boundaryNow);
        if (!_directDecelPhase && progress >= _directHalfBoundary)
            _directDecelPhase = true;

        // a) bis zur Hälfte beschleunigen, b) ab Hälfte bremsen
        if (!_directDecelPhase)
            radialSpeed += radialAccel * dt;
        else
            radialSpeed = Mathf.Max(0f, radialSpeed - radialAccel * dt);

        // Schrittweite begrenzen, damit wir die Stop-Grenze nicht überschießen
        float step = radialSpeed * dt;
        float advance = Mathf.Min(step, boundaryNow);

        if (advance > 0f && centerDist > 1e-6f)
        {
            Vector3 dir = toTarget / centerDist;
            transform.position += dir * advance;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        // Ziel-/Abschlussbedingungen
        if (navTarget.CompareTag("AsteroidBelt"))
        {
            if (Vector3.Distance(transform.position, targetPos) <= stopTol)
            {
                AbortAutopilot(false);
                return;
            }
        }
        else if (navTarget.CompareTag("Asteroid"))
        {
            float bodyRadius = GuessBodyRadius(navTarget);
            float desired = Mathf.Max(bodyRadius + asteroidApproachDistance, asteroidApproachDistance);
            if (Vector3.Distance(transform.position, navTarget.position) <= desired + asteroidStopTolerance)
            {
                AbortAutopilot(false);
                return;
            }
        }
        else
        {
            float bodyRadius = GuessBodyRadius(navTarget);
            float desired = Mathf.Max(bodyRadius * orbitAltitudeFactor, bodyRadius + minOrbitAltitudeUnits);
            if (Vector3.Distance(transform.position, navTarget.position) <= desired + 1e-3f)
            {
                orbitPlaneNormal = Vector3.up;
                autoState = AutoState.Orbit;
                radialSpeed = 0f;
                _directInit = false; _directDecelPhase = false;
                return;
            }
        }
    }



    void SpiralTick()
    {
        if (navTarget == null) { AbortAutopilot(true); return; }
        StatusUpdated?.Invoke("Approaching");
        Debug.Log($"Approaching to {navTarget.name}");

        Vector3 center = navTarget.position;

        // Belt: Zielpunkt periodisch nachführen
        if (navTarget.CompareTag("AsteroidBelt"))
        {
            if (++_beltAimRecalcCounter % Mathf.Max(1, beltAimRecalcEvery) == 0)
            {
                var belt = navTarget.GetComponent<AsteroidBelt>();
                if (belt != null)
                {
                    ComputeBeltNearestPoint(belt, transform.position, out _beltAimPoint, out desiredOrbitRadius);

                    center = _beltAimPoint;
                }
            }
        }

        // Radialdaten
        Vector3 radial = transform.position - center;
        float dist = radial.magnitude;
        float dt = Time.fixedDeltaTime;

        // (1) Tangentiale Bewegung für eine simple Spiralbahn (konst. Ebene)
        Vector3 planeNormal = Vector3.up;
        transform.RotateAround(center, planeNormal, OrbitDegPerSec * dt);

        // (2) Radialbeschleunigungs-Profil (Dreieck): beschleunigen/abbremsen
        float remaining = Mathf.Max(0f, dist - desiredOrbitRadius);
        float stopDist = (radialSpeed * radialSpeed) / Mathf.Max(2f * radialAccel, 1e-6f);
        radialSpeed += (stopDist >= remaining ? -radialAccel : radialAccel) * dt;
        radialSpeed = Mathf.Max(0f, radialSpeed);

        float move = radialSpeed * dt;
        float newDist = Mathf.Max(dist - move, desiredOrbitRadius);

        // (3) Neue Position entlang des Radials beibehalten
        Vector3 newRadialDir = (transform.position - center).normalized;
        if (newRadialDir.sqrMagnitude < 1e-8f)
            newRadialDir = (radial.sqrMagnitude > 1e-8f ? radial.normalized : Vector3.forward);

        transform.position = center + newRadialDir * newDist;
        transform.rotation = Quaternion.LookRotation(-newRadialDir, Vector3.up);

        // (4) Zielbedingungen
        if (navTarget.CompareTag("AsteroidBelt"))
        {
            // Am Gürtelrand anhalten
            if (newDist <= desiredOrbitRadius + 0.5f)
            {
                AbortAutopilot(false);
                return;
            }
        }
        else if (navTarget.CompareTag("Asteroid"))
        {
            // Nahe genug am Asteroiden → anhalten (kein Orbit)
            if (newDist <= desiredOrbitRadius + asteroidStopTolerance)
            {
                AbortAutopilot(false);
                return;
            }
        }
        else // Planet / generisches Orbital-Ziel
        {
            if (newDist <= desiredOrbitRadius + 1e-3f)
            {
                Vector3 tangent = Vector3.Cross(planeNormal, newRadialDir).normalized;
                orbitPlaneNormal = Vector3.Cross(newRadialDir, tangent).normalized;
                if (orbitPlaneNormal.sqrMagnitude < 1e-6f) orbitPlaneNormal = Vector3.up;

                autoState = AutoState.Orbit;
                transform.rotation = Quaternion.LookRotation(tangent, orbitPlaneNormal);
                radialSpeed = 0f;
            }
        }
    }
    #endregion

    /*====================================================================*/
    #region Orbit
    void OrbitTick()
    {
        if (navTarget == null) { AbortAutopilot(true); return; }
        StatusUpdated?.Invoke("Orbiting");

        Vector3 center = navTarget.position;

        transform.RotateAround(center, orbitPlaneNormal, OrbitDegPerSec * Time.fixedDeltaTime);

        Vector3 radial = (transform.position - center).normalized;
        transform.position = center + radial * desiredOrbitRadius;

        Vector3 dirTangent = Vector3.Cross(orbitPlaneNormal, radial).normalized;
        if (dirTangent.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(dirTangent, orbitPlaneNormal);
    }
    #endregion

    /*====================================================================*/
    #region Abort / Helpers
    public void AbortAutopilot(bool keepMomentum)
    {
        StatusUpdated?.Invoke("Stopping");
        Vector3 carriedVel = keepMomentum ? (_lastMove / Mathf.Max(Time.fixedDeltaTime, 1e-6f)) : Vector3.zero;

        rb.isKinematic = false;
        //rb.linearVelocity = carriedVel;
        rb.linearVelocity = carriedVel;
        rb.angularVelocity = Vector3.zero;

        autoState = AutoState.None;
        radialSpeed = 0f;
        _hasBeltAimPoint = false;

        StatusUpdated?.Invoke("Stopped");
        AutoPilotStopped?.Invoke();

        _directInit = false;
        _directDecelPhase = false;

    }

    static float GuessBodyRadius(Transform body)
    {
        if (body.TryGetComponent<SphereCollider>(out var col))
            return body.lossyScale.x * col.radius;
        if (body.TryGetComponent<Renderer>(out var rend))
            return rend.bounds.extents.magnitude;
        return body.lossyScale.x * 0.5f; // Fallback
    }

    /// <summary>
    /// Nächster Punkt auf innerem/äußerem Belt-Radius + Zielradius (center→point).
    /// </summary>
    static void ComputeBeltNearestPoint(AsteroidBelt belt, Vector3 probePos, out Vector3 nearestPoint, out float targetRadius)
    {
        Vector3 C = belt.transform.position;      // Belt-Zentrum
        Vector3 N = belt.transform.up;            // Normalenrichtung der Belt-Ebene

        // Projektion der Sondenposition in die Belt-Ebene
        Vector3 toProbe = probePos - C;
        Vector3 inPlane = Vector3.ProjectOnPlane(toProbe, N);

        if (inPlane.sqrMagnitude < 1e-10f)
            inPlane = belt.transform.forward;

        float r = inPlane.magnitude;

        // Nächstgelegene Kante wählen
        if (r < belt.innerRadius) targetRadius = belt.innerRadius;
        else if (r > belt.outerRadius) targetRadius = belt.outerRadius;
        else
        {
            float dInner = r - belt.innerRadius;
            float dOuter = belt.outerRadius - r;
            targetRadius = (dInner <= dOuter) ? belt.innerRadius : belt.outerRadius;
        }

        // Optional: leicht enger an die Kante heran (kleinerer Orbitradius an Gürtel)
        targetRadius = Mathf.Max(belt.innerRadius, Mathf.Min(belt.outerRadius, targetRadius - 2f));

        Vector3 radialDir = inPlane.normalized;
        nearestPoint = C + radialDir * targetRadius;
    }
    #endregion
}
