// Assets/Scripts/UI/ObjectItemUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ObjectItemUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Refs")]
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI label;

    [Header("Colors")]
    public Color normalColor = new(0.12f, 0.55f, 1f, 0.35f);
    public Color hoverColor = new(0.12f, 0.55f, 1f, 0.55f);
    public Color selectedColor = new(1f, 1f, 1f, 0.55f);

    public SystemObject SObject { get; private set; }
    public bool Selected { get; private set; }

    // Einfache „radio selection“ pro Liste:
    private static ObjectItemUI _currentlySelectedInListGroup;

    public void Init(SystemObject so)
    {
        SObject = so;

        if (label)
        {
            // Asteroiden hübsch beschriften (Material, falls vorhanden)
            if (so.GameObject && so.GameObject.CompareTag("Asteroid"))
            {
                var mat = so.GameObject.GetComponent<MineableAsteroid>()?.materialId;
                label.text = string.IsNullOrWhiteSpace(mat) ? (so.DisplayName ?? so.Name) : $"Asteroid – {mat}";
            }
            else
            {
                label.text = string.IsNullOrWhiteSpace(so.DisplayName) ? so.Name : so.DisplayName;
            }
        }

        if (background) background.color = normalColor;
        Selected = false;
    }

    public void SetSelected(bool sel)
    {
        Selected = sel;
        if (background) background.color = sel ? selectedColor : normalColor;
    }

    /* ---------- Pointer Events ---------- */
    public void OnPointerEnter(PointerEventData _) { if (!Selected && background) background.color = hoverColor; }
    public void OnPointerExit(PointerEventData _) { if (!Selected && background) background.color = normalColor; }

    public void OnPointerClick(PointerEventData _)
    {
        // „Radio“-Auswahl in dieser Liste
        if (_currentlySelectedInListGroup && _currentlySelectedInListGroup != this)
            _currentlySelectedInListGroup.SetSelected(false);

        _currentlySelectedInListGroup = this;
        SetSelected(true);

        // Neues Verhalten: Ziel direkt auf die aktuell im HUD selektierte Sonde setzen
        if (SObject != null && SObject.GameObject)
        {
            var ok = ProbeAutopilot.TrySetNavTargetOnSelectedProbe(SObject.GameObject.transform);
            if (!ok)
                Debug.LogWarning("[ObjectItemUI] Konnte Nav-Ziel nicht setzen (keine Sonde im HUD selektiert?).");
        }
    }
}
