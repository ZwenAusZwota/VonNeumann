// Assets/Scripts/Core/CameraController.cs
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Target (Sonde)")]
    public Transform target;

    [Header("Offset & Zoom")]
    public Vector3 defaultOffset = new(0, 1.5f, -4f); // Standard‑Rückstand
    public float minDistance = 2f;                   // in Units
    public float maxDistance = 20f;
    public float zoomSpeed = 0.05f;                // Scroll‑Skalierung

    [Header("Freilook‑Sensitivity")]
    public float mouseSensitivity = 0.2f;
    public float pitchLimit = 80f;

    // intern --------------------------------------------------------------
    ProbeControls controls;
    bool followTarget = true;
    float yaw, pitch;
    Vector3 currentOffset;   // wird hinein‑/herausgezoomt

    //----------------------------------------------------------------------
    void Awake()
    {
        currentOffset = defaultOffset;

        controls = new ProbeControls();
        controls.Camera.Enable();

        // 1) Freilook
        controls.Camera.Look.performed += ctx =>
        {
            if (Mouse.current.rightButton.isPressed)
            {
                followTarget = false;
                Vector2 d = ctx.ReadValue<Vector2>() * mouseSensitivity;
                yaw += d.x;
                pitch -= d.y;
                //pitch = Mathf.Clamp(pitch, -pitchLimit, pitchLimit);
                ApplyRotation();
            }
        };
        controls.Camera.RightClick.started += _ => followTarget = false;

        // 2) Zoom (nur wenn Follow aktiv)
        controls.Camera.Zoom.performed += ctx =>
        {
            if (!followTarget) return;

            float scroll = ctx.ReadValue<float>(); // + up, – down
            float factor = 1f - scroll * zoomSpeed;
            Vector3 newOffset = currentOffset * factor;

            float dist = newOffset.magnitude;
            if (dist >= minDistance && dist <= maxDistance)
                currentOffset = newOffset;
        };

        // 3) Reset (F1)
        controls.Camera.Reset.performed += _ => SnapBehindTarget(true);
    }

    void OnDestroy() => controls.Dispose();

    public void ResetCamera() => SnapBehindTarget(true);

    //----------------------------------------------------------------------
    void LateUpdate()
    {
        if (target == null) return;

        if (followTarget)
            transform.rotation = Quaternion.LookRotation(target.forward, Vector3.up);

        transform.position = target.position + transform.rotation * currentOffset;
    }

    //----------------------------------------------------------------------
    void ApplyRotation()
    {
        transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        // Position bleibt ‑ so behält Kamera ihren Standpunkt
    }

    void SnapBehindTarget(bool resetZoom)
    {
        if (target == null) return;

        followTarget = true;
        yaw = target.eulerAngles.y;
        pitch = 0f;

        if (resetZoom) currentOffset = defaultOffset;

        transform.rotation = Quaternion.LookRotation(target.forward, Vector3.up);
        transform.position = target.position + transform.rotation * currentOffset;
    }
}
