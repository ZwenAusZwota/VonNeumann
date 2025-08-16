// Assets/Scripts/UI/HUDControllerModular.cs
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class HUDControllerModular : MonoBehaviour
{

    [Header("HUD-Bereiche")]
    public GameObject inventoryPanel;

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

    // Player Input
    private InputController inputController;
    private InputAction _toggleInventory;

    void Awake()
    {
        inputController = new InputController();
    }

    private void OnEnable()
    {
        inputController.HUD.Enable();
        inputController.HUD.ToggleInventory.performed += OnToggleInventoryPanel;
    }

    private void OnDestroy()
    {
        inputController?.Dispose();
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

        ToggleInventoryPanel(false); // Hide inventory panel by default
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

    //public void SetProbe(Rigidbody rb)
    public void SetProbe(ProbeController _probe)
    {
        probe = _probe;
        navigationModule.SetProbe(probe);
    }

    public void UpdateProbePosition(Vector3 position, float speed)
    {
        //navigationModule.UpdateProbePosition(position, speed);
    }

    public void SetSystemObjects(List<SystemObject> objects)
    {
        objectSelectionModule?.UpdateSystemObjects(objects);
    }
    
    public void UpdateNearScan(List<SystemObject> objects)
    {
        //objectSelectionModule?.UpdateNearbyObjects(objects);
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
    
    public void PerformNearScan()
    {
        // Implement near scan logic
        var scannController = probe?.GetComponent<ProbeScanner>();
        if(scannController != null)
        {
            scannController.PerformNearScan();
            scanningModule?.UpdateScanStatus("Scanning...");
        }
        
        //scanningModule?.StartScanCooldown(5f); // Example cooldown
    }
    
    public void PerformMining()
    {
        // Implement mining logic
        var miningController = probe?.GetComponent<ProbeMiner>();
        if (miningController != null)
        {
            Debug.Log("Starting mining operation...");
            miningController.StartMining();
        }
    }

    public void UpdateMiningStatus(string status)
    {
        miningModule?.UpdateMiningStatus(status);
    }

    public void UpdateCargoStatus(float used, float max)
    {
        statusModule?.UpdateCargoStatus($"{used:N0}/{max:N0}");
    }

    public void ToggleInventoryPanel(bool show)
    {
        if (inventoryPanel != null)
        {
            inventoryPanel.gameObject.SetActive(show);
        }
    }

    private void OnToggleInventoryPanel(InputAction.CallbackContext ctx)
    {
        if (inventoryPanel == null)
        {
            Debug.LogWarning("[HUD] InventoryPanel ist nicht zugewiesen.");
            return;
        }
        inventoryPanel.SetActive(!inventoryPanel.activeSelf);
    }
}