using UnityEngine;

public class UpgradableProduct : MonoBehaviour
{
    public ProductBlueprint Blueprint { get; private set; }

    // Per Instanziierung gesetzt (z.B. via Factory oder Start())
    public void Init(ProductBlueprint bp) => Blueprint = bp;
}
