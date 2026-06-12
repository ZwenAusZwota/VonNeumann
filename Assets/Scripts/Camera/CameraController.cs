// Assets/Scripts/Core/CameraController.cs
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Target (Sonde)")]
    [Tooltip("Falls leer, bindet sich die Kamera beim Start automatisch an die Sonde.")]
    public Transform target;

    [Header("Automatische Bindung")]
    public bool autoBindOnStart = true;
    public float autoBindWindowSeconds = 5f;
    public float autoBindRetryInterval = 0.25f;
    public string[] candidateTags = new[] { "Probe", "Player", "Ship" };
    public string[] candidateNamesContains = new[] { "Probe", "Sonde" };

    [Header("Orbit")]
    [Tooltip("Standard-R?ckstand zur Sonde (Z = Entfernung, Y = H?hen-Offset)")]
    public Vector3 defaultOffset = new(0f, 2f, -8f);

    public float minDistance = 0.1f;
    public float maxDistance = 1000f;

    [Header("Maus ? Orbit (rechte Maustaste)")]
    public float mouseSensitivity = 0.15f;
    public float pitchLimit = 85f;
    public bool invertY = false;
    [Tooltip("Mauszeiger beim Orbit verstecken/sperren")]
    public bool lockCursorWhileOrbiting = false;
    [Tooltip("Kein Orbit, wenn der Mauszeiger ?ber UI liegt")]
    public bool blockOrbitOverUI = true;

    [Header("Zoom")]
    public float zoomSpeed = 0.1f;
    public bool smoothZoom = true;
    public float zoomSmoothSpeed = 5f;

    [Header("Weltraum-Navigation")]
    public bool autoFocusNearbyObjects = true;
    public float autoFocusDistance = 50f;
    public float targetTransitionSpeed = 2f;

    [Header("FOV & Rendering")]
    public float defaultFOV = 60f;
    public float maxFOV = 120f;
    public bool dynamicFOV = true;

    [Header("Collision & Clipping")]
    public bool preventCollision = false;
    public LayerMask collisionLayers = ~0;
    public float collisionRadius = 0.5f;

    private InputController controls;
    private Camera cam;
    private float yaw;
    private float pitch;
    private float currentDistance;
    private float targetDistance;
    private float orbitHeight;
    private Vector3 velocityPosition;
    private bool isTransitioning;
    private bool orbitInputActive;
    private float pendingZoomDelta;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.fieldOfView = defaultFOV;

        if (cam.clearFlags == CameraClearFlags.Nothing)
            cam.clearFlags = CameraClearFlags.Skybox;

        orbitHeight = defaultOffset.y;
        currentDistance = GetDefaultOrbitDistance();
        targetDistance = currentDistance;

        InitializeInputSystem();

        if (target != null)
            ResetToDefaultView(immediatePosition: true);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        if (target == null && autoBindOnStart)
            StartCoroutine(AutoBindRoutine());
    }

    private void OnEnable()
    {
        controls?.Camera.Enable();
    }

    private void OnDisable()
    {
        if (controls != null && controls.Camera.enabled)
            controls.Camera.Disable();

        if (orbitInputActive)
            EndOrbitInput();
    }

    private void OnDestroy()
    {
        ShutdownInputSystem();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (autoBindOnStart)
        {
            StopAllCoroutines();
            StartCoroutine(AutoBindRoutine());
        }
    }

    private IEnumerator AutoBindRoutine()
    {
        float deadline = Time.time + autoBindWindowSeconds;
        while (target == null && Time.time < deadline)
        {
            TryAutoBind();
            if (target != null) break;
            yield return new WaitForSeconds(autoBindRetryInterval);
        }

        if (target != null)
            ResetToDefaultView(immediatePosition: true);
    }

    private void TryAutoBind()
    {
        if (target != null) return;

        var probe = FindFirstObjectByTypeSafe<MonoBehaviour>("ProbeController");
        if (probe != null) { SetTarget(probe.transform); return; }

        foreach (var t in candidateTags)
        {
            try
            {
                var tagged = GameObject.FindGameObjectsWithTag(t).FirstOrDefault();
                if (tagged != null) { SetTarget(tagged.transform); return; }
            }
            catch (UnityException ex)
            {
                Debug.LogWarning($"[CameraController] Tag '{t}' ist nicht definiert: {ex.Message}");
            }
        }

        var all = FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
        var byName = all.FirstOrDefault(tr =>
            candidateNamesContains.Any(key => tr.name.ToLower().Contains(key.ToLower())));
        if (byName != null) SetTarget(byName);
    }

    private T FindFirstObjectByTypeSafe<T>(string typeName) where T : class
    {
        var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
        var hit = all.FirstOrDefault(m => m != null && m.GetType().Name == typeName);
        return hit as T;
    }

    private void InitializeInputSystem()
    {
        controls = new InputController();

        controls.Camera.RightClick.started += OnRightClickStarted;
        controls.Camera.RightClick.canceled += OnRightClickCanceled;
        controls.Camera.Zoom.performed += OnZoomPerformed;
        controls.Camera.Reset.performed += OnResetPerformed;
    }

    private void ShutdownInputSystem()
    {
        if (controls == null) return;

        controls.Camera.RightClick.started -= OnRightClickStarted;
        controls.Camera.RightClick.canceled -= OnRightClickCanceled;
        controls.Camera.Zoom.performed -= OnZoomPerformed;
        controls.Camera.Reset.performed -= OnResetPerformed;

        if (controls.Camera.enabled)
            controls.Camera.Disable();

        controls.Dispose();
        controls = null;
    }

    private void OnRightClickStarted(InputAction.CallbackContext _) => BeginOrbitInput();
    private void OnRightClickCanceled(InputAction.CallbackContext _) => EndOrbitInput();
    private void OnZoomPerformed(InputAction.CallbackContext ctx) => pendingZoomDelta += ctx.ReadValue<float>();
    private void OnResetPerformed(InputAction.CallbackContext _) => ResetToDefaultView();

    private void BeginOrbitInput()
    {
        orbitInputActive = true;
        if (lockCursorWhileOrbiting)
            Cursor.lockState = CursorLockMode.Locked;
    }

    private void EndOrbitInput()
    {
        orbitInputActive = false;
        Cursor.lockState = CursorLockMode.None;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        ProcessOrbitInput();
        ProcessZoomInput();
        UpdateCameraDistance();
        UpdateCameraTransform();
        UpdateDynamicFOV();

        if (autoFocusNearbyObjects && !isTransitioning)
            CheckForNearbyObjects();
    }

    private void ProcessOrbitInput()
    {
        if (!orbitInputActive) return;
        if (blockOrbitOverUI && IsPointerOverUI()) return;

        Vector2 delta = controls.Camera.Look.ReadValue<Vector2>();
        if (delta.sqrMagnitude < 0.0001f) return;

        yaw += delta.x * mouseSensitivity;
        pitch += (invertY ? delta.y : -delta.y) * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -pitchLimit, pitchLimit);
    }

    private void ProcessZoomInput()
    {
        if (target == null || Mathf.Abs(pendingZoomDelta) < 0.001f) return;

        if (blockOrbitOverUI && IsPointerOverUI())
        {
            pendingZoomDelta = 0f;
            return;
        }

        float scrollInput = pendingZoomDelta;
        pendingZoomDelta = 0f;

        currentDistance = Vector3.Distance(transform.position, GetOrbitPivot());
        float zoomFactor = 1f + (scrollInput * zoomSpeed);
        targetDistance = Mathf.Clamp(currentDistance / zoomFactor, minDistance, maxDistance);

        if (!smoothZoom)
            currentDistance = targetDistance;
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void UpdateCameraDistance()
    {
        if (!smoothZoom || Mathf.Abs(currentDistance - targetDistance) <= 0.01f) return;
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * zoomSmoothSpeed);
    }

    private Vector3 GetOrbitPivot()
    {
        return target.position + Vector3.up * orbitHeight;
    }

    private Vector3 GetOrbitDirection()
    {
        return Quaternion.Euler(pitch, yaw, 0f) * Vector3.back;
    }

    private void UpdateCameraTransform()
    {
        Vector3 pivot = GetOrbitPivot();
        Vector3 direction = GetOrbitDirection().normalized;
        Vector3 desiredPosition = pivot + direction * currentDistance;

        if (preventCollision)
            desiredPosition = CheckCollision(pivot, desiredPosition);

        // Kamera schaut immer auf den Orbit-Pivot (Sonde)
        transform.rotation = Quaternion.LookRotation(pivot - desiredPosition, Vector3.up);

        if (isTransitioning)
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref velocityPosition,
                1f / Mathf.Max(0.01f, targetTransitionSpeed));

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
            return targetPos + direction * Mathf.Max(0f, hit.distance - collisionRadius);

        return desiredPos;
    }

    private void UpdateDynamicFOV()
    {
        if (!dynamicFOV) return;

        float distanceRatio = Mathf.InverseLerp(minDistance, maxDistance, currentDistance);
        float targetFov = Mathf.Lerp(defaultFOV, maxFOV, distanceRatio);

        if (target.TryGetComponent<Rigidbody>(out var rb))
        {
            float speedFov = Mathf.Clamp(rb.linearVelocity.magnitude * 0.5f, 0f, 20f);
            targetFov += speedFov;
        }

        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.deltaTime * 2f);
    }

    private void CheckForNearbyObjects()
    {
        // Sonde nicht durch Auto-Fokus ersetzen (z. B. beim Hineinzoomen)
        if (target != null && target.GetComponent<ProbeController>() != null)
            return;

        var registry = ServiceContainer.Instance?.Get<PlanetRegistry>();
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
            SetTarget(closestObject);
    }

    public void ResetToDefaultView(bool immediatePosition = false)
    {
        if (target == null) return;

        orbitHeight = defaultOffset.y;
        currentDistance = GetDefaultOrbitDistance();
        targetDistance = currentDistance;
        cam.fieldOfView = defaultFOV;
        isTransitioning = false;

        Vector3 flatFwd = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        if (flatFwd.sqrMagnitude < 0.001f) flatFwd = Vector3.forward;

        yaw = Mathf.Atan2(flatFwd.x, flatFwd.z) * Mathf.Rad2Deg;
        pitch = 0f;

        if (immediatePosition)
            ApplyOrbitPose();
        else
            isTransitioning = true;
    }

    public void SetTarget(Transform newTarget)
    {
        if (newTarget == null) return;
        target = newTarget;
        ResetToDefaultView(immediatePosition: true);
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
    public bool IsFollowing() => target != null;
    public bool IsOrbiting => orbitInputActive;

    private float GetDefaultOrbitDistance()
    {
        float horizontal = new Vector3(defaultOffset.x, 0f, defaultOffset.z).magnitude;
        return Mathf.Clamp(horizontal > 0.01f ? horizontal : Mathf.Abs(defaultOffset.z), minDistance, maxDistance);
    }

    private void ApplyOrbitPose()
    {
        if (target == null) return;

        Vector3 pivot = GetOrbitPivot();
        Vector3 direction = GetOrbitDirection().normalized;
        Vector3 position = pivot + direction * currentDistance;
        transform.position = position;
        transform.rotation = Quaternion.LookRotation(pivot - position, Vector3.up);
    }

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
