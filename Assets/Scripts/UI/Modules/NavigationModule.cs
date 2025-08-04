// Assets/Scripts/UI/Modules/NavigationModule.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class NavigationModule : UIModule
{
    [Header("Navigation UI")]
    public TextMeshProUGUI textCurrentTarget;
    public TextMeshProUGUI textSpeed;
    public Button btnAutoPilot;
    
    [Header("Colors")]
    public Color autoPilotOffColor = new Color32(0x61, 0x5E, 0x5E, 0xFF);
    public Color autoPilotOnColor = new Color32(0x5A, 0xFF, 0x6E, 0xFF);
    
    private ProbeController probe;
    private Rigidbody probeRb;
    private bool autoPilotActive;
    
    public event Action<bool> OnAutoPilotToggled;
    
    protected override void OnInitialize()
    {
        btnAutoPilot.onClick.AddListener(ToggleAutopilot);
        SetAutoPilotButtonColor(false);
    }
    
    public void SetProbe(ProbeController probeController)
    {
        if (probe != null)
        {
            probe.AutoPilotStarted -= OnAutoPilotStarted;
            probe.AutoPilotStopped -= OnAutoPilotStopped;
        }
        
        probe = probeController;
        probeRb = probe.GetComponent<Rigidbody>();
        
        probe.AutoPilotStarted += OnAutoPilotStarted;
        probe.AutoPilotStopped += OnAutoPilotStopped;
    }
    
    public void SetNavigationTarget(SystemObject target)
    {
        if (target == null)
        {
            textCurrentTarget.text = "No Target";
            return;
        }
        
        textCurrentTarget.text = target.DisplayName;
        
        if (probe != null)
        {
            probe.SetNavTarget(target.GameObject.transform);
            DeactivateAutopilot();
        }
    }
    
    void Update()
    {
        if (probeRb != null)
        {
            float speed = probeRb.linearVelocity.magnitude;
            textSpeed.text = $"Speed: {speed:F1} m/s";
        }
    }
    
    void ToggleAutopilot()
    {
        if (probe == null) return;
        
        if (autoPilotActive)
            probe.StopAutopilot();
        else
            probe.StartAutopilot();
    }
    
    void DeactivateAutopilot()
    {
        if (probe == null) return;
        probe.StopAutopilot();
    }
    
    void OnAutoPilotStarted()
    {
        autoPilotActive = true;
        SetAutoPilotButtonColor(true);
        OnAutoPilotToggled?.Invoke(true);
    }
    
    void OnAutoPilotStopped()
    {
        autoPilotActive = false;
        SetAutoPilotButtonColor(false);
        OnAutoPilotToggled?.Invoke(false);
    }
    
    void SetAutoPilotButtonColor(bool active)
    {
        var targetGraphic = btnAutoPilot.targetGraphic;
        if (targetGraphic != null)
            targetGraphic.color = active ? autoPilotOnColor : autoPilotOffColor;
    }
}