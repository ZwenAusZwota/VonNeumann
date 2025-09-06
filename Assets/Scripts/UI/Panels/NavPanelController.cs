// Assets/Scripts/UI/NavPanelController.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class NavPanelController : MonoBehaviour
{
    [Header("UI – Telemetrie")]
    [SerializeField] private TextMeshProUGUI txtTarget;
    [SerializeField] private TextMeshProUGUI txtDistance;
    [SerializeField] private TextMeshProUGUI txtSpeed;

    [Header("UI – Autopilot Button")]
    [SerializeField] private Button btnAutopilot;                 // Der Button im NavPanel
    [SerializeField] private Image btnAutopilotIndicator;         // Optional: eigenes Image; leer lassen = Button.targetGraphic
    [SerializeField] private TextMeshProUGUI btnAutopilotLabel;   // Optional: "Autopilot: AN/AUS"
    [SerializeField] private Color colorActive = new Color(0.17f, 0.73f, 0.35f, 1f); // grün
    [SerializeField] private Color colorInactive = new Color(0.50f, 0.50f, 0.50f, 1f); // grau

    const float AU_IN_KM = 149_597_870.7f;

    void OnEnable()
    {
        if (btnAutopilot != null) btnAutopilot.onClick.AddListener(OnClickAutopilot);

        // Live-Refresh bei Selektion/Änderungen
        if (HUDBindingService.I != null)
        {
            HUDBindingService.I.OnSelectionChanged += _ => Refresh();
            HUDBindingService.I.OnItemChanged += _ => Refresh();
            HUDBindingService.I.OnListReset += _ => Refresh();
        }
        Refresh();
        UpdateAutopilotVisual();
    }

    void OnDisable()
    {
        if (btnAutopilot != null) btnAutopilot.onClick.RemoveListener(OnClickAutopilot);

        if (HUDBindingService.I != null)
        {
            HUDBindingService.I.OnSelectionChanged -= _ => Refresh();
            HUDBindingService.I.OnItemChanged -= _ => Refresh();
            HUDBindingService.I.OnListReset -= _ => Refresh();
        }
    }

    void Update()
    {
        // laufende Aktualisierung während des Flugs + Button-Status
        RefreshLive();
        UpdateAutopilotVisual();
    }

    void Refresh()
    {
        var ap = GetSelectedAutopilot();
        if (ap == null || ap.NavTarget == null)
        {
            SetTexts("—", "—", "—");
            return;
        }
        RefreshLive(); // initiale Füllung
    }

    void RefreshLive()
    {
        var ap = GetSelectedAutopilot();
        if (ap == null || ap.NavTarget == null)
        {
            SetTexts("—", "—", "—");
            return;
        }

        string targetName = ap.NavTarget.name;

        // Distanz formatieren
        float distUnits = ap.CurrentDistanceUnits;
        string distStr = FormatDistance(distUnits);

        // Speed formatieren (einfach in km/s)
        string speedStr = FormatSpeed(ap.CurrentSpeedUnits);

        SetTexts(targetName, distStr, speedStr);
    }

    /* ---------------------- Autopilot Button ---------------------- */

    void OnClickAutopilot()
    {
        var ap = GetSelectedAutopilot();
        if (ap == null) return;

        if (ap.IsAutopilotActive) ap.StopAutopilot();   // AP aus
        else ap.StartAutopilot();                       // AP an

        UpdateAutopilotVisual();
    }

    void UpdateAutopilotVisual()
    {
        var ap = GetSelectedAutopilot();
        bool hasAp = ap != null;
        bool canToggle = hasAp && ap.NavTarget != null; // ohne Ziel nicht einschaltbar
        bool active = hasAp && ap.IsAutopilotActive;

        if (btnAutopilot != null) btnAutopilot.interactable = canToggle;

        // Wähle Image: explizit zugewiesenes, sonst das targetGraphic des Buttons
        Image img = btnAutopilotIndicator != null
            ? btnAutopilotIndicator
            : (btnAutopilot != null ? btnAutopilot.targetGraphic as Image : null);

        if (img != null) img.color = active ? colorActive : colorInactive;
        if (btnAutopilotLabel != null) btnAutopilotLabel.text = active ? "Autopilot: AN" : "Autopilot: AUS";
    }

    /* ---------------------- Helpers ---------------------- */

    ProbeAutopilot GetSelectedAutopilot()
    {
        var sel = HUDBindingService.I?.SelectedItem;
        var tr = sel?.Transform;
        if (!tr) return null;
        return tr.GetComponent<ProbeAutopilot>();
    }

    void SetTexts(string target, string distance, string speed)
    {
        if (txtTarget) txtTarget.text = $"{target}";
        if (txtDistance) txtDistance.text = $"{distance}";
        if (txtSpeed) txtSpeed.text = $"{speed}";
    }

    string FormatDistance(float units)
    {
        if (!float.IsFinite(units)) return "—";
        float km = units * Mathf.Max(PlanetScale.KM_PER_UNIT, 1e-6f);
        float au = km / AU_IN_KM;
        if (au >= 0.05f) return $"{au:0.###} AU";
        return $"{km:0,0} km";
    }

    string FormatSpeed(float unitsPerSec)
    {
        // Units/s → km/s
        float kmps = unitsPerSec * Mathf.Max(PlanetScale.KM_PER_UNIT, 1e-6f) / 3600; 
        return $"{kmps:0} km/h";
    }
}
