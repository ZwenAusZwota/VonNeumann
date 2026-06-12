// Assets/Scripts/UI/NavPanelController.cs

using UnityEngine;

using UnityEngine.UI;

using TMPro;

using SpaceGame.UI;



[DisallowMultipleComponent]

public class NavPanelController : MonoBehaviour

{

    [Header("UI – Telemetrie")]

    [SerializeField] private TextMeshProUGUI txtTarget;

    [SerializeField] private TextMeshProUGUI txtDistance;

    [SerializeField] private TextMeshProUGUI txtSpeed;



    [Header("UI – Autopilot")]

    [SerializeField] private Button btnAutopilot;

    [SerializeField] private Image btnAutopilotIndicator;

    [SerializeField] private TextMeshProUGUI btnAutopilotLabel;

    [SerializeField] private Color colorActive = new(0.17f, 0.73f, 0.35f, 1f);

    [SerializeField] private Color colorInactive = new(0.50f, 0.50f, 0.50f, 1f);



    [Header("Layout")]

    [SerializeField] private bool applyResponsiveLayout = true;

    [SerializeField] private RectTransform headerRow;

    [SerializeField] private RectTransform rowTarget;

    [SerializeField] private RectTransform rowDistance;

    [SerializeField] private RectTransform rowSpeed;

    [SerializeField] private RectTransform autopilotRow;

    [SerializeField] private float headerHeight = 50f;

    [SerializeField] private float telemetryRowHeight = 34f;

    [SerializeField] private float telemetryValueFontSize = 14f;

    [SerializeField] private float autopilotRowHeight = 40f;

    [SerializeField] private float outerPadding = 10f;

    [SerializeField] private float rowSpacing = 6f;



    private const float AU_IN_KM = 149_597_870.7f;

    private const string HeaderName = "Header";

    private const string CloseButtonName = "btnClose";



    private bool _chromeReady;



    private void Awake()

    {

        AutoWireReferences();

        EnsurePanelChrome();

        ApplyResponsiveLayout();

    }



    private void OnRectTransformDimensionsChange()

    {

        ApplyResponsiveLayout();

    }



    private void OnEnable()

    {

        var theme = UITheme.Instance;

        if (theme != null)

        {

            colorActive = theme.successColor;

            colorInactive = theme.textSecondary;

        }



        if (btnAutopilot != null)

            btnAutopilot.onClick.AddListener(OnClickAutopilot);



        if (HUDBindingService.I != null)

        {

            HUDBindingService.I.OnSelectionChanged += HandleHudDataChanged;

            HUDBindingService.I.OnItemChanged += HandleHudDataChanged;

            HUDBindingService.I.OnListReset += HandleHudListReset;

        }



        Refresh();

        UpdateAutopilotVisual();

    }



    private void OnDisable()

    {

        if (btnAutopilot != null)

            btnAutopilot.onClick.RemoveListener(OnClickAutopilot);



        if (HUDBindingService.I != null)

        {

            HUDBindingService.I.OnSelectionChanged -= HandleHudDataChanged;

            HUDBindingService.I.OnItemChanged -= HandleHudDataChanged;

            HUDBindingService.I.OnListReset -= HandleHudListReset;

        }

    }



    private void Update()

    {

        RefreshLive();

        UpdateAutopilotVisual();

    }



    private void HandleHudDataChanged(HUDItem _) => Refresh();



    private void HandleHudListReset(System.Collections.Generic.IReadOnlyList<HUDItem> _) => Refresh();



    private void AutoWireReferences()

