using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Volumenbasiertes Inventar mit HUD-Rendering, MaterialSO-Support und Fabrikator-APIs.
/// </summary>
public class InventoryController : MonoBehaviour
{
    [Header("Capacity")]
    [Tooltip("Maximales Füllvolumen in m³")]
    public float maxVolume = 500f;

    [SerializeField, Tooltip("Name des GameObjects, das als InventoryPanel dient")]
    private string panelName = "InventoryPanel";

    [Header("HUD Layout")]
    public int X_START = 0;
    public int Y_START = 0;
    public int X_SPACE_BETWEEN_ITEMS = 55;
    public int Y_SPACE_BETWEEN_ITEMS = 55;
    public int NUMBER_OF_COLUMNS = 4;

    [Header("Prefabs")]
    [SerializeField] private GameObject itemPrefab; // UI-Element (mit txtName, txtAmount)

    // ─────────────────────────────────────────────────────────────────────────────
    // Interne Felder
    // ─────────────────────────────────────────────────────────────────────────────
    private GameObject inventoryPanelGO; // wird per Lazy-Lookup ermittelt
    private float usedVolume;

    // Anzeige-Datenstruktur (dein bestehendes Modell)
    private readonly InventoryObject inventory = new InventoryObject();
    private readonly Dictionary<InventorySlot, GameObject> itemsDisplayed = new Dictionary<InventorySlot, GameObject>();

    // Lager:
    // Materialien nach MaterialSO → Einheiten
    private readonly Dictionary<MaterialSO, float> store = new();
    // Fertige Produkte nach productId → Stückzahl
    private readonly Dictionary<string, int> productStore = new();

    // Events
    /// <summary>Wird ausgelöst, wenn sich das Füllvolumen ändert. Parameter: usedVolume, maxVolume.</summary>
    public event Action<float, float> CargoChanged;

    // ─────────────────────────────────────────────────────────────────────────────
    // Eigenschaften
    // ─────────────────────────────────────────────────────────────────────────────
    public float UsedVolume => usedVolume;
    public float FreeVolume => Mathf.Max(0f, maxVolume - usedVolume);

