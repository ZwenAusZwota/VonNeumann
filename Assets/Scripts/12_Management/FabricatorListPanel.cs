using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class FabricatorListPanel : MonoBehaviour
{
    [SerializeField] Transform listContainer;
    [SerializeField] FabricatorListItem itemPrefab;

    void OnEnable() { Refresh(); }
    public void Refresh()
    {
        foreach (Transform c in listContainer) Destroy(c.gameObject);
        // TODO: hole die Fabrikatoren aus deinem FabricatorManager / HubRegistry (Kind==Factory)
        List<HubRegistry.HubInfo> fabs = new();
        foreach (var h in HubRegistry.Instance.All())
            if (h.Kind == "Factory") fabs.Add(h);

        foreach (var f in fabs)
        {
            var item = Instantiate(itemPrefab, listContainer);
            item.Bind(f.DisplayName, f.Id);
        }
    }
}

public class FabricatorListItem : MonoBehaviour
{
    [SerializeField] TMP_Text lblName;
    [SerializeField] Button btnSelect;

    string _id;
    public void Bind(string name, string id)
    {
        _id = id;
        if (lblName) lblName.text = name;
        if (btnSelect)
        {
            btnSelect.onClick.RemoveAllListeners();
            btnSelect.onClick.AddListener(() => Debug.Log($"Factory selected: {_id}"));
        }
    }
}