    {

        if (txtTarget == null)

            txtTarget = transform.Find("txtDestination")?.GetComponent<TextMeshProUGUI>()

                        ?? transform.Find("txtTarget")?.GetComponent<TextMeshProUGUI>();



        if (txtDistance == null)

            txtDistance = transform.Find("txtDistance")?.GetComponent<TextMeshProUGUI>();



        if (txtSpeed == null)

            txtSpeed = transform.Find("txtSpeed")?.GetComponent<TextMeshProUGUI>();



        if (btnAutopilot == null)

            btnAutopilot = transform.Find("btnAutopilot")?.GetComponent<Button>();



        if (btnAutopilotLabel == null && btnAutopilot != null)

            btnAutopilotLabel = btnAutopilot.GetComponentInChildren<TextMeshProUGUI>(true);



        if (btnAutopilotIndicator == null && btnAutopilot != null)

            btnAutopilotIndicator = btnAutopilot.targetGraphic as Image;



        foreach (Transform child in transform)

        {

            if (rowTarget == null && child.Find("txtDestination") != null)

                rowTarget = child as RectTransform;

            if (rowDistance == null && child.Find("txtDistance") != null)

                rowDistance = child as RectTransform;

            if (rowSpeed == null && child.Find("txtSpeed") != null)

                rowSpeed = child as RectTransform;

        }



        if (autopilotRow == null && btnAutopilot != null)

            autopilotRow = btnAutopilot.transform as RectTransform;

    }



    private void EnsurePanelChrome()

    {

        if (_chromeReady) return;



        var rootVlg = GetComponent<VerticalLayoutGroup>();

        if (rootVlg != null)

            rootVlg.enabled = false;



        var legacyBg = GetComponent<Image>();

        if (legacyBg != null && legacyBg.enabled)

            legacyBg.enabled = false;



        var draggable = GetComponent<DraggableHudPanel>();

        if (draggable == null)

        {

            draggable = gameObject.AddComponent<DraggableHudPanel>();

            draggable.panelId = "NavPanel";

            draggable.minSize = new Vector2(280f, 240f);

        }



        if (GetComponent<HUDPanelStateAdapter>() == null)

            gameObject.AddComponent<HUDPanelStateAdapter>();



        draggable.ApplyInitialLayoutFromSave();

        HudPanelThemeApplier.ApplyTo(transform);



        var theme = UITheme.Instance;

        if (theme != null)

            EnsureHeader(theme, draggable);



        _chromeReady = true;

    }



    private void EnsureHeader(UITheme theme, DraggableHudPanel draggable)

    {

        headerRow = transform.Find(HeaderName) as RectTransform;

        if (headerRow == null)

        {

            var headerGo = new GameObject(HeaderName, typeof(RectTransform), typeof(Image));

            headerRow = headerGo.GetComponent<RectTransform>();

            headerRow.SetParent(transform, false);

            headerRow.SetAsFirstSibling();



            var headerImg = headerGo.GetComponent<Image>();

            headerImg.color = theme.panelHeaderBackground;

            headerImg.raycastTarget = false;

        }



        var title = transform.Find("txtNav") as RectTransform;

        if (title != null && title.parent != headerRow)

        {

            title.SetParent(headerRow, false);

            title.anchorMin = new Vector2(0f, 0f);

            title.anchorMax = new Vector2(1f, 1f);

            title.offsetMin = new Vector2(12f, 0f);

            title.offsetMax = new Vector2(-40f, 0f);

            title.pivot = new Vector2(0.5f, 0.5f);



            var titleTmp = title.GetComponent<TextMeshProUGUI>();

            if (titleTmp != null)

            {

                titleTmp.text = "Navigation";

                titleTmp.alignment = TextAlignmentOptions.MidlineLeft;

                titleTmp.fontStyle = FontStyles.Bold;

                titleTmp.color = theme.textAccent;

            }

        }



        var closeButton = headerRow.Find(CloseButtonName)?.GetComponent<Button>();

        if (closeButton == null)

        {

            var closeGo = new GameObject(CloseButtonName, typeof(RectTransform), typeof(Image), typeof(Button));

            closeGo.transform.SetParent(headerRow, false);



            var closeRect = closeGo.GetComponent<RectTransform>();

            closeRect.anchorMin = new Vector2(1f, 0.5f);

            closeRect.anchorMax = new Vector2(1f, 0.5f);

            closeRect.pivot = new Vector2(1f, 0.5f);

            closeRect.sizeDelta = new Vector2(28f, 28f);

            closeRect.anchoredPosition = new Vector2(-6f, 0f);



            var closeImg = closeGo.GetComponent<Image>();

            closeImg.color = theme.buttonNormal;



            var labelGo = new GameObject("Label", typeof(RectTransform));

            labelGo.transform.SetParent(closeGo.transform, false);

            var labelRect = labelGo.GetComponent<RectTransform>();

            labelRect.anchorMin = Vector2.zero;

            labelRect.anchorMax = Vector2.one;

            labelRect.offsetMin = Vector2.zero;

            labelRect.offsetMax = Vector2.zero;



            var label = labelGo.AddComponent<TextMeshProUGUI>();

            label.text = "X";

            label.alignment = TextAlignmentOptions.Center;

            label.fontSize = 16f;

            label.color = theme.textPrimary;

            label.raycastTarget = false;



            closeButton = closeGo.GetComponent<Button>();

            closeButton.targetGraphic = closeImg;

            var colors = closeButton.colors;

            colors.normalColor = theme.buttonNormal;

            colors.highlightedColor = theme.buttonHover;

            colors.pressedColor = theme.buttonPressed;

            closeButton.colors = colors;

            closeButton.onClick.AddListener(draggable.ClosePanel);



            DraggableHudPanel.ApplyStandardCloseButton(closeButton);

        }



        if (draggable.closeButton == null)

            draggable.closeButton = closeButton;

    }



