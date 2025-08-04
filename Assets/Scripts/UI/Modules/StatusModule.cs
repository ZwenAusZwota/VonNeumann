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
    
    public void UpdateCargoFromInventory(ProbeInventory inventory)
    {
        if (inventory != null && player != null)
        {
            float sum = 0;
            foreach (var kv in player.cargo) 
                sum += kv.Value;
            textCargo.text = $"Cargo: {sum:N0} t";
        }
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