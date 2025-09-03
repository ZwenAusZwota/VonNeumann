// Assets/Scripts/Core/CameraController.cs
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Target (Sonde)")]
    [Tooltip("Falls leer, bindet sich die Kamera beim Start automatisch an die Sonde.")]
    public Transform target;

    [Header("Automatische Bindung")]
    [Tooltip("Beim Start automatisch nach einer Sonde suchen und binden.")]
    public bool autoBindOnStart = true;

    [Tooltip("Wie lange nach dem Start wird wiederholt nach einer Sonde gesucht?")]
    public float autoBindWindowSeconds = 5f;

    [Tooltip("Versuchsintervall für Auto-Bind in Sekunden.")]
    public float autoBindRetryInterval = 0.25f;

    [Tooltip("Bevorzugte Tags für die Zielsuche (Reihenfolge wichtig).")]
    public string[] candidateTags = new[] { "Probe", "Player", "Ship" };

    [Tooltip("Heuristik über Namen, falls kein Tag/Controller gefunden.")]
    public string[] candidateNamesContains = new[] { "Probe", "Sonde" };

    [Header("Weltraum-Kamera Einstellungen")]
    [Tooltip("Standard-Rückstand zur Sonde")]
    public Vector3 defaultOffset = new(0f, 2f, -8f);

    [Tooltip("Minimum Entfernung zur Sonde (in Unity Units)")]
    public float minDistance = 0.1f;

    [Tooltip("Maximum Entfernung zur Sonde (für Übersicht)")]
    public float maxDistance = 1000f;

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
    [Tooltip("Automatisch auf nahe Objekte fokussieren (falls Registry vorhanden).")]
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

    [Tooltip("Dynamisches FOV basierend auf Abstand/Geschwindigkeit")]
    public bool dynamicFOV = true;

    [Header("Collision & Clipping")]
    [Tooltip("Kamera-Kollision mit Objekten verhindern")]
    public bool preventCollision = false; // Im Weltraum meist aus

    [Tooltip("Layer für Kollisions-Erkennung")]
    public LayerMask collisionLayers = ~0;

    [Tooltip("Radius für Kollisions-Erkennung")]
    public float collisionRadius = 0.5f;

    // Internals
    private InputController controls;
    private Camera cam;
    private bool followTarget = true;
    private float yaw, pitch;
    private Vector3 currentOffset;
    private float currentDistance;
    private float targetDistance;
    private Vector3 velocityOffset;
    private bool isTransitioning = false;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.fieldOfView = defaultFOV;

        // Falls eine Scene/Render-Fehlkonfiguration vorlag:
        if (cam.clearFlags == CameraClearFlags.Nothing)
            cam.clearFlags = CameraClearFlags.Skybox;

        currentOffset = defaultOffset;
        currentDistance = defaultOffset.magnitude;
        targetDistance = currentDistance;

        InitializeInputSystem();

        // Sicherstellen, dass wir zu Beginn eine sinnvolle Pose haben:
        if (target != null)
        {
            ResetToDefaultView(immediatePosition: true);
        }

        // Nach Szenenwechsel erneut versuchen zu binden
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        if (target == null && autoBindOnStart)
        {
            StartCoroutine(AutoBindRoutine());
        }
    }

    private void OnDestroy()
    {
        controls?.Dispose();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Bei Eintritt in 10_Game oder generell nach Laden erneut binden
        if (autoBindOnStart)
        {
            StopAllCoroutines();
            StartCoroutine(AutoBindRoutine());
        }
    }

    private IEnumerator AutoBindRoutine()
    {
        float deadline = Time.time + autoBindWindowSeconds;
        Debug.Log("[CameraController] Auto-Bind gestartet...", this);
        while (target == null && Time.time < deadline)
        {
            TryAutoBind();
            if (target != null) break;
            yield return new WaitForSeconds(autoBindRetryInterval);
        }

        // Falls wir etwas gefunden haben, sofort sauber ausrichten
        if (target != null)
        {
            ResetToDefaultView(immediatePosition: true);
        }
    }

    /// <summary>Versucht in sinnvoller Reihenfolge, eine Sonde zu finden.</summary>
    private void TryAutoBind()
    {
        // 1) Bereits vom Inspector gesetzt?
        if (target != null) return;

        // 2) Über Controller-Typ (empfohlen)
        var probe = FindFirstObjectOfTypeSafe<MonoBehaviour>("ProbeController");
        if (probe != null) { SetTarget(probe.transform); return; }

        // 3) Über Tag
        foreach (var t in candidateTags)
        {
            var tagged = GameObject.FindGameObjectsWithTag(t).FirstOrDefault();
            if (tagged != null) { SetTarget(tagged.transform); return; }
        }

        // 4) Über Namen-Heuristik
        var all = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var byName = all.FirstOrDefault(tr =>
            candidateNamesContains.Any(key => tr.name.ToLower().Contains(key.ToLower())));
        if (byName != null) { SetTarget(byName); return; }

        // 5) Optional: Planet-/Hub-Registries, falls vorhanden (später per Code-Aufruf bindbar)
        // -> Externe Systeme können jederzeit CameraController.Instance.SetTarget(...) aufrufen.
    }

    // Utility: findet ein Objekt eines Typs per Namen ohne harte Abhängigkeit.
    private T FindFirstObjectOfTypeSafe<T>(string typeName) where T : class
    {
        // Versuche zunächst generisch
        var typed = Object.FindFirstObjectByType<MonoBehaviour>(FindObjectsInactive.Exclude);
        // Fallback: wir suchen alle MonoBehaviours und matchen nach Typname
        var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var hit = all.FirstOrDefault(m => m != null && m.GetType().Name == typeName);
        return hit as T;
    }

    private void InitializeInputSystem()
    {
        controls = new InputController();
        controls.Camera.Enable();

        // Freilook mit rechter Maustaste
        controls.Camera.Look.performed += ctx =>
        {
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
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
    }

    private void LateUpdate()
    {
        // Wenn das Target noch nicht da ist, regelmäßig (sparsam) nachbinden
        if (target == null)
            return;

        UpdateCameraDistance();
        UpdateCameraPosition();
        UpdateDynamicFOV();

        if (autoFocusNearbyObjects && !isTransitioning)
            CheckForNearbyObjects();
    }

    private void UpdateCameraDistance()
    {
        if (smoothZoom && Mathf.Abs(currentDistance - targetDistance) > 0.01f)
        {
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * zoomSmoothSpeed);
            UpdateOffsetFromDistance();
        }
    }

    private void UpdateOffsetFromDistance()
    {
        Vector3 direction = currentOffset.sqrMagnitude > 1e-6f ? currentOffset.normalized : Vector3.back;
        currentOffset = direction * currentDistance;
    }

    private void UpdateCameraPosition()
    {
        if (target == null) return;

        Vector3 desiredPosition;

        if (followTarget)
        {
            // Folge-Modus: Kamera folgt der Sonden-Rotation (Yaw-orientiert)
            Vector3 flatFwd = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (flatFwd.sqrMagnitude < 1e-6f) flatFwd = Vector3.forward;

            Quaternion targetRotation = Quaternion.LookRotation(flatFwd, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * targetTransitionSpeed);
            desiredPosition = target.position + transform.rotation * currentOffset;
        }
        else
        {
            // Freilook-Modus: Freie Rotation um das Ziel
            desiredPosition = target.position + transform.rotation * Vector3.back * currentDistance;
        }

        if (preventCollision)
        {
            desiredPosition = CheckCollision(target.position, desiredPosition);
        }

        if (isTransitioning)
        {
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocityOffset, 1f / Mathf.Max(0.01f, targetTransitionSpeed));
            if (Vector3.Distance(transform.position, desiredPosition) < 0.1f)
                isTransitioning = false;
        }
        else
        {
            transform.position = desiredPosition;
        }
    }

    private Vector3 CheckCollision(Vector3 targetPos, Vector3 desiredPos)
    {
        Vector3 direction = (desiredPos - targetPos).normalized;
        float distance = Vector3.Distance(targetPos, desiredPos);

        if (Physics.SphereCast(targetPos, collisionRadius, direction, out RaycastHit hit, distance, collisionLayers))
        {
            return targetPos + direction * Mathf.Max(0f, hit.distance - collisionRadius);
        }
        return desiredPos;
    }

    private void UpdateDynamicFOV()
    {
        if (!dynamicFOV || target == null) return;

        // FOV abhängig von Entfernung
        float distanceRatio = Mathf.InverseLerp(minDistance, maxDistance, currentDistance);
        float targetFov = Mathf.Lerp(defaultFOV, maxFOV, distanceRatio);

        // Optionaler Geschwindigkeits-Boost (Rigidbody vorhanden?)
        if (target.TryGetComponent<Rigidbody>(out var rb))
        {
            float speed = rb.linearVelocity.magnitude; // FIX: velocity statt linearVelocity
            float speedFov = Mathf.Clamp(speed * 0.5f, 0f, 20f);
            targetFov += speedFov;
        }

        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.deltaTime * 2f);
    }

    private void CheckForNearbyObjects()
    {
        // Optional: only if a registry exists in your project
        var registry = PlanetRegistry.Instance;
        if (registry == null) return;

        Transform closestObject = null;
        float closestDistance = autoFocusDistance;

        foreach (var obj in registry.NavTargets)
        {
            if (obj == target || obj == null) continue;
            float d = Vector3.Distance(transform.position, obj.position);
            if (d < closestDistance)
            {
                closestDistance = d;
                closestObject = obj;
            }
        }

        if (closestObject != null)
        {
            SetTarget(closestObject);
        }
    }

    private void ApplyFreeRotation()
    {
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    public void ResetToDefaultView(bool immediatePosition = false)
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

        if (immediatePosition)
        {
            // Stelle sicher, dass beim ersten Frame kein „schwarzer“ Offset passiert
            transform.position = target.position + transform.rotation * currentOffset;
        }
        else
        {
            isTransitioning = true; // sanft nachziehen
        }
    }

    public void SetTarget(Transform newTarget)
    {
        if (newTarget == null) return;

        target = newTarget;
        ResetToDefaultView(immediatePosition: true);
        Debug.Log($"[CameraController] Target gesetzt: {newTarget.name}", this);
    }

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

    private void OnDrawGizmosSelected()
    {
        if (target == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, target.position);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(target.position, minDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(target.position, maxDistance);

        if (autoFocusNearbyObjects)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, autoFocusDistance);
        }
    }
}
