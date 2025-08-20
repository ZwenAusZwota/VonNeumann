using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScanPanelController : MonoBehaviour
{
    [Header("UI Targets")]
    [SerializeField] private Transform nearContainer;   // ScrollView/Viewport/Content
    [SerializeField] private Transform farContainer;
    [SerializeField] private GameObject listItemPrefabNear; //
    [SerializeField] private GameObject listItemPrefabFar; //
    [SerializeField] private TextMeshProUGUI txtNearScan;
    [SerializeField] private TextMeshProUGUI txtFarScan;

    [Header("HUD Layout")]
    public int X_START = 0;
    public int Y_START = 0;
    public int X_SPACE_BETWEEN_ITEMS = 55;
    public int Y_SPACE_BETWEEN_ITEMS = 55;
    public int NUMBER_OF_COLUMNS = 4;
}