    private void ApplyResponsiveLayout()

    {

        if (!applyResponsiveLayout) return;



        AutoWireReferences();



        if (headerRow != null)

        {

            SetStretch(

                headerRow,

                new Vector2(0f, 1f), new Vector2(1f, 1f),

                new Vector2(0f, -headerHeight), Vector2.zero);

        }



        float top = headerHeight + outerPadding;

        float rowBlock = telemetryRowHeight + rowSpacing;



        StyleTelemetryRow(rowTarget, top);

        StyleTelemetryRow(rowDistance, top + rowBlock);

        StyleTelemetryRow(rowSpeed, top + rowBlock * 2f);



        if (autopilotRow != null)

        {

            SetStretch(

                autopilotRow,

                new Vector2(0f, 0f), new Vector2(1f, 0f),

                new Vector2(outerPadding, outerPadding),

                new Vector2(-outerPadding, outerPadding + autopilotRowHeight));



            if (btnAutopilot != null)

            {

                var colors = btnAutopilot.colors;

                var theme = UITheme.Instance;

                if (theme != null)

                {

                    colors.normalColor = theme.buttonNormal;

                    colors.highlightedColor = theme.buttonHover;

                    colors.pressedColor = theme.buttonPressed;

                    colors.disabledColor = theme.backgroundDisabled;

                    btnAutopilot.colors = colors;

                }

            }

        }

    }



    private void StyleTelemetryRow(RectTransform row, float topOffset)

    {

        if (row == null) return;



        SetStretch(

            row,

            new Vector2(0f, 1f), new Vector2(1f, 1f),

            new Vector2(outerPadding, -(topOffset + telemetryRowHeight)),

            new Vector2(-outerPadding, -topOffset));



        var theme = UITheme.Instance;

        if (theme != null)

        {

            var bg = row.GetComponent<Image>() ?? row.gameObject.AddComponent<Image>();

            bg.color = theme.scrollPanelBackground;

            bg.raycastTarget = false;

        }



        var layout = row.GetComponent<HorizontalLayoutGroup>() ?? row.gameObject.AddComponent<HorizontalLayoutGroup>();

        layout.padding = new RectOffset(10, 10, 4, 4);

        layout.spacing = 8f;

        layout.childAlignment = TextAnchor.MiddleLeft;

        layout.childControlWidth = true;

        layout.childControlHeight = true;

        layout.childForceExpandWidth = false;

        layout.childForceExpandHeight = true;



        foreach (Transform child in row)

        {

            var isLabel = child.name.StartsWith("lbl");

            var le = child.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>();



            if (isLabel)

            {

                le.minWidth = 100f;

                le.preferredWidth = 110f;

                le.flexibleWidth = 0f;



                var tmp = child.GetComponent<TMP_Text>();

                if (tmp != null)

                {

                    tmp.fontStyle = FontStyles.Normal;

                    if (theme != null) tmp.color = theme.textSecondary;

                    tmp.alignment = TextAlignmentOptions.MidlineLeft;

                }



                var legacy = child.GetComponent<Text>();

                if (legacy != null && theme != null)

                    legacy.color = theme.textSecondary;

            }

            else

            {

                le.flexibleWidth = 1f;



                var tmp = child.GetComponent<TMP_Text>();

                if (tmp != null)

                {

                    tmp.fontStyle = FontStyles.Normal;

                    tmp.fontSize = telemetryValueFontSize;

                    if (theme != null) tmp.color = theme.textPrimary;

                    tmp.alignment = TextAlignmentOptions.MidlineRight;

                }

            }

        }

    }



