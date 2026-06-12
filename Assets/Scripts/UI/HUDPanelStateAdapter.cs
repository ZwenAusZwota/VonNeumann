// Assets/Scripts/10_Gameplay/UI/HUD/HUDPanelStateAdapter.cs
using UnityEngine;

[DisallowMultipleComponent]
public class HUDPanelStateAdapter : MonoBehaviour, IHUDPanelSavable
{
    [SerializeField] private string panelId = "NavPanel";   // <- je Panel individuell setzen
    [SerializeField] private GameObject root;               // optional: eigener Root f�r Sichtbarkeit

    public string PanelId => panelId;

    private void Awake()
    {
        root ??= gameObject;
        if (string.IsNullOrWhiteSpace(panelId))
            panelId = gameObject.name;
    }

    private void Reset()
    {
        root ??= gameObject;
        if (string.IsNullOrWhiteSpace(panelId))
            panelId = gameObject.name;
    }

    public bool IsVisible()
    {
        return (root != null ? root.activeSelf : gameObject.activeSelf);
    }

    public void SetVisible(bool visible)
    {
        if (root != null) root.SetActive(visible);
        else gameObject.SetActive(visible);
    }
}
