// Assets/Scripts/UI/Modules/ScanningModule.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class ScanningModule : UIModule
{
    [Header("Scanning UI")]
    public Button btnNearScan;
    public Button btnDeepScan;
    public TextMeshProUGUI textScanStatus;
    public ObjectListManager scanResultsList;
    
    [Header("Cooldowns")]
    public TextMeshProUGUI textScanCooldown;
    
    private DateTime? scanReadyAt;
    
    public event Action OnNearScanRequested;
    public event Action OnDeepScanRequested;
    
    protected override void OnInitialize()
    {
        btnNearScan.onClick.AddListener(() => OnNearScanRequested?.Invoke());
        
        if (btnDeepScan != null)
            btnDeepScan.onClick.AddListener(() => OnDeepScanRequested?.Invoke());
        
        UpdateScanStatus("Ready");
    }
    
    public void UpdateScanResults(System.Collections.Generic.List<SystemObject> results)
    {
        if (scanResultsList != null)
        {
            scanResultsList.Clear();
            scanResultsList.AddObjects(results);
        }
    }
    
    public void UpdateScanStatus(string status)
    {
        if (textScanStatus != null)
            textScanStatus.text = $"Scan: {status}";
    }
    
    public void StartScanCooldown(float seconds)
    {
        scanReadyAt = DateTime.UtcNow.AddSeconds(seconds);
        btnNearScan.interactable = false;
        
        if (btnDeepScan != null)
            btnDeepScan.interactable = false;
    }
    
    void Update()
    {
        UpdateCooldownDisplay();
    }
    
    void UpdateCooldownDisplay()
    {
        if (scanReadyAt.HasValue)
        {
            double remaining = (scanReadyAt.Value - DateTime.UtcNow).TotalSeconds;
            if (remaining > 0)
            {
                if (textScanCooldown != null)
                    textScanCooldown.text = $"Scan CD: {remaining:F0}s";
            }
            else
            {
                if (textScanCooldown != null)
                    textScanCooldown.text = "Scan CD: Ready";
                
                scanReadyAt = null;
                btnNearScan.interactable = true;
                
                if (btnDeepScan != null)
                    btnDeepScan.interactable = true;
            }
        }
    }
}