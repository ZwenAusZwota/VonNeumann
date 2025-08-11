// Assets/Scripts/UI/Modules/StatusModule.cs
using UnityEngine;
using TMPro;
using System;

public class StatusModule : UIModule
{
    [Header("Status Display")]
    public TextMeshProUGUI textCredits;
    public TextMeshProUGUI textCargo;
    public TextMeshProUGUI textTime;
    public TextMeshProUGUI textSystemInfo;
    
    private PlayerDto player;
    
    protected override void OnInitialize()
    {
        // Initialize with default values
        UpdateCredits(0);
        UpdateCargo(0, 0);
    }
    
    public void SetPlayer(PlayerDto playerData)
    {
        player = playerData;
        UpdateCredits(player.credit);
    }
    
    public void UpdateCredits(float credits)
    {
        textCredits.text = $"Credits: {credits:N0}";
    }
    
    public void UpdateCargo(float used, float max)
    {
        textCargo.text = $"Cargo: {used:N0}/{max:N0} m³";
    }
    
    public void UpdateCargoStatus(string cargo)
    {
        textCargo.text = $"Cargo: {cargo} t";
    }
    
    public void SetSystemInfo(string systemName)
    {
        if (textSystemInfo != null)
            textSystemInfo.text = $"System: {systemName}";
    }
    
    void Update()
    {
        // Update game time display
        if (textTime != null)
        {
            textTime.text = $"Time: {DateTime.Now:HH:mm:ss}";
        }
    }
}