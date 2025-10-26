using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ProductBlueprint",
                 menuName = "SpaceGame/Fabrication/Product Blueprint")]
public class ProductBlueprint : ScriptableObject
{
    /* ---------- Basis-Infos ---------- */
    [Header("Basis-Infos")]
    public string productId;
    public string displayName;
    public Sprite icon;
    public GameObject prefab;
    public float buildTime = 15f;
    [TextAreaAttribute]
    public string description; 
    

    /* ---------- Hilfsstruktur ---------- */
    [System.Serializable]
    public class ResourceCost
    {
        public MaterialSO resource;
        public int amount;
    }
    [System.Serializable]
    public class ComponentCost
    {
        public ProductBlueprint product;
        public int amount;
    }

    /* ---------- Baukosten ---------- */
    [Header("Baukosten")]
    public List<ResourceCost> resourceCosts;
    public List<ComponentCost> componentCosts;

    /* ---------- Fabricator-Infos ---------- */
    public enum FabricatorType { Probe, Station, LargeFactory }
    [Header("Herstellort")]
    public List<FabricatorType> allowedFabricators;

    /* ---------- Upgrade-Infos ---------- */
    [Header("Upgrade-Infos (optional)")]
    public bool upgradeable;
    public List<ResourceCost> upgradeResourceCost = new();
    public List<ComponentCost> upgradeComponentCosts;
    public ProductBlueprint successor;
}
