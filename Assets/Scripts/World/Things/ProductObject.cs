using UnityEngine;

/* Basisklasse existiert bei dir bereits (ItemObject) – wir erweitern sie NICHT neu,
   sondern leiten ein Produkttyp-Objekt davon ab. */
public class ProductObject : ItemObject
{
    /// <param name="productId">Blueprint-ID (z. B. "MiniProbe_Mk1")</param>
    /// <param name="displayName">HUD-Anzeige</param>
    public ProductObject(string productId, string displayName)
    {
        this.type = ItemType.Product;
        this.materialId = productId; // für die bestehende Vergleichslogik: "materialId" fungiert hier als generische ID
        this.description = displayName;
    }
}
