// Assets/Scripts/Probe/ProbeAutopilot.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ProbeAutopilot : MonoBehaviour
{
    /* ===================== Einstellungen ===================== */

    [Header("Ausrichten")]
    public float alignDegPerSec = 45f;
    public float alignToleranceDeg = 1.5f;
    public float reorientInterval = 0.6f;

    [Header("Direkter Flug")]
    public float speedGain = 0.1f;
    public float maxCruiseSpeed = 400f;
    public float approachSpeed = 3.0f;
    public float accel = 20f;
    public float holdDistanceUnits = 10f;

    [Header("Anflug auf Asteroiden")]
    [Tooltip("Abstand vor der Oberfläche der Nav-Sphäre, an dem beim Autopilot-Anflug gehalten wird.")]
    public float surfaceStandOff = 3f;

    [Header("Track-Assist (weit weg)")]
    public float trackAssistDistance = 200f;
    public float trackAssistMargin = 1.0f;

    [Header("Sticky-Follow")]
    public bool stickAfterAutopilot = true;
    [Range(0f, 1f)] public float stickPosSmoothTime = 0.25f;
    public float stickDeadzoneUnits = 0.1f;
    public float stickMaxSpeed = 200f;
    public float stickFaceDegPerSec = 45f;

    [Header("Autopilot Stop Verhalten")]
    public bool freezeOnStop = true;


    /* ===================== Laufzeit-Zustand ===================== */

    private enum AutoState { None, Aligning, Cruising, SelectingAsteroid, OrientingToAsteroid }
    private AutoState state = AutoState.None;

    [SerializeField] private Transform navTarget;
    private Transform lastTarget;

    // Das tatsächlich angeflogene Ziel (beim Belt: der gewählte Asteroiden-Root)
    private Transform flightTarget;

    private Rigidbody rb;

    // Fixer Anchor (für Nicht-Kugel-Ziele)
    private Vector3 anchorLocal;

    // Dynamischer Kugel-Anchor (für Asteroiden)
    private bool dynamicSurfaceAnchor = false;
    private float dynamicSurfaceRadiusUU = 0f; // Welt-Radius (UU)

    private Vector3 desiredWorld;
    private float curSpeed = 0f;
    private float reorientTimer = 0f;
    private Quaternion desiredFacing;

    // Sticky
    private bool stickyActive = false;
    private Transform stickyTarget = null;
    private Vector3 stickyLocalOffset; // bei dynamischem Anchor ungenutzt
    private Vector3 stickyVel;

    // Diagnose & Kinematik
    private Vector3 lastMove;
    private Vector3 _tPrevPos, _tVel;
    
    // Asteroid Belt intelligente Auswahl
    private AsteroidBelt currentBelt;
    private Transform selectedAsteroid;
    private float asteroidSelectionTimer = 0f;
    private const float ASTEROID_SELECTION_INTERVAL = 2f; // Alle 2 Sekunden neu bewerten

    /* ===================== Events ===================== */
    public event Action AutoPilotStarted, AutoPilotStopped;
    public event Action<string> StatusUpdated;
    public Transform NavTarget => navTarget;

    /* ===================== Unity ===================== */
    private void Awake() { rb = GetComponent<Rigidbody>(); }

    private void FixedUpdate()
    {
        if (state == AutoState.None && stickyActive && stickyTarget != null)
        { StickyFollowTick(); return; }
        if (state == AutoState.None) return;

        Vector3 before = transform.position;

        if (lastTarget != null && navTarget != lastTarget)
        { AbortAutopilot(keepMomentum: true); return; }

        switch (state)
        {
            case AutoState.Aligning: AlignTick(); break;
            case AutoState.Cruising: CruiseTick(); break;
            case AutoState.SelectingAsteroid: SelectingAsteroidTick(); break;
            case AutoState.OrientingToAsteroid: OrientingToAsteroidTick(); break;
        }

        lastMove = transform.position - before;
    }

    /* ===================== Öffentliche API ===================== */
    public void SetNavTarget(Transform tgt)
    {
        navTarget = tgt;
        AbortAutopilot(keepMomentum: true);
    }
    public bool IsAutopilotActive => state != AutoState.None;

    public void StartAutopilot()
    {
        if (navTarget == null || state != AutoState.None) return;

        ResetRigidbody(makeKinematicAfter: true);

        lastTarget = navTarget;
        stickyTarget = navTarget;
        stickyActive = false;

        _tPrevPos = navTarget.position;
        _tVel = Vector3.zero;

        // ZUERST: Prüfe auf Asteroid Belt und setze korrekte Ausrichtung
        var belt = navTarget != null ? navTarget.GetComponent<AsteroidBelt>() : null;
        if (belt != null)
        {
            // Sofortige Asteroid-Auswahl für korrekte Ausrichtung
            selectedAsteroid = SelectOptimalAsteroid();
            if (selectedAsteroid != null)
            {
                // Direkt zum Asteroiden ausrichten - NICHT zum Belt-Zentrum!
                desiredFacing = ComputeLookRotationTo(selectedAsteroid.position);
                StatusUpdated?.Invoke($"Richte mich auf Asteroid aus: {selectedAsteroid.name}");
            }
            else
            {
                // Fallback: Zum Belt-Zentrum ausrichten
                desiredFacing = ComputeLookRotationTo(navTarget.position);
                StatusUpdated?.Invoke("Kein Asteroid gefunden - richte mich auf Belt-Zentrum aus");
            }
        }
        else
        {
            // Normales Ziel - normale Ausrichtung
            desiredFacing = ComputeLookRotationTo(navTarget.position);
        }

        state = AutoState.Aligning;
        curSpeed = 0f;
        reorientTimer = 0f;

        AutoPilotStarted?.Invoke();
        StatusUpdated?.Invoke("Aligning");
    }

    public void StopAutopilot() => AbortAutopilot(false);

    /* ===================== Kern-Logik ===================== */

    private void AlignTick()
    {
        if (navTarget == null) { AbortAutopilot(true); return; }

        reorientTimer += Time.fixedDeltaTime;
        if (reorientTimer >= reorientInterval)
        {
            // Prüfe auf Asteroid Belt und aktualisiere Ausrichtung entsprechend
            var belt = navTarget != null ? navTarget.GetComponent<AsteroidBelt>() : null;
            if (belt != null && selectedAsteroid != null)
            {
                // Bleibe auf den ausgewählten Asteroiden ausgerichtet
                desiredFacing = ComputeLookRotationTo(selectedAsteroid.position);
            }
            else
            {
                // Normale Ausrichtung zum Ziel
                desiredFacing = ComputeLookRotationTo(navTarget.position);
            }
            reorientTimer = 0f;
        }

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, desiredFacing, alignDegPerSec * Time.fixedDeltaTime);

        if (Quaternion.Angle(transform.rotation, desiredFacing) <= alignToleranceDeg)
        {
            ChooseFlightTargetAndAnchor();
            desiredWorld = GetDesiredWorld(); // erster Zielpunkt

            SampleTargetKinematics();

            state = AutoState.Cruising;
            StatusUpdated?.Invoke("Cruising (direct)");
        }
    }

    private void CruiseTick()
    {
        if (flightTarget == null) { AbortAutopilot(true); return; }

        reorientTimer += Time.fixedDeltaTime;
        if (reorientTimer >= reorientInterval)
        {
            desiredFacing = ComputeLookRotationTo(flightTarget.position);
            reorientTimer = 0f;
            SampleTargetKinematics();
        }

        desiredWorld = GetDesiredWorld();

        float dist = Vector3.Distance(transform.position, desiredWorld);

        float baseSpeed = Mathf.Clamp(dist * Mathf.Max(0f, speedGain), approachSpeed, maxCruiseSpeed);
        float minTrack = (dist > trackAssistDistance) ? Mathf.Min(maxCruiseSpeed, _tVel.magnitude + trackAssistMargin) : approachSpeed;
        float targetSpeed = Mathf.Max(baseSpeed, minTrack);

        curSpeed = Mathf.MoveTowards(curSpeed, targetSpeed, accel * Time.fixedDeltaTime);

        // ---- Bewegung erstellen und HART vor der Nav-Sphäre kappen ----
        float dt = Time.fixedDeltaTime;
        Vector3 next = Vector3.MoveTowards(transform.position, desiredWorld, curSpeed * dt);

        HardClampOutsideAsteroid(ref next, surfaceStandOff);

        Vector3 move = next - transform.position;
        if (move.sqrMagnitude > 1e-10f)
        {
            transform.position = next;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredFacing, alignDegPerSec * dt);
        }

        // Ankunft?
        float stopThresh = Mathf.Max(0.05f, holdDistanceUnits * 0.2f);
        if (Vector3.Distance(transform.position, GetDesiredWorld()) <= stopThresh)
        {
            stickyTarget = flightTarget;
            stickyLocalOffset = anchorLocal; // bei dynamischem Anchor unbenutzt

            stickyActive = stickAfterAutopilot && stickyTarget != null;
            rb.isKinematic = stickyActive;

            state = AutoState.None;
            AutoPilotStopped?.Invoke();
            StatusUpdated?.Invoke(stickyActive ? "Arrived (sticky follow active)" : "Arrived");
        }
    }

    private void StickyFollowTick()
    {
        if (stickyTarget == null) { stickyActive = false; return; }

        float dt = Time.fixedDeltaTime;

        Vector3 desired = dynamicSurfaceAnchor
            ? ComputeSphereStandOffPoint(stickyTarget, dynamicSurfaceRadiusUU, 0f)
            : stickyTarget.TransformPoint(stickyLocalOffset);

        HardClampOutsideAsteroid(ref desired, 0f);

        Vector3 delta = desired - transform.position;

        if (delta.magnitude > stickDeadzoneUnits)
        {
            if (stickPosSmoothTime > 0f)
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position, desired, ref stickyVel,
                    stickPosSmoothTime, stickMaxSpeed, dt);
            }
            else
            {
                transform.position = Vector3.MoveTowards(transform.position, desired, stickMaxSpeed * dt);
            }
        }

        reorientTimer += dt;
        if (reorientTimer >= reorientInterval)
        {
            desiredFacing = ComputeLookRotationTo(stickyTarget.position);
            reorientTimer = 0f;
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredFacing, stickFaceDegPerSec * dt);
    }

    /* ===================== Zielwahl & Anchor ===================== */

    private void ChooseFlightTargetAndAnchor()
    {
        // ZUERST: Prüfe auf Asteroid Belt und wähle Asteroid aus
        var belt = navTarget != null ? navTarget.GetComponent<AsteroidBelt>() : null;
        if (belt != null)
        {
            currentBelt = belt;
            asteroidSelectionTimer = 0f;
            
            // Sofortige Asteroid-Auswahl statt Warten
            selectedAsteroid = SelectOptimalAsteroid();
            
            if (selectedAsteroid != null)
            {
                // Direkt zum ausgewählten Asteroiden fliegen - KEIN Belt-Zentrum!
                SetupAsteroidTarget(selectedAsteroid);
                StatusUpdated?.Invoke($"Asteroid ausgewählt: {selectedAsteroid.name}");
                return;
            }
            else
            {
                // Fallback: Zum Belt-Zentrum fliegen
                StatusUpdated?.Invoke("Kein Asteroid gefunden - fliege zum Belt-Zentrum");
                flightTarget = navTarget; // Nur als Fallback setzen
            }
        }
        else
        {
            // Normales Ziel (Planet, einzelner Asteroid als Target, etc.)
            flightTarget = navTarget;
        }
        
        // Allgemeine Einstellungen für normale Ziele
        if (belt == null)
        {
            var t = navTarget;
            Vector3 dir = (transform.position - t.position);
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.right;
            Vector3 worldAnchor = t.position + dir.normalized * Mathf.Max(0.01f, holdDistanceUnits);
            anchorLocal = t.InverseTransformPoint(worldAnchor);
        }
        
        dynamicSurfaceAnchor = false;
        dynamicSurfaceRadiusUU = 0f;

        desiredFacing = ComputeLookRotationTo(flightTarget.position);
    }

    /// <summary>
    /// Tick für intelligente Asteroid-Auswahl
    /// </summary>
    private void SelectingAsteroidTick()
    {
        asteroidSelectionTimer += Time.fixedDeltaTime;
        
        // Alle ASTEROID_SELECTION_INTERVAL Sekunden oder beim ersten Mal Asteroid auswählen
        if (asteroidSelectionTimer >= ASTEROID_SELECTION_INTERVAL || selectedAsteroid == null)
        {
            selectedAsteroid = SelectOptimalAsteroid();
            asteroidSelectionTimer = 0f;
            
            if (selectedAsteroid != null)
            {
                StatusUpdated?.Invoke($"Asteroid ausgewählt: {selectedAsteroid.name}");
                state = AutoState.OrientingToAsteroid;
            }
            else
            {
                StatusUpdated?.Invoke("Kein geeigneter Asteroid gefunden - warte...");
            }
        }
    }

    /// <summary>
    /// Tick für Ausrichtung auf den ausgewählten Asteroiden
    /// </summary>
    private void OrientingToAsteroidTick()
    {
        if (selectedAsteroid == null)
        {
            state = AutoState.SelectingAsteroid;
            return;
        }
        
        // Berechne gewünschte Ausrichtung zum Asteroiden
        Vector3 directionToAsteroid = (selectedAsteroid.position - transform.position).normalized;
        desiredFacing = Quaternion.LookRotation(directionToAsteroid);
        
        // Prüfe, ob Ausrichtung erreicht ist
        float angleDifference = Quaternion.Angle(transform.rotation, desiredFacing);
        if (angleDifference <= alignToleranceDeg)
        {
            // Ausrichtung erreicht - beginne normalen Anflug
            SetupAsteroidTarget(selectedAsteroid);
            state = AutoState.Aligning;
            StatusUpdated?.Invoke("Ausrichtung erreicht - beginne Anflug");
        }
        else
        {
            // Weiter ausrichten
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredFacing, alignDegPerSec * Time.fixedDeltaTime);
        }
    }

    /// <summary>
    /// Wählt den optimalen Asteroiden basierend auf verschiedenen Kriterien
    /// </summary>
    private Transform SelectOptimalAsteroid()
    {
        if (currentBelt == null) return null;
        
        Transform[] asteroids = GetAllAsteroidsFromBelt(currentBelt);
        if (asteroids == null || asteroids.Length == 0) return null;
        
        Transform bestAsteroid = null;
        float bestScore = float.MaxValue;
        
        foreach (var asteroid in asteroids)
        {
            if (asteroid == null) continue;
            
            float score = CalculateAsteroidScore(asteroid);
            if (score < bestScore)
            {
                bestScore = score;
                bestAsteroid = asteroid;
            }
        }
        
        return bestAsteroid;
    }

    /// <summary>
    /// Sammelt alle Asteroiden aus einem Asteroid Belt
    /// </summary>
    private Transform[] GetAllAsteroidsFromBelt(AsteroidBelt belt)
    {
        if (belt == null) return new Transform[0];
        
        List<Transform> asteroids = new List<Transform>();
        
        // Methode 1: Suche nach Kindern mit Tag "Asteroid"
        foreach (Transform child in belt.transform)
        {
            if (child != null && child.CompareTag("Asteroid"))
            {
                asteroids.Add(child);
            }
        }
        
        // Methode 2: Suche nach allen GameObjects mit Tag "Asteroid" in der Szene
        if (asteroids.Count == 0)
        {
            GameObject[] allAsteroids = GameObject.FindGameObjectsWithTag("Asteroid");
            foreach (var asteroidGO in allAsteroids)
            {
                if (asteroidGO != null && !asteroids.Contains(asteroidGO.transform))
                {
                    asteroids.Add(asteroidGO.transform);
                }
            }
        }
        
        // Methode 3: Fallback - verwende GetClosestAsteroid mit verschiedenen Positionen
        if (asteroids.Count == 0)
        {
            Debug.LogWarning("[ProbeAutopilot] Keine Asteroiden über Tags gefunden - verwende GetClosestAsteroid");
            
            // Versuche mehrere Positionen im Belt zu scannen
            Vector3 beltCenter = belt.transform.position;
            float beltRadius = (belt.innerRadius + belt.outerRadius) * 0.5f;
            
            for (int i = 0; i < 16; i++) // Mehr Positionen für bessere Abdeckung
            {
                float angle = i * 22.5f * Mathf.Deg2Rad; // 16 Positionen alle 22.5°
                Vector3 scanPos = beltCenter + new Vector3(
                    Mathf.Cos(angle) * beltRadius,
                    0,
                    Mathf.Sin(angle) * beltRadius
                );
                
                Transform closest = belt.GetClosestAsteroid(scanPos);
                if (closest != null && !asteroids.Contains(closest))
                {
                    asteroids.Add(closest);
                }
            }
        }
        
        Debug.Log($"[ProbeAutopilot] {asteroids.Count} Asteroiden im Belt gefunden");
        return asteroids.ToArray();
    }

    /// <summary>
    /// Berechnet einen Score für einen Asteroiden (niedriger = besser)
    /// </summary>
    private float CalculateAsteroidScore(Transform asteroid)
    {
        if (asteroid == null) return float.MaxValue;
        
        // Distanz-Score (näher = besser) - STÄRKSTE Gewichtung
        float distance = Vector3.Distance(transform.position, asteroid.position);
        float distanceScore = distance * 1.0f; // Stärkere Gewichtung für Distanz
        
        // Größe-Score (größer = besser für Mining)
        float size = asteroid.localScale.magnitude;
        float sizeScore = Mathf.Max(0, 10f - size) * 0.1f; // Schwächere Gewichtung für Größe
        
        // Geschwindigkeit-Score (langsamer = besser)
        Rigidbody rb = asteroid.GetComponent<Rigidbody>();
        float velocityScore = 0f;
        if (rb != null)
        {
            velocityScore = rb.linearVelocity.magnitude * 0.1f; // Schwächere Gewichtung
        }
        
        // Material-Score (falls MineableAsteroid vorhanden)
        float materialScore = 0f;
        var mineable = asteroid.GetComponentInParent<MineableAsteroid>();
        if (mineable != null)
        {
            // Bevorzuge Asteroiden mit mehr Material
            float remainingPercentage = mineable.RemainingPercentage;
            materialScore = (1f - remainingPercentage) * 0.5f; // Schwächere Gewichtung
        }
        
        float totalScore = distanceScore + sizeScore + velocityScore + materialScore;
        
        Debug.Log($"[ProbeAutopilot] Asteroid {asteroid.name}: Distanz={distance:F1}, Score={totalScore:F2}");
        
        return totalScore;
    }

    /// <summary>
    /// Richtet das Flight-Target auf den ausgewählten Asteroiden aus
    /// </summary>
    private void SetupAsteroidTarget(Transform asteroid)
    {
        if (asteroid == null) return;
        
        // Robust auf Asteroiden-ROOT samt Radius
        if (TryGetAsteroidNavData(asteroid, out var root, out var radiusWorld))
        {
            flightTarget = root;
            dynamicSurfaceAnchor = true;
            dynamicSurfaceRadiusUU = radiusWorld;
        }
        else
        {
            // Fallback: fixer Oberflächenpunkt (Bounds) – NIE Zentrum!
            flightTarget = asteroid;
            Vector3 anchorWorld = ComputeSurfaceAnchorFromBounds(asteroid, transform.position, surfaceStandOff);
            anchorLocal = flightTarget.InverseTransformPoint(anchorWorld);
        }
        
        desiredFacing = ComputeLookRotationTo(flightTarget.position);
    }

    private Vector3 GetDesiredWorld()
    {
        if (dynamicSurfaceAnchor && flightTarget != null)
            return ComputeSphereStandOffPoint(flightTarget, dynamicSurfaceRadiusUU, surfaceStandOff);

        return flightTarget.TransformPoint(anchorLocal);
    }

    /// Punkt auf Linie Zentrum→Sonde bei (Radius + StandOff) – **WELT-Radius!**
    private Vector3 ComputeSphereStandOffPoint(Transform asteroidRoot, float radiusWorldUU, float standOff)
    {
        Vector3 center = asteroidRoot.position;
        Vector3 dir = transform.position - center;
        if (dir.sqrMagnitude < 1e-8f) dir = asteroidRoot.forward;
        dir.Normalize();
        const float eps = 0.01f; // vermeidet Flattern
        return center + dir * (radiusWorldUU + Mathf.Max(0f, standOff) + eps);
    }

    /// Fallback: Belt-Rand (in UU) bei näherem Radius
    private Vector3 ComputeFixedBeltAnchorWorld(AsteroidBelt belt, AsteroidBelt.ResolvedConfig cfg, Vector3 probePos)
    {
        Vector3 C = belt.transform.position;   // Belt-Zentrum
        Vector3 N = belt.transform.up;         // Belt-Normale

        Vector3 inPlane = Vector3.ProjectOnPlane(probePos - C, N);
        if (inPlane.sqrMagnitude < 1e-10f) inPlane = belt.transform.right;

        float r = inPlane.magnitude;
        Vector3 radialDir = inPlane.normalized;

        float dInner = Mathf.Abs(r - cfg.innerRadiusUU);
        float dOuter = Mathf.Abs(cfg.outerRadiusUU - r);
        float targetR = (dInner <= dOuter) ? cfg.innerRadiusUU : cfg.outerRadiusUU;

        return C + radialDir * targetR;
    }

    /// Fallback: Bounds-basierter Oberflächenpunkt (wenn keine Nav-Sphäre vorhanden)
    private Vector3 ComputeSurfaceAnchorFromBounds(Transform asteroid, Vector3 probePos, float standOff)
    {
        Vector3 center = asteroid.position;
        Vector3 dir = probePos - center;
        if (dir.sqrMagnitude < 1e-8f) dir = asteroid.forward;
        dir.Normalize();

        float r = ApproximateRadiusFromBounds(asteroid);
        const float eps = 0.01f;
        return center + dir * (r + Mathf.Max(0f, standOff) + eps);
    }

    private static float ApproximateRadiusFromBounds(Transform t)
    {
        float radius = 0.5f * t.lossyScale.magnitude; // Fallback

        var renderers = t.GetComponentsInChildren<Renderer>(true);
        if (renderers != null && renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            radius = Mathf.Max(radius, b.extents.magnitude);
        }
        else
        {
            var colliders = t.GetComponentsInChildren<Collider>(true);
            if (colliders != null && colliders.Length > 0)
            {
                Bounds b = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++) b.Encapsulate(colliders[i].bounds);
                radius = Mathf.Max(radius, b.extents.magnitude);
            }
        }
        return Mathf.Max(radius, 0.01f);
    }

    /* ===================== Utilities ===================== */

    /// Liefert Asteroiden-Root + **WELT-Nav-Radius** (aus SphereCollider), sonst Bounds.
    private bool TryGetAsteroidNavData(Transform any, out Transform asteroidRoot, out float radiusWorldUU)
    {
        asteroidRoot = null;
        radiusWorldUU = 0f;

        if (any == null) return false;

        var ma = any.GetComponentInParent<MineableAsteroid>();
        if (ma != null)
        {
            asteroidRoot = ma.transform;

            // bevorzugt Nav-SphereCollider → Welt-Radius aus radius * maxLossyScale
            var sc = ma.navSphereCollider;
            if (sc != null)
            {
                float scale = Mathf.Max(asteroidRoot.lossyScale.x, Mathf.Max(asteroidRoot.lossyScale.y, asteroidRoot.lossyScale.z));
                radiusWorldUU = Mathf.Max(0.01f, sc.radius * scale);
                return true;
            }

            // Fallback: gespeicherter Radius oder Bounds
            radiusWorldUU = (ma.navSphereRadiusUU > 0f) ? ma.navSphereRadiusUU : ApproximateRadiusFromBounds(asteroidRoot);
            return true;
        }

        // Fallback: Tag/Hierarchie – obersten Asteroiden-Knoten suchen
        var root = any;
        while (root.parent != null) root = root.parent;

        if (root.CompareTag("Asteroid"))
        {
            asteroidRoot = root;
            var ma2 = asteroidRoot.GetComponent<MineableAsteroid>();
            if (ma2 != null && ma2.navSphereCollider != null)
            {
                float scale = Mathf.Max(asteroidRoot.lossyScale.x, Mathf.Max(asteroidRoot.lossyScale.y, asteroidRoot.lossyScale.z));
                radiusWorldUU = Mathf.Max(0.01f, ma2.navSphereCollider.radius * scale);
                return true;
            }
            radiusWorldUU = (ma2 != null && ma2.navSphereRadiusUU > 0f) ? ma2.navSphereRadiusUU : ApproximateRadiusFromBounds(asteroidRoot);
            return true;
        }

        return false;
    }

    /// Harter No-Clip-Guard: position stets **außerhalb** der Nav-Sphäre halten.
    private void HardClampOutsideAsteroid(ref Vector3 pos, float standOff)
    {
        if (flightTarget == null) return;

        if (!TryGetAsteroidNavData(flightTarget, out var root, out var radiusWorld)) return;

        Vector3 c = root.position;
        Vector3 v = pos - c;
        float d = v.magnitude;
        float minD = radiusWorld + Mathf.Max(0f, standOff);

        if (d < minD && d > 1e-6f)
        {
            pos = c + v * (minD / d);
            if (curSpeed > 0f) curSpeed = 0f; // sanft bremsen
        }
    }

    private void SampleTargetKinematics()
    {
        if (flightTarget == null) { _tVel = Vector3.zero; _tPrevPos = Vector3.zero; return; }

        if (flightTarget.TryGetComponent<Rigidbody>(out var trb))
            _tVel = trb.linearVelocity; // Unity 6 API
        else
            _tVel = (flightTarget.position - _tPrevPos) / Mathf.Max(Time.fixedDeltaTime, 1e-5f);

        _tPrevPos = flightTarget.position;
    }

    private Quaternion ComputeLookRotationTo(Vector3 worldPos)
    {
        Vector3 to = worldPos - transform.position;
        if (to.sqrMagnitude < 1e-10f) to = transform.forward;
        return Quaternion.LookRotation(to.normalized, Vector3.up);
    }

    public void AbortAutopilot(bool keepMomentum)
    {
        StatusUpdated?.Invoke("Stopping");

        if (freezeOnStop)
        {
            ResetRigidbody(makeKinematicAfter: true);
            stickyActive = false; // kein relativer Offset
        }
        else
        {

            bool willStick = stickAfterAutopilot && (stickyTarget != null);

            if (willStick)
            {
                rb.isKinematic = true;
            }
            else
            {
                rb.isKinematic = false;
                Vector3 carried = keepMomentum
                    ? (lastMove / Mathf.Max(Time.fixedDeltaTime, 1e-6f))
                    : Vector3.zero;
                rb.linearVelocity = carried;
                rb.angularVelocity = Vector3.zero;
            }
            stickyActive = willStick;
        }
        state = AutoState.None;
        
        // Asteroid Belt Variablen zurücksetzen
        currentBelt = null;
        selectedAsteroid = null;
        asteroidSelectionTimer = 0f;
        
        AutoPilotStopped?.Invoke();
        StatusUpdated?.Invoke(stickyActive ? "Stopped (sticky follow active)" : "Stopped");
    }

    /* ==== Öffentliche Helper für Mining: Oberfläche „anlegen“ (StandOff=0) ==== */
    public void SetSurfaceContact(Transform asteroid)
    {
        if (asteroid == null) return;

        if (!TryGetAsteroidNavData(asteroid, out var root, out var radiusWorld)) return;

        flightTarget = root;
        dynamicSurfaceAnchor = true;
        dynamicSurfaceRadiusUU = radiusWorld;

        stickyTarget = root;
        stickyLocalOffset = Vector3.zero; // ungenutzt
        stickyActive = true;
        rb.isKinematic = true;

        desiredFacing = ComputeLookRotationTo(root.position);

        state = AutoState.None;
        AutoPilotStopped?.Invoke();
        StatusUpdated?.Invoke("Surface contact engaged");
    }

    /* ===================== Diagnose ===================== */

    /// Distanz zum tatsächlich angesteuerten Punkt
    public float CurrentDistanceUnits
    {
        get
        {
            if (flightTarget != null)
            {
                var worldAnchor = dynamicSurfaceAnchor
                    ? ComputeSphereStandOffPoint(flightTarget, dynamicSurfaceRadiusUU, surfaceStandOff)
                    : flightTarget.TransformPoint(anchorLocal);
                return Vector3.Distance(transform.position, worldAnchor);
            }

            if (navTarget == null) return float.NaN;

            var belt = navTarget.GetComponent<AsteroidBelt>();
            if (belt != null)
            {
                var cfg = belt.ToResolvedConfig();
                Vector3 anchorWorld = ComputeFixedBeltAnchorWorld(belt, cfg, transform.position);
                return Vector3.Distance(transform.position, anchorWorld);
            }

            return Vector3.Distance(transform.position, navTarget.position);
        }
    }

    public float CurrentSpeedUnits
    {
        get
        {
            float dt = Mathf.Max(Time.fixedDeltaTime, 1e-6f);
            return lastMove.magnitude / dt;
        }
    }

    // UI-Kompatibilität
    public static bool TrySetNavTargetOnSelectedProbe(Transform target)
    {
        if (target == null) return false;

        var sel = HUDBindingService.I?.SelectedItem;
        if (sel?.Transform == null) return false;

        var ap = sel.Transform.GetComponent<ProbeAutopilot>();
        if (ap == null) return false;

        ap.SetNavTarget(target);
        ap.StatusUpdated?.Invoke($"Nav target set to {target.name}");
        return true;
    }

    void ResetRigidbody(bool makeKinematicAfter)
    {
        bool wasKinematic = rb.isKinematic;
        if (wasKinematic) rb.isKinematic = false;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.isKinematic = makeKinematicAfter;
    }
}
