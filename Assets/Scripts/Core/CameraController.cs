// Assets/Scripts/Core/CameraController.cs
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Target (Sonde)")]
    public Transform target;

    [Header("Weltraum-Kamera Einstellungen")]
    [Tooltip("Standard-Rückstand zur Sonde")]
    public Vector3 defaultOffset = new(0, 2f, -8f);

    [Tooltip("Minimum Entfernung zur Sonde (in Unity Units)")]
    public float minDistance = 0.1f;

    [Tooltip("Maximum Entfernung zur Sonde (für Übersicht)")]
    public float maxDistance = 1000f; // Viel größer für Weltraum

    [Tooltip("Zoom-Geschwindigkeit (Mausrad)")]
    public float zoomSpeed = 0.1f;

    [Tooltip("Smooth Zoom für flüssige Übergänge")]
    public bool smoothZoom = true;

    [Tooltip("Zoom Smoothing Geschwindigkeit")]
    public float zoomSmoothSpeed = 5f;

    [Header("Maus-Steuerung")]
    [Tooltip("Empfindlichkeit für Freilook (rechte Maustaste)")]
    public float mouseSensitivity = 2f;

    [Tooltip("Maximaler Pitch-Winkel (Hoch/Runter schauen)")]
    public float pitchLimit = 85f;

    [Tooltip("Maus-Inversion für Y-Achse")]
    public bool invertY = false;

    [Header("Weltraum-Navigation")]
    [Tooltip("Automatisches Umschalten auf Planeten/Objekte in der Nähe")]
    public bool autoFocusNearbyObjects = true;

    [Tooltip("Entfernung für automatischen Fokus-Wechsel")]
    public float autoFocusDistance = 50f;

    [Tooltip("Geschwindigkeit der Kamera-Bewegung beim Ziel-Wechsel")]
    public float targetTransitionSpeed = 2f;

    [Header("FOV & Rendering")]
    [Tooltip("Standard Field of View")]
    public float defaultFOV = 60f;

    [Tooltip("FOV beim maximalen Zoom-out (für Übersicht)")]
    public float maxFOV = 120f;

    [Tooltip("Dynamisches FOV basierend auf Geschwindigkeit")]
    public bool dynamicFOV = true;

    [Header("Collision & Clipping")]
    [Tooltip("Kamera-Kollision mit Objekten verhindern")]
    public bool preventCollision = true;

    [Tooltip("Layer für Kollisions-Erkennung")]
    public LayerMask collisionLayers = -1;

    [Tooltip("Radius für Kollisions-Erkennung")]
    public float collisionRadius = 0.5f;

    // Interne Variablen
    InputController controls;
    Camera cam;
    bool followTarget = true;
    float yaw, pitch;
    Vector3 currentOffset;
    Vector3 targetOffset;
    float currentDistance;
    float targetDistance;

    // Für Smooth Transitions
    Vector3 velocityOffset;
    Transform previousTarget;
    bool isTransitioning = false;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.fieldOfView = defaultFOV;

        currentOffset = defaultOffset;
        targetOffset = defaultOffset;
        currentDistance = defaultOffset.magnitude;
        targetDistance = currentDistance;

        InitializeInputSystem();
    }

    void InitializeInputSystem()
    {
        controls = new InputController();
        controls.Camera.Enable();

        // Freilook mit rechter Maustaste
        controls.Camera.Look.performed += ctx =>
        {
            if (Mouse.current.rightButton.isPressed)
            {
                followTarget = false;
                Vector2 mouseDelta = ctx.ReadValue<Vector2>() * mouseSensitivity;

                yaw += mouseDelta.x;
                pitch += invertY ? mouseDelta.y : -mouseDelta.y;
                pitch = Mathf.Clamp(pitch, -pitchLimit, pitchLimit);

                ApplyFreeRotation();
            }
        };

        controls.Camera.RightClick.started += _ =>
        {
            followTarget = false;
            Cursor.lockState = CursorLockMode.Locked;
        };

        controls.Camera.RightClick.canceled += _ =>
        {
            Cursor.lockState = CursorLockMode.None;
        };

        // Zoom mit Mausrad
        controls.Camera.Zoom.performed += ctx =>
        {
            if (target == null) return;

            float scrollInput = ctx.ReadValue<float>();
            float zoomFactor = 1f + (scrollInput * zoomSpeed);

            targetDistance = currentDistance / zoomFactor;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);

            if (!smoothZoom)
            {
                currentDistance = targetDistance;
                UpdateOffsetFromDistance();
            }
        };

        // Reset auf F1
        controls.Camera.Reset.performed += _ => ResetToDefaultView();

        // Navigation zu Planeten (Numpad 0-9)
        //controls.Camera.NavStar.performed += _ => NavigateToObject(0);        // Numpad 0 = Stern
        //controls.Camera.NavPlanet1.performed += _ => NavigateToObject(1);     // Numpad 1 = Planet 1
        //controls.Camera.NavPlanet2.performed += _ => NavigateToObject(2);     // Numpad 2 = Planet 2
        // ... weitere Planeten können hier hinzugefügt werden
    }

    void OnDestroy() => controls?.Dispose();

    void LateUpdate()
    {
        if (target == null) return;

        UpdateCameraDistance();
        UpdateCameraPosition();
        UpdateDynamicFOV();

        if (autoFocusNearbyObjects && !isTransitioning)
            CheckForNearbyObjects();
    }

    void UpdateCameraDistance()
    {
        if (smoothZoom && Mathf.Abs(currentDistance - targetDistance) > 0.01f)
        {
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * zoomSmoothSpeed);
            UpdateOffsetFromDistance();
        }
    }

    void UpdateOffsetFromDistance()
    {
        Vector3 direction = currentOffset.normalized;
        currentOffset = direction * currentDistance;
        targetOffset = currentOffset;
    }

    void UpdateCameraPosition()
    {
        Vector3 desiredPosition;

        if (followTarget)
        {
            // Folge-Modus: Kamera folgt der Sonden-Rotation
            Quaternion targetRotation = Quaternion.LookRotation(target.forward, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * targetTransitionSpeed);
            desiredPosition = target.position + transform.rotation * currentOffset;
        }
        else
        {
            // Freilook-Modus: Freie Rotation um das Ziel
            desiredPosition = target.position + transform.rotation * Vector3.back * currentDistance;
        }

        // Kollisions-Erkennung
        if (preventCollision)
        {
            desiredPosition = CheckCollision(target.position, desiredPosition);
        }

        // Smooth position transition
        if (isTransitioning)
        {
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocityOffset, 1f / targetTransitionSpeed);

            if (Vector3.Distance(transform.position, desiredPosition) < 0.1f)
                isTransitioning = false;
        }
        else
        {
            transform.position = desiredPosition;
        }
    }

    Vector3 CheckCollision(Vector3 targetPos, Vector3 desiredPos)
    {
        Vector3 direction = (desiredPos - targetPos).normalized;
        float distance = Vector3.Distance(targetPos, desiredPos);

        if (Physics.SphereCast(targetPos, collisionRadius, direction, out RaycastHit hit,
                              distance, collisionLayers))
        {
            // Position vor dem Hindernis
            return targetPos + direction * (hit.distance - collisionRadius);
        }

        return desiredPos;
    }

    void UpdateDynamicFOV()
    {
        if (!dynamicFOV || target == null) return;

        // FOV basierend auf Entfernung zum Ziel anpassen
        float distanceRatio = (currentDistance - minDistance) / (maxDistance - minDistance);
        float targetFOV = Mathf.Lerp(defaultFOV, maxFOV, distanceRatio);

        // Geschwindigkeits-basiertes FOV (falls Rigidbody vorhanden)
        if (target.TryGetComponent<Rigidbody>(out var rb))
        {
            float speed = rb.linearVelocity.magnitude;
            float speedFOV = Mathf.Clamp(speed * 2f, 0f, 20f); // Max +20 FOV bei hoher Geschwindigkeit
            targetFOV += speedFOV;
        }

        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * 2f);
    }

    void CheckForNearbyObjects()
    {
        if (PlanetRegistry.Instance == null) return;

        Transform closestObject = null;
        float closestDistance = autoFocusDistance;

        // Prüfe alle registrierten Objekte
        foreach (var obj in PlanetRegistry.Instance.NavTargets)
        {
            if (obj == target) continue; // Aktuelles Ziel ignorieren

            float distance = Vector3.Distance(transform.position, obj.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestObject = obj;
            }
        }

        if (closestObject != null && closestObject != target)
        {
            SetTarget(closestObject);
        }
    }

    void ApplyFreeRotation()
    {
        transform.rotation = Quaternion.Euler(pitch, yaw, 0);
    }

    public void ResetToDefaultView()
    {
        if (target == null) return;

        followTarget = true;

        // Kamera SOFORT hinter die Sonde – nur Yaw wird übernommen
        Vector3 flatFwd = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        if (flatFwd.sqrMagnitude < 0.001f) flatFwd = Vector3.forward;
        transform.rotation = Quaternion.LookRotation(flatFwd, Vector3.up);

        currentOffset = defaultOffset;
        currentDistance = defaultOffset.magnitude;
        targetDistance = currentDistance;

        cam.fieldOfView = defaultFOV;
        //isTransitioning = true;      // nur Position wird geglättet
    }

    public void SetTarget(Transform newTarget)
    {
        if (newTarget == null) return;

        previousTarget = target;
        target = newTarget;

        ResetToDefaultView();

        Debug.Log($"Camera target changed to: {newTarget.name}");
    }

    void NavigateToObject(int index)
    {
        if (PlanetRegistry.Instance == null) return;

        var navTargets = PlanetRegistry.Instance.NavTargets;
        if (index >= 0 && index < navTargets.Count)
        {
            SetTarget(navTargets[index]);
        }
    }

    // Public API für andere Scripts
    public void ZoomTo(float distance)
    {
        targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
    }

    public void SetZoomRange(float min, float max)
    {
        minDistance = min;
        maxDistance = max;
        targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
    }

    public float GetCurrentDistance() => currentDistance;

    public bool IsFollowing() => followTarget;

    // Debug-Informationen
    void OnDrawGizmosSelected()
    {
        if (target == null) return;

        // Aktuelle Ziel-Verbindung
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, target.position);

        // Zoom-Bereiche
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(target.position, minDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(target.position, maxDistance);

        // Auto-Focus Bereich
        if (autoFocusNearbyObjects)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, autoFocusDistance);
        }
    }

#if UNITY_EDITOR
    //void OnGUI()
    //{
    //    if (!Debug.isDebugBuild) return;
        
    //    GUILayout.BeginArea(new Rect(10, 10, 300, 120));
    //    GUILayout.BeginVertical("box");
    //    GUILayout.Label("Camera Info", EditorStyles.boldLabel);
    //    GUILayout.Label($"Target: {(target ? target.name : "None")}");
    //    GUILayout.Label($"Distance: {currentDistance:F1} Units");
    //    GUILayout.Label($"FOV: {cam.fieldOfView:F1}°");
    //    GUILayout.Label($"Mode: {(followTarget ? "Following" : "Free Look")}");
    //    GUILayout.Label($"Position: {transform.position}");
    //    GUILayout.EndVertical();
    //    GUILayout.EndArea();
    //}
#endif
}