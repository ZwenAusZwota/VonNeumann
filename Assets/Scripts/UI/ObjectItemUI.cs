// Assets/Scripts/UI/PlanetItemUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ObjectItemUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Refs")]
    public Image background;
    public TextMeshProUGUI label;

    [Header("Colors")]
    public Color normalColor = new(0.12f, 0.55f, 1f, 0.35f);
    public Color hoverColor = new(0.12f, 0.55f, 1f, 0.55f);
    public Color selectedColor = new(1f, 1f, 1f, 0.55f);

    public SystemObject sObject { get; private set; }
    public bool Selected { get; private set; }

    ObjectListManager owner;

    public void Init(ObjectListManager owner, SystemObject so)
    {
        this.owner = owner;
        sObject = so;
        label.text = so.DisplayName;
        if(so.GameObject.CompareTag("Asteroid"))
        {
            var material = so.GameObject.GetComponent<MineableAsteroid>().materialId;
            label.text = $"A - {material}";
        }
        
        background.color = normalColor;
    }

    public void SetSelected(bool sel)
    {
        Selected = sel;
        background.color = sel ? selectedColor : normalColor;
    }

    /* ---------- Events ---------- */
    public void OnPointerEnter(PointerEventData _) { if (!Selected) background.color = hoverColor; }
    public void OnPointerExit(PointerEventData _) { if (!Selected) background.color = normalColor; }
    public void OnPointerClick(PointerEventData _) { owner.SelectItem(this); }
}
