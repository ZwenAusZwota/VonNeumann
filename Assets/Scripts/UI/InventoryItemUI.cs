// Assets/Scripts/UI/InventoryItemUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Verbesserte UI-Komponente für Inventar-Items mit schönerem Layout
/// </summary>
public class InventoryItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Components")]
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI labelName;
    [SerializeField] private TextMeshProUGUI labelAmount;
    [SerializeField] private TextMeshProUGUI labelVolume;
    [SerializeField] private Image fillBar;
    
    [Header("Theme")]
    [SerializeField] private UITheme theme;
    [SerializeField] private bool useTheme = true;

    [Header("Colors (Fallback)")]
    public Color normalColor = new(0.08f, 0.12f, 0.20f, 0.90f);
    public Color hoverColor = new(0.11f, 0.18f, 0.29f, 0.95f);

    private Color NormalColor => useTheme && theme != null ? theme.backgroundNormal : normalColor;
    private Color HoverColor => useTheme && theme != null ? theme.backgroundHover : hoverColor;

    private void Awake()
    {
        if (useTheme && theme == null)
            theme = UITheme.Instance;
    }
    
    private string materialId;
    private float amount;
    private float volume;
    
    public void Init(string matId, float amt, float vol)
    {
        materialId = matId;
        amount = amt;
        volume = vol;
        
        // Material-Definition holen
        var matDef = MaterialDatabase.Get(materialId);
        if (matDef != null)
        {
            if (labelName)
            {
                labelName.text = matDef.displayName;
            }
            
            if (icon && matDef.icon != null)
            {
                icon.sprite = matDef.icon;
                icon.gameObject.SetActive(true);
            }
            else if (icon)
            {
                icon.gameObject.SetActive(false);
            }
        }
        else
        {
            if (labelName)
            {
                labelName.text = materialId;
            }
        }
        
        // Amount anzeigen
        if (labelAmount)
        {
            if (amount >= 1000000)
            {
                labelAmount.text = $"{amount / 1000000f:0.##}M";
            }
            else if (amount >= 1000)
            {
                labelAmount.text = $"{amount / 1000f:0.##}K";
            }
            else
            {
                labelAmount.text = $"{amount:0.#}";
            }
        }
        
        // Volumen anzeigen
        if (labelVolume)
        {
            if (volume >= 1000)
            {
                labelVolume.text = $"{volume / 1000f:0.##}K m³";
            }
            else
            {
                labelVolume.text = $"{volume:0.#} m³";
            }
        }
        
        if (background)
            background.color = NormalColor;

        if (labelName) labelName.color = useTheme && theme != null ? theme.textPrimary : Color.white;
        if (labelAmount) labelAmount.color = useTheme && theme != null ? theme.starColor : new Color(1f, 0.9f, 0.3f);
        if (labelVolume) labelVolume.color = useTheme && theme != null ? theme.textSecondary : new Color(0.6f, 0.6f, 0.6f);
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (background)
            background.color = HoverColor;
        
        // Leichte Vergrößerung
        transform.localScale = Vector3.one * 1.02f;
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (background)
            background.color = NormalColor;
        
        transform.localScale = Vector3.one;
    }
}


