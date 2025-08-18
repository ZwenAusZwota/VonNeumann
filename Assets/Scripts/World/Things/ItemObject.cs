using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum ItemType
{
    Material,
    Product
}


public abstract class ItemObject
{
    //public GameObject InventoryItemPrefab;
    public ItemType type;
    public string materialId;

    //[TextArea(15, 20)]
    public string description;

}
