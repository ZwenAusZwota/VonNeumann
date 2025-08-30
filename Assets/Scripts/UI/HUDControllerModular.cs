// Assets/Scripts/UI/HUDControllerModular.cs
using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class HUDControllerModular : MonoBehaviour
{

    //[Header("HUD-Bereiche")]
    //public GameObject inventoryPanel;
    //public GameObject buildPanel;
    //public GameObject escapePanel;
    //public GameObject scanPanel;
    //public GameObject taskPanel;

    //[Header("Server Connection")]
    //public ServerConnector connector;

    ///*[Header("Game References")]*/
    //private ProbeController probe;
    
    //// Module references (automatically found)
    //private NavigationModule navigationModule;
    //private StatusModule statusModule;
    //private MiningModule miningModule;

    //// Player Input
    //private InputController inputController;
    //private InputAction _toggleInventory;

    //public Action evtQuitGame;

    //void Awake()
    //{
    //    inputController = new InputController();
    //}

    //private void OnEnable()
    //{
    //    inputController.HUD.Enable();
    //    inputController.HUD.ToggleInventory.performed += ctx => inventoryPanel.SetActive(!inventoryPanel.activeSelf);
    //    //inputController.HUD.ToggleFabricator.performed += ctx => buildPanel.SetActive(!buildPanel.activeSelf);
    //    inputController.HUD.Escape.performed += ctx => escapePanel.SetActive(!escapePanel.activeSelf);
    //    inputController.HUD.ToggleScanner.performed += ctx => scanPanel.SetActive(!scanPanel.activeSelf);
    //    //inputController.HUD.ToggleTasks.performed += ctx => taskPanel.SetActive(!taskPanel.activeSelf);
    //    inputController.HUD.ToggleTasks.performed += ctx => 
    //    {
    //        SceneRouter.Instance.OpenManagementAdditive();
    //    };

    //}

    //private void OnDestroy()
    //{
    //    inputController?.Dispose();
    //}

    //void Start()
    //{
    //    // Get module references
    //    navigationModule = UIModuleManager.Instance.GetModule<NavigationModule>();
    //    statusModule = UIModuleManager.Instance.GetModule<StatusModule>();
    //    miningModule = UIModuleManager.Instance.GetModule<MiningModule>();
        
    //    SetupEventConnections();

    //    inventoryPanel.gameObject.SetActive(false); // Hide inventory panel by default
    //    //buildPanel.gameObject.SetActive(false); // Hide build panel by default
    //    escapePanel.gameObject.SetActive(false);
    //    scanPanel.gameObject.SetActive(false);
    //    //taskPanel.gameObject.SetActive(false); // Hide task panel by default
    //}
    
    //void SetupEventConnections()
    //{
    //    // Connect server events
    //    if (connector != null)
    //    {
    //        connector.OnMineResult += HandleMineResult;
    //    }
        
    //    if (miningModule != null)
    //    {
    //        miningModule.OnMiningRequested += PerformMining;
    //    }

    //    // Set up probe reference
    //    if (probe != null && navigationModule != null)
    //    {
    //        navigationModule.SetProbe(probe);
    //    }


    //}
    
    //public void HandleInit(InitPayload payload)
    //{
    //    statusModule?.SetPlayer(payload.player);
    //}

    //public void OnExitClicked()
    //{
    //    evtQuitGame?.Invoke();
    //}

    ////public void SetProbe(Rigidbody rb)
    //public void SetProbe(ProbeController _probe)
    //{
    //    probe = _probe;
    //    navigationModule.SetProbe(probe);
    //    HUDBindingService.Select(probe != null ? probe.gameObject : null); // <— hinzufügen
    //}

    //public void UpdateProbePosition(Vector3 position, float speed)
    //{
    //    //navigationModule.UpdateProbePosition(position, speed);
    //}

    //public void SetSystemObjects(List<SystemObject> objects)
    //{
    //}
    
    //public void UpdateNearScan(List<SystemObject> objects)
    //{
    //    //objectSelectionModule?.UpdateNearbyObjects(objects);
    //    //scanningModule?.UpdateScanResults(objects);
    //}
    
    //void HandleObjectSelection(SystemObject selectedObject)
    //{
    //    navigationModule?.SetNavigationTarget(selectedObject);
    //}
    
    //void HandleMineResult(MineResult result)
    //{
    //    statusModule?.UpdateCredits(result.newCredit);
    //    miningModule?.HandleMiningResult(result);
    //}
    
    //public void PerformNearScan()
    //{
    //    //// Implement near scan logic
    //    //var scannController = probe?.GetComponent<ProbeScanner>();
    //    //if(scannController != null)
    //    //{
    //    //    scannController.PerformNearScan();
    //    //    scanningModule?.UpdateScanStatus("Scanning...");
    //    //}
        
    //    ////scanningModule?.StartScanCooldown(5f); // Example cooldown
    //}
    
    //public void PerformMining()
    //{
    //    // Implement mining logic
    //    var miningController = probe?.GetComponent<ProbeMiner>();
    //    if (miningController != null)
    //    {
    //        Debug.Log("Starting mining operation...");
    //        miningController.StartMining();
    //    }
    //}

    //public void UpdateMiningStatus(string status)
    //{
    //    miningModule?.UpdateMiningStatus(status);
    //}

    //public void UpdateCargoStatus(float used, float max)
    //{
    //    statusModule?.UpdateCargoStatus($"{used:N0}/{max:N0}");
    //}

}