    private static void SetStretch(

        RectTransform rect,

        Vector2 anchorMin,

        Vector2 anchorMax,

        Vector2 offsetMin,

        Vector2 offsetMax)

    {

        if (rect == null) return;



        rect.anchorMin = anchorMin;

        rect.anchorMax = anchorMax;

        rect.offsetMin = offsetMin;

        rect.offsetMax = offsetMax;

    }



    private void Refresh()

    {

        var ap = GetSelectedAutopilot();

        if (ap == null || ap.NavTarget == null)

        {

            SetTexts("—", "—", "—");

            return;

        }



        RefreshLive();

    }



    private void RefreshLive()

    {

        var ap = GetSelectedAutopilot();

        if (ap == null || ap.NavTarget == null)

        {

            SetTexts("—", "—", "—");

            return;

        }



        var probe = HUDBindingService.I?.SelectedItem?.Transform;

        SetTexts(
            ap.NavTarget.name,
            FormatDistance(ap.CurrentDistanceUnits),
            FormatSpeed(ProbeTelemetry.GetSpeedUnitsPerSecond(probe)));

    }



    private void OnClickAutopilot()

    {

        var ap = GetSelectedAutopilot();

        if (ap == null) return;



        if (ap.IsAutopilotActive) ap.StopAutopilot();

        else ap.StartAutopilot();



        UpdateAutopilotVisual();

    }



    private void UpdateAutopilotVisual()

    {

        var ap = GetSelectedAutopilot();

        bool hasAp = ap != null;

        bool canToggle = hasAp && ap.NavTarget != null;

        bool active = hasAp && ap.IsAutopilotActive;



        if (btnAutopilot != null)

            btnAutopilot.interactable = canToggle;



        var img = btnAutopilotIndicator != null

            ? btnAutopilotIndicator

            : btnAutopilot != null ? btnAutopilot.targetGraphic as Image : null;



        if (img != null)

            img.color = active ? colorActive : colorInactive;



        if (btnAutopilotLabel != null)

            btnAutopilotLabel.text = active ? "Autopilot: AN" : "Autopilot: AUS";

    }



    private ProbeAutopilot GetSelectedAutopilot()

    {

        var sel = HUDBindingService.I?.SelectedItem;

        var tr = sel?.Transform;

        return tr != null ? tr.GetComponent<ProbeAutopilot>() : null;

    }



    private void SetTexts(string target, string distance, string speed)

    {

        if (txtTarget) txtTarget.text = target;

        if (txtDistance) txtDistance.text = distance;

        if (txtSpeed) txtSpeed.text = speed;

    }



    private string FormatDistance(float units)

    {

        if (!float.IsFinite(units)) return "—";

        float km = units * Mathf.Max(PlanetScale.KM_PER_UNIT, 1e-6f);

        float au = km / AU_IN_KM;

        if (au >= 0.05f) return $"{au:0.###} AU";

        return $"{km:0,0} km";

    }



    private string FormatSpeed(float unitsPerSec)

    {

        if (!float.IsFinite(unitsPerSec)) return "—";

        float kmPerSec = PlanetScale.UnitsPerSecToKmPerSec(unitsPerSec);

        if (kmPerSec >= 1000f) return $"{kmPerSec / 1000f:0.#} Mm/s";

        if (kmPerSec >= 100f) return $"{kmPerSec:0} km/s";

        return $"{kmPerSec:0.#} km/s";

    }

}


