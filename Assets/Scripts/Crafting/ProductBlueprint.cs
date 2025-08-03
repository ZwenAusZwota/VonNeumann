using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ProductBlueprint",
                 menuName = "SpaceGame/Product Blueprint")]
public class ProductBlueprint : ScriptableObject
{
    /* ---------- Basis-Infos ---------- */
    [Header("Basis-Infos")]
    public string id;
    public string displayName;
    public GameObject prefab;
    public float buildTime = 15f;

    /* ---------- Hilfsstruktur ---------- */
    [Serializable]
    public struct Cost
    {
        public string res;
        public int amt;
    }

    /* ---------- Baukosten ---------- */
    [Header("Baukosten")]
    public List<Cost> buildCost = new();

    /* ---------- Upgrade-Infos ---------- */
    [Header("Upgrade-Infos (optional)")]
    public bool upgradeable;
    public List<Cost> upgradeCost = new();
    public ProductBlueprint successor;
}
