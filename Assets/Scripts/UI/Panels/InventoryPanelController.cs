// Assets/Scripts/UI/InventoryPanelController.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryPanelController : MonoBehaviour
{
    [Header("UI Targets")]
    [SerializeField] private Transform listContainer;   // ScrollView/Viewport/Content
    [SerializeField] private GameObject listItemPrefab; // enthält TextMeshPro: "txtName", "txtAmount" und optional Image
    [SerializeField] private TextMeshProUGUI txtCapacity;

    [Header("HUD Layout")]
    public int X_START = 0;
    public int Y_START = 0;
    public int X_SPACE_BETWEEN_ITEMS = 55;
    public int Y_SPACE_BETWEEN_ITEMS = 55;
    public int NUMBER_OF_COLUMNS = 4;

    private InventoryController bound;

    private void OnEnable()
    {
        HUDBindingService.ActiveInventoryChanged += OnActiveInventoryChanged;
        HUDBindingService.Reannounce();
    }

    private void OnDisable()
    {
        HUDBindingService.ActiveInventoryChanged -= OnActiveInventoryChanged;
        Unbind();
    }

    private void OnActiveInventoryChanged(InventoryController inv)
    {
        if (inv == bound) return;
        Unbind();
        Bind(inv);
    }

    private void Bind(InventoryController inv)
    {
        bound = inv;
        if (bound == null)
        {
            RebuildList(null);
            SetCapacity(0, 0);
            return;
        }

        bound.InventoryUpdated += RebuildList;
        bound.CargoChanged += SetCapacity;
        bound.ForceRefreshUI();
    }

    private void Unbind()
    {
        if (bound == null) return;
        bound.InventoryUpdated -= RebuildList;
        bound.CargoChanged -= SetCapacity;
        bound = null;
    }

    private void RebuildList(IReadOnlyList<InventoryItemView> items)
    {
        // clear
        for (int i = listContainer.childCount - 1; i >= 0; i--)
            Destroy(listContainer.GetChild(i).gameObject);

        if (items == null) return;

        // build
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
           
            var go = Instantiate(listItemPrefab, listContainer);

            var rect = go.GetComponent<RectTransform>();
            if (rect != null) rect.localPosition = GetPosition(i);

            var texts = go.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                if (t.name == "txtName") t.text = it.displayName;
                else if (t.name == "txtAmount") t.text = it.amount.ToString("n0");
            }

            var img = go.GetComponentInChildren<Image>(true);
            if (img != null && it.icon != null) img.sprite = it.icon;
        }
    }

    private void SetCapacity(float used, float max)
    {
        if (txtCapacity != null) txtCapacity.text = $"Capacity: {used:N0} / {max:N0}";
    }

    private Vector3 GetPosition(int i)
    {
        return new Vector3(
            X_START + (X_SPACE_BETWEEN_ITEMS * (i % NUMBER_OF_COLUMNS)),
            Y_START + (-(Y_SPACE_BETWEEN_ITEMS * (i / NUMBER_OF_COLUMNS))),
            0f
        );
    }
}
