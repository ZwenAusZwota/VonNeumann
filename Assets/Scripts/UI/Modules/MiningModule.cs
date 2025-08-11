// Assets/Scripts/UI/Modules/MiningModule.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class MiningModule : UIModule
{
    [Header("Mining UI")]
    public Button btnStartMining;
    public TextMeshProUGUI textMiningStatus;
    public TextMeshProUGUI textMiningCooldown;
    public TextMeshProUGUI textMiningYield;
    
    private DateTime? miningReadyAt;
    //private MiningController miningController;
    
    public event Action OnMiningRequested;
    
    protected override void OnInitialize()
    {
        if (btnStartMining != null)
            btnStartMining.onClick.AddListener( () => OnMiningRequested?.Invoke()); //OnMiningRequested?.Invoke());
        
        UpdateMiningStatus("Ready");
    }
    
    void MiningRequested()
    {
        //if (miningController != null && miningController.CanMine())
        //{
        //    OnMiningRequested?.Invoke();
        //    UpdateMiningStatus("Mining in progress...");
        //}
        //else
        //{
        //    UpdateMiningStatus("Cannot mine now");
        //}
    }

    public void UpdateMiningStatus(string status)
    {
        if (textMiningStatus != null)
            textMiningStatus.text = $"{status}";
    }
    
    public void HandleMiningResult(MineResult result)
    {
        if (result.cooldown > 0)
        {
            StartMiningCooldown(result.cooldown);
        }
        
        if (textMiningYield != null)
        {
            textMiningYield.text = $"Last Yield: +{result.newCredit} Credits";//$"Last Yield: +{result.newCredit - result.previousCredit:N0} Credits";
        }
    }
    
    public void StartMiningCooldown(float seconds)
    {
        miningReadyAt = DateTime.UtcNow.AddSeconds(seconds);
        if (btnStartMining != null)
            btnStartMining.interactable = false;
    }
    
    void Update()
    {
        UpdateCooldownDisplay();
    }
    
    void UpdateCooldownDisplay()
    {
        if (miningReadyAt.HasValue)
        {
            double remaining = (miningReadyAt.Value - DateTime.UtcNow).TotalSeconds;
            if (remaining > 0)
            {
                if (textMiningCooldown != null)
                    textMiningCooldown.text = $"Mining CD: {remaining:F0}s";
            }
            else
            {
                if (textMiningCooldown != null)
                    textMiningCooldown.text = "Mining CD: Ready";
                
                miningReadyAt = null;
                if (btnStartMining != null)
                    btnStartMining.interactable = true;
            }
        }
    }
}