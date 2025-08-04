// Assets/Scripts/UI/HUDControllerModular.cs
using UnityEngine;
using System.Collections.Generic;

public class HUDControllerModular : MonoBehaviour
{
    [Header("Server Connection")]
    public ServerConnector connector;
    
    /*[Header("Game References")]*/
    private ProbeController probe;
    
    // Module references (automatically found)
    private NavigationModule navigationModule;
    private StatusModule statusModule;
    private ScanningModule scanningModule;
    private MiningModule miningModule;
    private ObjectSelectionModule objectSelectionModule;
    
    void Awake()
    {
        // Modules will be automatically initialized by UIModuleManager
    }
    
    void Start()
    {
        // Get module references
        navigationModule = UIModuleManager.Instance.GetModule<NavigationModule>();
        statusModule = UIModuleManager.Instance.GetModule<StatusModule>();
        scanningModule = UIModuleManager.Instance.GetModule<ScanningModule>();
        miningModule = UIModuleManager.Instance.GetModule<MiningModule>();
        objectSelectionModule = UIModuleManager.Instance.GetModule<ObjectSelectionModule>();
        
        SetupEventConnections();
    }
    
    void SetupEventConnections()
    {
        // Connect server events
        if (connector != null)
        {
            connector.OnMineResult += HandleMineResult;
        }
        
        // Connect module events
        if (scanningModule != null)
        {
            scanningModule.OnNearScanRequested += PerformNearScan;
        }
        
        if (miningModule != null)
        {
            miningModule.OnMiningRequested += PerformMining;
        }
        
        if (objectSelectionModule != null)
        {
            objectSelectionModule.OnObjectSelected += HandleObjectSelection;
        }
        
        // Set up probe reference
        if (probe != null && navigationModule != null)
        {
            navigationModule.SetProbe(probe);
        }
    }
    
    public void HandleInit(InitPayload payload)
    {
        statusModule?.SetPlayer(payload.player);
    }

    public void SetProbe(Rigidbody rb)
    {
       /* probeRb = rb;
        probe = rb.GetComponent<ProbeController>();
        inv = rb.GetComponent<ProbeInventory>();
        if (inv) inv.CargoChanged += HandleCargoChanged;
        probe.AutoPilotStopped += () => SetButtonColor(false); // Autopilot-Stop-Event abonnieren
        probe.AutoPilotStarted += () => SetButtonColor(true); // Autopilot-Start-Event abonnieren
       */
    }

    public void SetSystemObjects(List<SystemObject> objects)
    {
        objectSelectionModule?.UpdateSystemObjects(objects);
    }
    
    public void UpdateNearScan(List<SystemObject> objects)
    {
        objectSelectionModule?.UpdateNearbyObjects(objects);
        scanningModule?.UpdateScanResults(objects);
    }
    
    void HandleObjectSelection(SystemObject selectedObject)
    {
        navigationModule?.SetNavigationTarget(selectedObject);
    }
    
    void HandleMineResult(MineResult result)
    {
        statusModule?.UpdateCredits(result.newCredit);
        miningModule?.HandleMiningResult(result);
    }
    
    void PerformNearScan()
    {
        // Implement near scan logic
        //GameManager.Instance?.PerformNearScan(probe.transform.position);
        GameManager.Instance?.PerformNearScan();
        scanningModule?.UpdateScanStatus("Scanning...");
        scanningModule?.StartScanCooldown(5f); // Example cooldown
    }
    
    void PerformMining()
    {
        // Implement mining logic
        var miningController = probe?.GetComponent<MiningController>();
        if (miningController != null)
        {
            miningController.StartMining();
            miningModule?.UpdateMiningStatus("Mining...");
        }
    }
}