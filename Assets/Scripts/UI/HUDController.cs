// Assets/Scripts/UI/HUDController.cs
using TMPro;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    //[Header("UI References")]
    //public TextMeshProUGUI textStar;
    //public TextMeshProUGUI textPlanet;
    //public TextMeshProUGUI textCredits;
    //public TextMeshProUGUI textCargo;
    //public TextMeshProUGUI textCooldown;
    //public TextMeshProUGUI textSpeed;
    //public TextMeshProUGUI textTime;
    //public Button btnAutoPilot;
    //public Button btnNearScan;
    ////public Button btnMine;

    //public PlanetIndicatorManager indicatorManager;

    //public SystemObject CurrentSelection { get; private set; }


    //Color colOff = new Color32(0x61, 0x5E, 0x5E, 0xFF);
    //Color colOn = new Color32(0x5A, 0xFF, 0x6E, 0xFF);

    //bool autoPilotActive;

    //[Header("Links")]
    //public ServerConnector connector;

    //// Zwischengespeicherter Stand
    //private PlayerDto player;
    //private DateTime? miningReadyAt;

    //private Rigidbody probeRb;

    //[Header("Objects")]
    ////public PlanetListUI planetList;
    //public ObjectListManager objectList;
    //public ObjectListManager scanList;
    ///* Verweis auf die Spieler‑Sonde (Navigation) */
    //ProbeController probe;
    //ProbeInventory inv;

    ///* Events */
    //public event Action NearScanClicked;

    //void Awake()
    //{
    //    btnAutoPilot.onClick.AddListener(ToggleAutopilot);
    //    btnNearScan.onClick.AddListener(OnNearScanClicked);
    //    //btnMineScan.onClick.AddListener(OnMiningClicked);
        
    //    SetButtonColor(false);
    //}

    //void OnEnable()
    //{
    //    //connector.OnInit += HandleInit;
    //    connector.OnMineResult += HandleMineResult;
    //    objectList.OnSelected += OnObjectSelected;
    //    scanList.OnSelected += OnObjectSelected;
    //    // Weitere Events (Trade, CargoUpdate) nach Bedarf anhängen
    //}

    //void OnDisable()
    //{
    //    //connector.OnInit -= HandleInit;
    //    connector.OnMineResult -= HandleMineResult;
    //    objectList.OnSelected -= OnObjectSelected;
    //    scanList.OnSelected -= OnObjectSelected;
    //    probe.AutoPilotStopped -= () => SetButtonColor(false); // Autopilot-Stop-Event abonnieren
    //    probe.AutoPilotStarted -= () => SetButtonColor(true); // Autopilot-Start-Event abonnieren
    //}

    //public void HandleInit(InitPayload payload)
    //{
    //    player = payload.player;
    //    textPlanet.text = "";
    //}

    //public void SetObjects(List<SystemObject> objects)
    //{
    //    objectList.AddObjects(objects);
    //}

    //public void UpdateScan(List<SystemObject> objects)
    //{
    //    scanList.AddObjects(objects);
    //}

    ///* Auswahl‑Callback der PlanetList */
    //void OnObjectSelected(SystemObject dto)
    //{
    //    if (dto == null) return;

    //    textPlanet.text = dto.DisplayName;
    //    CurrentSelection = dto;  


    //    /* Navigation: Transform suchen & Probe informieren */
    //    var tf = dto.GameObject.transform;
    //    probe?.SetNavTarget(tf);
    //    DeactivateAutopilot();  // Autopilot deaktivieren, wenn ein Objekt ausgewählt wird
    //}

    //public void SetActiveBody(string bodyName)
    //{
    //    textPlanet.text = bodyName;
    //}

    //public void HandleMineResult(MineResult res)
    //{
    //    // Credits & Cargo updaten
    //    player.credit = res.newCredit;
    //    textCredits.text = $"Credits: {player.credit:N0}";
    //    UpdateCargoText();

    //    // Cooldown berechnen
    //    miningReadyAt = DateTime.UtcNow.AddSeconds(res.cooldown);
    //}

    //void Update()
    //{
    //    // Cooldown‑Anzeige
    //    if (miningReadyAt.HasValue)
    //    {
    //        double sec = (miningReadyAt.Value - DateTime.UtcNow).TotalSeconds;
    //        if (sec > 0)
    //            textCooldown.text = $"Mining CD: {sec:F0}s";
    //        else
    //        {
    //            textCooldown.text = "Mining CD: ready";
    //            miningReadyAt = null;
    //        }
    //    }

    //    // ---- Geschwindigkeits‑Anzeige -------------------------
    //    if (probeRb != null)
    //    {
    //        float speed = probeRb.linearVelocity.magnitude;     // m/s
    //        textSpeed.text = $"Speed: {speed:F1} m/s";
    //    }
    //}

    //void HandleCargoChanged(float used, float max) =>
    //textCargo.text = $"Cargo: {used:N0}/{max:N0} m³";

    //void UpdateCargoText()
    //{
    //    float sum = 0;
    //    foreach (var kv in player.cargo) sum += kv.Value;
    //    textCargo.text = $"Cargo: {sum:N0} t";
    //}

    //public void SetProbe(Rigidbody rb)
    //{
    //    probeRb = rb;
    //    probe = rb.GetComponent<ProbeController>();
    //    inv = rb.GetComponent<ProbeInventory>();
    //    if (inv) inv.CargoChanged += HandleCargoChanged;
    //    probe.AutoPilotStopped += () => SetButtonColor(false); // Autopilot-Stop-Event abonnieren
    //    probe.AutoPilotStarted += () => SetButtonColor(true); // Autopilot-Start-Event abonnieren
    //}

    //void ToggleAutopilot()
    //{
    //    if (probe == null) return;

    //    if (autoPilotActive)
    //        probe.StopAutopilot();     // s. unten
    //    else
    //        probe.StartAutopilot();    // s. unten

    //    autoPilotActive = !autoPilotActive;
    //    SetButtonColor(autoPilotActive);
    //}

    //void DeactivateAutopilot()
    //{
    //    if (probe == null) return;
    //    probe.StopAutopilot();
    //    autoPilotActive = false;
    //    SetButtonColor(false);
    //}

    //void SetButtonColor(bool on)
    //{
    //    var tg = btnAutoPilot.targetGraphic;
    //    if (tg != null) tg.color = on ? colOn : colOff;
    //}

    //void OnNearScanClicked()       
    //{
    //    //if (probeRb == null) return;
    //    //GameManager.Instance?.PerformNearScan(probeRb.transform.position);
    //    NearScanClicked?.Invoke();
    //}

    //public void OnMiningClicked()
    //{
    //    // Mining-Logik hier implementieren
    //    // z.B. MiningController.Instance.TryStartMining(CurrentSelection);
    //    probe.GetComponent<MiningController>().StartMining();
    //}

    //public void SetCargoText(float used, float max) =>
    //    textCargo.text = $"Cargo: {used:N0}/{max:N0} m³";
}
