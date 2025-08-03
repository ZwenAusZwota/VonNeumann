using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class ObjectListManager : MonoBehaviour, IPointerClickHandler
{

    [Header("Prefab")]
    //public GameObject itemPrefab;
    public ObjectItemUI itemPrefab;
    public RectTransform objectListRect;

    //readonly List<GameObject> items = new();
    readonly List<ObjectItemUI> items = new();

    ObjectItemUI selected;

    public event System.Action<SystemObject> OnSelected;

    public void Awake()
    {
        
    }

    public void Clear()
    {
        //Debug.Log("Clearing object list");
        foreach (var item in items)
        {
            Destroy(item.gameObject);
        }
        items.Clear();
        selected = null;
    }

    public void AddStar(SystemObject star)
    {
        //Debug.LogFormat("Adding star: {0}", star.name);
        var item = Instantiate(itemPrefab, objectListRect);
        item.Init(this, star);
        items.Add(item);
    }

    /* 🔸 Liste kommt bereits sortiert & benannt vom GameManager */
    public void AddObjects(List<SystemObject> objects)
    {
        foreach (SystemObject p in objects)
        {
            var item = Instantiate(itemPrefab, objectListRect);

            item.Init(this, p);
            items.Add(item);
        }
    }

    public void SelectItem(ObjectItemUI item)
    {
        if (selected == item) return;
        if (selected) selected.SetSelected(false);
        selected = item;
        selected.SetSelected(true);
        OnSelected?.Invoke(item.sObject);
    }

    public void OnPointerClick(PointerEventData _) => Deselect();

    public void Deselect()
    {
        if (selected)
        {
            selected.SetSelected(false);
            selected = null;
        }
    }
}
