// Assets/Scripts/UI/Modules/ObjectSelectionModule.cs
using UnityEngine;
using System;
using System.Collections.Generic;

public class ObjectSelectionModule : UIModule
{
    [Header("Object Lists")]
    public ObjectListManager systemObjectsList;
    public ObjectListManager nearbyObjectsList;
    
    public SystemObject CurrentSelection { get; private set; }
    
    public event Action<SystemObject> OnObjectSelected;
    
    protected override void OnInitialize()
    {
        if (systemObjectsList != null)
            systemObjectsList.OnSelected += HandleObjectSelected;
        
        if (nearbyObjectsList != null)
            nearbyObjectsList.OnSelected += HandleObjectSelected;
    }
    
    protected override void OnDisable()
    {
        if (systemObjectsList != null)
            systemObjectsList.OnSelected -= HandleObjectSelected;
        
        if (nearbyObjectsList != null)
            nearbyObjectsList.OnSelected -= HandleObjectSelected;
    }
    
    public void UpdateSystemObjects(List<SystemObject> objects)
    {
        if (systemObjectsList != null)
        {
            systemObjectsList.Clear();
            systemObjectsList.AddObjects(objects);
        }
    }
    
    public void UpdateNearbyObjects(List<SystemObject> objects)
    {
        if (nearbyObjectsList != null)
        {
            nearbyObjectsList.Clear();
            nearbyObjectsList.AddObjects(objects);
        }
    }
    
    void HandleObjectSelected(SystemObject selectedObject)
    {
        CurrentSelection = selectedObject;
        OnObjectSelected?.Invoke(selectedObject);
    }
    
    public void ClearSelection()
    {
        CurrentSelection = null;
        systemObjectsList?.Deselect();
        nearbyObjectsList?.Deselect();
    }
}