    // ─────────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (itemPrefab == null)
            Debug.LogError("[Inventory] itemPrefab fehlt – bitte im Inspector zuweisen.");
    }

    private void Start()
    {
        CreateDisplay();
        CargoChanged?.Invoke(usedVolume, maxVolume);
    }

    private void Update()
    {
        UpdateDisplay();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Lazy-Lookup fürs Panel
    // ─────────────────────────────────────────────────────────────────────────────
    private GameObject GetInventoryPanelGO()
    {
        if (inventoryPanelGO == null)
        {
            inventoryPanelGO = GameObject.Find(panelName);
            //if (inventoryPanelGO == null)
            //{
            //    Debug.LogWarning($"[Inventory] Kein Panel mit Name '{panelName}' gefunden.");
            //}
        }
        return inventoryPanelGO;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Public API – Materialien (MaterialSO)
    // ─────────────────────────────────────────────────────────────────────────────

    public float GetMaterialUnits(MaterialSO material)
        => material != null && store.TryGetValue(material, out var v) ? v : 0f;

    public float GetMaterialUnits(string materialId)
        => GetMaterialUnits(MaterialDatabase.Get(materialId)); // Legacy-Komfort

    /// <summary>Lagert Material ein (volumenbasiert). Rückgabe: tatsächlich eingelagerte Einheiten.</summary>
    public float Add(MaterialSO material, float units)
    {
        if (material == null || units <= 0f) return 0f;

        float vReq = units * material.volumePerUnit;
        if (vReq > FreeVolume) units = FreeVolume / material.volumePerUnit;
        if (units <= 0f) return 0f;

        usedVolume += units * material.volumePerUnit;
        store[material] = store.TryGetValue(material, out var cur) ? cur + units : units;

        // HUD (bestehendes UI weiterverwenden)
        ItemObject item = new MaterialObject(material.id, material.displayName);
        inventory.AddItem(item, 1);

        CargoChanged?.Invoke(usedVolume, maxVolume);
        return units;
    }

    public float Add(string materialId, float units)
        => Add(MaterialDatabase.Get(materialId), units); // Legacy

    /// <summary>Entnimmt Material (so viel wie vorhanden). Rückgabe: tatsächlich entnommene Einheiten.</summary>
    public float Remove(MaterialSO material, float units)
    {
        if (material == null || !store.TryGetValue(material, out var have) || have <= 0f || units <= 0f)
            return 0f;

        float take = Mathf.Min(units, have);
        store[material] = have - take;

        usedVolume -= take * material.volumePerUnit;
        if (usedVolume < 0f) usedVolume = 0f;

        CargoChanged?.Invoke(usedVolume, maxVolume);
        return take;
    }

    public float Remove(string materialId, float units)
        => Remove(MaterialDatabase.Get(materialId), units); // Legacy

    // ─────────────────────────────────────────────────────────────────────────────
    // Public API – Produkte (für Fabrikation)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Gibt die vorhandene Stückzahl eines Produkts zurück.</summary>
    public int GetProductCount(string productId)
        => productStore.TryGetValue(productId, out var n) ? n : 0;

    /// <summary>Entnimmt fertige Produkte. assumedVolumePerUnit korrigiert das belegte Volumen (falls bekannt).</summary>
    public int RemoveProduct(string productId, int amount, float assumedVolumePerUnit = 0f)
    {
        if (amount <= 0) return 0;
        if (!productStore.TryGetValue(productId, out var have) || have <= 0) return 0;

        int take = Mathf.Min(have, amount);
        productStore[productId] = have - take;

        if (assumedVolumePerUnit > 0f)
        {
            usedVolume -= take * assumedVolumePerUnit;
            if (usedVolume < 0f) usedVolume = 0f;
            CargoChanged?.Invoke(usedVolume, maxVolume);
        }

        // HUD synchron halten: passenden Slot suchen und Menge reduzieren
        int leftToRemove = amount;
        for (int i = 0; i < inventory.Container.Count && leftToRemove > 0; i++)
        {
            var slot = inventory.Container[i];
            if (slot.item != null &&
                slot.item.type == ItemType.Product &&
                slot.item.materialId == productId)
            {
                int takeFromSlot = Mathf.Min(leftToRemove, slot.amount);
                slot.amount -= takeFromSlot;
                leftToRemove -= takeFromSlot;

                if (slot.amount <= 0)
                {
                    inventory.Container.RemoveAt(i);
                    i--;
                }
            }
        }
        // Optional: HUD sofort neu zeichnen
        UpdateDisplay();


        return take;
    }

    /// <summary>Versucht, ein fertiges Produkt einzulagern. Volumen = Summe der Input-Materialvolumina (oder override im Blueprint).</summary>
    public bool TryAddProduct(ProductBlueprint blueprint)
    {
        if (blueprint == null) return false;

        float vol = EstimateProductVolume(blueprint);
        if (vol > FreeVolume) return false;

        usedVolume += vol;
        productStore[blueprint.productId] = GetProductCount(blueprint.productId) + 1;

        CargoChanged?.Invoke(usedVolume, maxVolume);

        var productHUD = new ProductObject(blueprint.productId, blueprint.displayName);
        inventory.AddItem(productHUD, 1);
        // Optional: sofort HUD aktualisieren
        UpdateDisplay();

        return true;
    }

    /// <summary>Gibt die für ein Produkt verbrauchten Kosten (Materialien & Komponenten) zurück – z. B. beim Abbruch.</summary>
    public void RefundResources(ProductBlueprint blueprint)
    {
        if (blueprint == null) return;

        // 1) Materialien zurückbuchen
        foreach (var rc in blueprint.resourceCosts)
        {
            if (rc.resource == null || rc.amount <= 0) continue;
            Add(rc.resource, rc.amount);
        }

        // 2) Komponenten zurückbuchen (als fertige Produkte)
        foreach (var cc in blueprint.componentCosts)
        {
            if (cc.product == null || cc.amount <= 0) continue;

            float volPerUnit = EstimateProductVolume(cc.product);
            // Platzprüfung:
            if (volPerUnit * cc.amount <= FreeVolume)
            {
                usedVolume += volPerUnit * cc.amount;
                productStore[cc.product.productId] = GetProductCount(cc.product.productId) + cc.amount;
                CargoChanged?.Invoke(usedVolume, maxVolume);
            }
            else
            {
                Debug.LogWarning($"[Inventory] RefundResources: Nicht genug Volumen, um {cc.amount}x {cc.product.displayName} zurückzubuchen.");
                // Optional: pending refunds puffern.
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Fabrikator-Integrations-APIs (vom FabricatorController genutzt)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Prüft, ob genügend Rohstoffe und Komponenten für das Blueprint vorhanden sind.</summary>
    public bool HasResourcesFor(ProductBlueprint bp)
    {
        if (bp == null) return false;

        // Rohstoffe
        foreach (var rc in bp.resourceCosts)
        {
            if (rc.resource == null) return false;
            if (GetMaterialUnits(rc.resource) < rc.amount) return false;
        }

        // Komponenten
        foreach (var cc in bp.componentCosts)
        {
            if (cc.product == null) return false;
            if (GetProductCount(cc.product.productId) < cc.amount) return false;
        }
        return true;
    }

    /// <summary>Zieht die für die Produktion nötigen Rohstoffe/Komponenten ab. Gibt true bei Erfolg.</summary>
    public bool ConsumeResources(ProductBlueprint bp)
    {
        if (!HasResourcesFor(bp)) return false;

        // Rohstoffe entnehmen
        foreach (var rc in bp.resourceCosts)
        {
            var removed = Remove(rc.resource, rc.amount);
            if (removed < rc.amount)
            {
                Debug.LogWarning($"[Inventory] ConsumeResources: {rc.amount}x {rc.resource?.displayName} erwartet, nur {removed} entfernt.");
                return false;
            }
        }

        // Komponenten entnehmen (Volumen der entfernten Komponenten korrigieren)
        foreach (var cc in bp.componentCosts)
        {
            float volPerUnit = EstimateProductVolume(cc.product);
            int taken = RemoveProduct(cc.product.productId, cc.amount, volPerUnit);
            if (taken < cc.amount)
            {
                Debug.LogWarning($"[Inventory] ConsumeResources: {cc.amount}x {cc.product.displayName} erwartet, nur {taken} entfernt.");
                return false;
            }
        }
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Volumen-Schätzer für Produkte
    // ─────────────────────────────────────────────────────────────────────────────
    private float EstimateProductVolume(ProductBlueprint bp)
    {
        if (bp == null || bp.resourceCosts == null) return 0f;
        //if (bp.overrideProductVolume) return Mathf.Max(0f, bp.productVolume);

        float vol = 0f;
        foreach (var rc in bp.resourceCosts)
        {
            if (rc.resource == null) continue;
            vol += rc.amount * rc.resource.volumePerUnit;
        }
        return vol;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // HUD-Aufbau & Aktualisierung
    // ─────────────────────────────────────────────────────────────────────────────
    public void CreateDisplay()
    {
        //var panel = GetInventoryPanelGO();
       // if (panel == null || itemPrefab == null) return;

        // Neuaufbau: Vorhandenes aufräumen
        foreach (var kv in itemsDisplayed)
        {
            if (kv.Value != null) Destroy(kv.Value);
        }
        itemsDisplayed.Clear();

        for (int i = 0; i < inventory.Container.Count; i++)
        {
            var obj = CreateInventoryItem(i);
            if (obj != null)
            {
                var slot = inventory.Container[i];
                if (!itemsDisplayed.ContainsKey(slot))
                    itemsDisplayed.Add(slot, obj);
            }
        }
    }

    public void UpdateDisplay()
    {
        //var panel = GetInventoryPanelGO();
        //if (panel == null || itemPrefab == null) return;

        // 1) Bestehende Slots updaten oder neu anlegen
        for (int i = 0; i < inventory.Container.Count; i++)
        {
            var slot = inventory.Container[i];
            if (itemsDisplayed.TryGetValue(slot, out var obj) && obj != null)
            {
                var tmp = obj.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = slot.amount.ToString("n0");
            }
            else
            {
                var created = CreateInventoryItem(i);
                if (created != null) itemsDisplayed[slot] = created;
            }
        }

        // 2) UI-Objekte entfernen, zu denen es keinen Slot mehr gibt
        var toRemove = new List<InventorySlot>();
        foreach (var kv in itemsDisplayed)
        {
            if (!inventory.Container.Contains(kv.Key))
            {
                if (kv.Value != null) Destroy(kv.Value);
                toRemove.Add(kv.Key);
            }
        }
        foreach (var key in toRemove) itemsDisplayed.Remove(key);
    }

    private GameObject CreateInventoryItem(int index)
    {
        Debug.Log($"[Inventory] CreateInventoryItem: Index {index} von {inventory.Container[index].item.description}");
        var panel = GetInventoryPanelGO();
        if (panel == null || itemPrefab == null) return null;

        var obj = Instantiate(itemPrefab, Vector3.zero, Quaternion.identity, panel.transform);
        var rect = obj.GetComponent<RectTransform>();
        if (rect != null) rect.localPosition = GetPosition(index);

        var components = obj.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var component in components)
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

    private Vector3 GetPosition(int i)
    {
        return new Vector3(
            X_START + (X_SPACE_BETWEEN_ITEMS * (i % NUMBER_OF_COLUMNS)),
            Y_START + (-(Y_SPACE_BETWEEN_ITEMS * (i / NUMBER_OF_COLUMNS))),
            0f
        );
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Bestehende Datenstrukturen (beibehalten, damit dein übriger Code weiterläuft)
// Hinweis: ItemObject / MaterialObject werden außerhalb definiert und hier NICHT neu deklariert.
// ─────────────────────────────────────────────────────────────────────────────

[Serializable]
public class InventoryObject
{
    public List<InventorySlot> Container = new List<InventorySlot>();

    public void AddItem(ItemObject item, int amount)
    {
        // Merge-Logik: gleicher Typ & gleiches Material → Menge erhöhen
        InventorySlot slot = Container.Find(s => (s.item.type == item.type && s.item.materialId == item.materialId));
        if (slot != null)
        {
            slot.AddAmount(amount);
        }
        else
        {
            Container.Add(new InventorySlot(item, amount));
        }
    }
}

[Serializable]
public class InventorySlot
{
    public ItemObject item;
    public int amount;

    public InventorySlot(ItemObject item, int amount)
    {
        this.item = item;
        this.amount = amount;
    }

    public void AddAmount(int add)
    {
        amount += add;
    }
}
