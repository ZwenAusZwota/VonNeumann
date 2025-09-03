// Assets/Scripts/Entities/ProbeController.cs
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

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
    public GameObject prefab;

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
    private float _lastDistanceRaw = -1f;

    float radialSpeed;
    bool isBraking;

    /*────────────────────────────────────────── Cached references */
    InputController inputController;
    Rigidbody rb;
    PlanetRegistry registry;
    ProbeAutopilot autopilot;
    ProbeScanner scanner;
    ProbeMiner miner;
    InventoryController inventory;

    public event Action AutoPilotStarted;
    public event Action AutoPilotStopped;
    public event Action<string> StatusUpdated;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        registry = PlanetRegistry.Instance;
        inputController = new InputController();
        autopilot = GetComponent<ProbeAutopilot>();
        scanner = GetComponent<ProbeScanner>();
        miner = GetComponent<ProbeMiner>();
        inventory = GetComponent<InventoryController>();

        transform.localScale = Vector3.one * spawnScale;
        rb.maxAngularVelocity = maxDegPerSec * Mathf.Deg2Rad * 1.5f;

        // ⚠️ Robust: Registrierung erst, wenn HubRegistry.Instance verfügbar ist
        var hubInfo = new HubRegistry.HubInfo
        {
            Id = "probe01",
            DisplayName = "Bob 1",
            Kind = "Probe",
            LastKnownPos = transform.position
        };
        SafeRegisterHub(hubInfo);
    }

    void OnEnable()
    {
        inputController.Probe.Enable();
        var map = inputController.Probe;

        map.Rotate.performed += ctx => rotateInput = ctx.ReadValue<Vector2>();
        map.Rotate.canceled += _ => rotateInput = Vector2.zero;

        map.Roll.performed += ctx => rollInput = ctx.ReadValue<float>();
        map.Roll.canceled += _ => rollInput = 0f;

        map.Thrust.performed += ctx => thrustInput = ctx.ReadValue<float>();
        map.Thrust.canceled += _ => thrustInput = 0f;

        map.Reset.performed += _ => HandleMinusKey();
        map.SpawnPrefab.performed += _ => spawnPrefab();

        // Saubere Event-Handler (und null-sicher)
        if (autopilot != null)
        {
            autopilot.AutoPilotStarted += HandleAutoPilotStarted;
            autopilot.AutoPilotStopped += HandleAutoPilotStopped;
            autopilot.StatusUpdated += HandleStatusUpdated;
        }
        else
        {
            Debug.LogWarning("ProbeController: Kein ProbeAutopilot auf dem Prefab.");
        }

        if (miner != null)
        {
            miner.StatusUpdated += HandleMinerStatusUpdated;
        }
    }

    void OnDisable()
    {
        inputController.Disable();

        if (autopilot != null)
        {
            autopilot.AutoPilotStarted -= HandleAutoPilotStarted;
            autopilot.AutoPilotStopped -= HandleAutoPilotStopped;
            autopilot.StatusUpdated -= HandleStatusUpdated;
        }

        if (miner != null)
        {
            miner.StatusUpdated -= HandleMinerStatusUpdated;
        }
    }

    private void OnDestroy() => inputController?.Dispose();

    private void Start()
    {
        // HUD-Auswahl beim Spawn (null-sicher)
        HUDBindingService.I?.Select(this.gameObject);
    }

    public void OnSelectedByPlayer()
    {
        HUDBindingService.I?.Select(this.gameObject);
    }

    void Update()
    {
        var kbd = Keyboard.current;
        if (kbd == null) return;

        if ((kbd.numpadPlusKey != null && kbd.numpadPlusKey.wasPressedThisFrame) ||
            (kbd[Key.NumpadPlus] != null && kbd[Key.NumpadPlus].wasPressedThisFrame))
            StartAutopilot();

        if ((kbd.numpadMinusKey != null && kbd.numpadMinusKey.wasPressedThisFrame) ||
            (kbd[Key.NumpadMinus] != null && kbd[Key.NumpadMinus].wasPressedThisFrame))
            HandleMinusKey();
    }

    void spawnPrefab()
    {
        string prefabName = "Miner_MK1";
        if (prefab == null)
        {
            Debug.LogError($"Prefab '{prefabName}' konnte nicht geladen werden!");
            return;
        }
        Vector3 spawnPos = transform.position + transform.forward * 5f;
        GameObject instance = Instantiate(prefab, spawnPos, Quaternion.identity);
        instance.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        instance.transform.SetParent(null);
        Debug.Log($"Prefab '{prefabName}' gespawnt bei {spawnPos}");
    }

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

        float distanceRaw = Vector3.Distance(navTarget.position, transform.position);
        Distance = Mathf.RoundToInt(distanceRaw);

        float delta = Mathf.Abs(distanceRaw - _lastDistanceRaw);
        if (delta > 0f)
            CurrentSpeed = delta / Time.fixedDeltaTime;
        else
            CurrentSpeed = 0f;

        _lastDistanceRaw = distanceRaw;
    }
    #endregion

    #region Autopilot (Brücke zum Component)
    public void SetNavTarget(Transform tgt)
    {
        navTarget = tgt;
        if (autopilot != null) autopilot.SetNavTarget(tgt);
    }
    public void StartAutopilot() { if (autopilot != null) autopilot.StartAutopilot(); }
    public void StopAutopilot() { if (autopilot != null) autopilot.StopAutopilot(); }
    public bool IsAutopilotActive => autopilot != null && autopilot.IsAutopilotActive;
    #endregion

    /*====================================================================*/
    #region Manual flight & emergency brake
    void ManualFlightTick()
    {
        if (isBraking)
        {
            ApplyEmergencyBrake();
            return;
        }

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
        // In deiner bestehenden Datei wird linearVelocity verwendet – beibehalten.
        Vector3 v = rb.linearVelocity;
        if (v.sqrMagnitude < 0.01f)
        {
            rb.linearVelocity = Vector3.zero;
            isBraking = false;
            isDampingReset = true;
            return;
        }
        rb.AddForce(-v.normalized * brakeAccel, ForceMode.Acceleration);
    }
    #endregion

    #region Helpers & reset / abort
    void ResetProbe()
    {
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        isDampingReset = true;

        AutoPilotStopped?.Invoke();
    }

    void HandleMinusKey()
    {
        ResetProbe();
        // null-sicherer Abbruch
        autopilot?.AbortAutopilot(keepMomentum: true);
    }

    static float GuessBodyRadius(Transform body)
    {
        if (body.TryGetComponent<SphereCollider>(out var col))
            return body.lossyScale.x * col.radius;
        if (body.TryGetComponent<Renderer>(out var rend))
            return rend.bounds.extents.magnitude;
        return body.lossyScale.x * 0.5f;
    }

    // ───────────────────── Event-Handler (benannt) ─────────────────────
    void HandleAutoPilotStarted() => AutoPilotStarted?.Invoke();
    void HandleAutoPilotStopped() => AutoPilotStopped?.Invoke();
    void HandleStatusUpdated(string status) => StatusUpdated?.Invoke(status);
    void HandleMinerStatusUpdated()
    {
        // Platzhalter für spätere HUD-Updates (Cargo etc.)
    }
    #endregion

    /*====================================================================*/
    #region HubRegistry – robuste Registrierung
    void SafeRegisterHub(HubRegistry.HubInfo info)
    {
        if (HubRegistry.Instance != null)
        {
            HubRegistry.Instance.RegisterOrUpdate(info);
        }
        else
        {
            StartCoroutine(RegisterWhenHubReady(info));
        }
    }

    IEnumerator RegisterWhenHubReady(HubRegistry.HubInfo info)
    {
        int guard = 0;
        while (HubRegistry.Instance == null && guard < 300) // ~5 Sek. @60 FPS
        {
            guard++;
            yield return null;
        }

        if (HubRegistry.Instance != null)
            HubRegistry.Instance.RegisterOrUpdate(info);
        else
            Debug.LogWarning("ProbeController: HubRegistry.Instance blieb null – Registrierung übersprungen.");
    }
    #endregion
}
