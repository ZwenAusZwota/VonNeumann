using UnityEngine;


public class MaterialObject : ItemObject
{


    public MaterialObject(string materialId, string description) {
        this.type = ItemType.Material;
        this.materialId = materialId;
        this.description = description;
        // Load the material from resources or set a default one
        // material = Resources.Load<Material>($"Materials/{materialId}");
    }



}
