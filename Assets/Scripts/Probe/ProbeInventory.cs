using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>Einfaches Volumen-Inventar für die Sonde.</summary>
public class ProbeInventory : MonoBehaviour
{
    [Tooltip("Maximales Füllvolumen in m³")]
    public float maxVolume = 500f;

    [Header("HUD Settings")]
    public int X_START;
    public int Y_START;

    public int X_SPACE_BETWEEN_ITEMS = 55;
    public int Y_SPACE_BETWEEN_ITEMS = 55;
    public int NUMBER_OF_COLUMNS = 5;

    public GameObject itemPrefab; // Prefab für die Anzeige der Items

    private GameObject InventoryPanel; // Referenz zum UI-Panel, in dem die Items angezeigt werden
    InventoryObject inventory = new InventoryObject();
    Dictionary<InventorySlot, GameObject> itemsDisplayed = new Dictionary<InventorySlot, GameObject>();

    float usedVolume;
    readonly Dictionary<string, float> store = new();   // MaterialId → Einheiten

    public float FreeVolume => maxVolume - usedVolume;

    public event Action<float, float> CargoChanged;   // used, max

    /// <returns> Tatsächlich eingelagerten Einheiten </returns>
    public float Add(string materialId, float units)
    {
        var def = MaterialRegistry.Get(materialId);
        float vReq = units * def.volumePerUnit;

        if (vReq > FreeVolume)
            units = FreeVolume / def.volumePerUnit;     // nur Teil passt rein

        if (units <= 0f) return 0f;

        usedVolume += units * def.volumePerUnit;
        store[materialId] = store.TryGetValue(materialId, out var cur) ? cur + units : units;

        ItemObject item = new MaterialObject(materialId, def.id);
        inventory.AddItem(item, 1);

        //HUDController hud = FindObjectOfType<HUDController>();
        // if (hud) hud.SetCargoText(usedVolume, maxVolume);
        CargoChanged?.Invoke(usedVolume, maxVolume);
        return units;
    }

    void Awake()
    {
        // Initialisiere das InventoryPanel, falls es nicht im Inspector zugewiesen wurde
        if (InventoryPanel == null)
        {
            InventoryPanel = GameObject.Find("InventoryPanel");
        }
        if (itemPrefab == null)
        {
            Debug.LogError("ItemPrefab is not assigned in the ProbeInventory script.");
        }

        for (int i = 0; i < 3; i++)
        {
            ItemObject item = new MaterialObject("Ice","Eis");
            inventory.AddItem(item, 1);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        createDisplay();
    }

    // Update is called once per frame
    void Update()
    {
        UpdateDisplay();
    }

    public void createDisplay()
    {
        for (int i = 0; i < inventory.Container.Count; i++)
        {
            //var obj = Instantiate(inventory.Container[i].item.ProbeInventoryPrefab, Vector3.zero, Quaternion.identity, transform); //Prefab wird aus dem global Prefab dieser Klasse geladen
            var obj = createInventoryItem(i);
        }
    }

    public void UpdateDisplay()
    {
        for (int i = 0; i < inventory.Container.Count; i++)
        {
            InventorySlot slot = inventory.Container[i];
            if (itemsDisplayed.ContainsKey(slot))
            {
                itemsDisplayed[slot].GetComponentInChildren<TextMeshProUGUI>().text = slot.amount.ToString("n0");
      
            }
            else
            {
                //var obj = Instantiate(slot.item.prefab, Vector3.zero, Quaternion.identity, transform); //Prefab wird aus dem global Prefab dieser Klasse geladen
                //obj.GetComponent<RectTransform>().localPosition = getPosition(i);
                //obj.GetComponentInChildren<TextMeshProUGUI>().text = slot.amount.ToString("n0");
                var obj = createInventoryItem(i);
                itemsDisplayed.Add(slot, obj);
            }
        }
    }

    public Vector3 getPosition(int i)
    {
        return new Vector3(
                X_START + (X_SPACE_BETWEEN_ITEMS * (i % NUMBER_OF_COLUMNS)),
                Y_START + (-(Y_SPACE_BETWEEN_ITEMS * (i / NUMBER_OF_COLUMNS))),
                0f
            );
    }

    private GameObject createInventoryItem(int index)
    {
        var obj = Instantiate(itemPrefab, Vector3.zero, Quaternion.identity, InventoryPanel.transform);
        obj.GetComponent<RectTransform>().localPosition = getPosition(index);
        //obj.GetComponentInChildren<TextMeshProUGUI>().text = inventory.Container[index].amount.ToString("n0");
        var components = obj.GetComponentsInChildren<TextMeshProUGUI>();
        foreach(var component in components)
        {
            if (component.name == "txtAmount")
            {
                component.text = inventory.Container[index].amount.ToString("n0");
            }
            else if (component.name == "txtName")
            {
                component.text = inventory.Container[index].item.description;
            }
        }

        return obj;
    }
}

public class InventoryObject 
{
    public List<InventorySlot> Container = new List<InventorySlot>();
    public void AddItem(ItemObject item, int amount)
    {
        // Check if the item already exists in the inventory
        InventorySlot slot = Container.Find(s => ( s.item.type == item.type && s.item.materialId == item.materialId ));
        if (slot != null)
        {
            // If it exists, increase the amount
            slot.AddAmount(amount);
        }
        else
        {
            // If it doesn't exist, create a new slot and add it to the inventory
            Container.Add(new InventorySlot(item, amount));
        }
    }

}

[System.Serializable]
public class InventorySlot
{
    public ItemObject item;
    public int amount;
    public InventorySlot(ItemObject item, int amount)
    {
        this.item = item;
        this.amount = amount;
    }

    public void AddAmount(int amount)
    {
        this.amount += amount;
    }
}
