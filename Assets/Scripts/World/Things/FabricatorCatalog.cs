using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FabricatorCatalog", menuName = "SpaceGame/Fabrication/Fabricator Catalog")]
public class FabricatorCatalog : ScriptableObject
{
    public List<ProductBlueprint> allProducts = new();

    // Optional: gefilterte Sicht nach Fabrikator-Typ
    public List<ProductBlueprint> GetFor(ProductBlueprint.FabricatorType type)
    {
        var list = new List<ProductBlueprint>();
        foreach (var p in allProducts)
            if (p != null && p.allowedFabricators.Contains(type)) list.Add(p);
        return list;
    }
}
