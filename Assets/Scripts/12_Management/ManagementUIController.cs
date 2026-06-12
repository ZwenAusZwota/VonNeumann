using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Baut das Sonden-Management-UI zur Laufzeit auf (Zwei-Spalten-Layout).
/// </summary>
[DisallowMultipleComponent]
public class ManagementUIController : MonoBehaviour
{
    [SerializeField] private ManagementSceneController sceneController;

    private UITheme _theme;
    private TextMeshProUGUI _statusBody;
    private TextMeshProUGUI _boardSystemsBody;
    private TextMeshProUGUI _modulesBody;
    private TextMeshProUGUI _powerBody;
    private TextMeshProUGUI _researchBody;
    private TextMeshProUGUI _inventoryBody;
    private TextMeshProUGUI _fleetBody;
    private Slider _cargoSlider;
    private Slider _powerSlider;
    private TextMeshProUGUI _cargoLabel;
    private TextMeshProUGUI _powerLabel;

    private void Awake()
    {
        if (sceneController == null)
            sceneController = GetComponent<ManagementSceneController>();
        _theme = UITheme.Instance;
        BuildUi();
    }

    private void Update()
    {
        ScienceTreeService.I.Tick();
        RefreshContent();
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("ManagementCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var root = canvasGo.GetComponent<RectTransform>();
        OverlayUiKit.Stretch(root);

        var bg = OverlayUiKit.CreateImage(root, "Background", _theme?.panelBackground ?? new Color(0.04f, 0.07f, 0.13f, 0.96f));
        OverlayUiKit.Stretch(bg.rectTransform);

        var header = OverlayUiKit.CreatePanel(root, "Header", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -72f), Vector2.zero,
            _theme?.panelHeaderBackground ?? new Color(0.06f, 0.11f, 0.19f, 0.98f));
        OverlayUiKit.CreateLabel(header, "Sonden-Management", 26, FontStyles.Bold, TextAlignmentOptions.MidlineLeft,
            new Vector2(24f, 0f), new Vector2(-72f, 0f), _theme?.textPrimary ?? Color.white);
        OverlayUiKit.CreateCloseButton(header, _theme, () => sceneController?.CloseManagement());

        var body = OverlayUiKit.CreatePanel(root, "Body", Vector2.zero, Vector2.one, new Vector2(16f, 16f), new Vector2(-16f, -88f),
            _theme?.scrollPanelBackground ?? new Color(0.02f, 0.03f, 0.055f, 0.98f));

        // Linke Spalte: Sonde & Systeme
        var leftPanel = OverlayUiKit.CreatePanel(body, "LeftColumn", Vector2.zero, new Vector2(0.5f, 1f),
            new Vector2(0f, 0f), new Vector2(-8f, 0f),
            _theme?.backgroundNormal ?? new Color(0.03f, 0.05f, 0.09f, 0.5f));
        OverlayUiKit.CreateTopLabel(leftPanel, "Sonde & Systeme", 16, FontStyles.Bold, _theme?.textAccent ?? Color.cyan);
        var leftScroll = OverlayUiKit.CreateVerticalScroll(leftPanel, out var leftContent);
        var leftScrollRt = leftScroll;
        leftScrollRt.anchorMin = Vector2.zero;
        leftScrollRt.anchorMax = Vector2.one;
        leftScrollRt.offsetMin = new Vector2(0f, 8f);
        leftScrollRt.offsetMax = new Vector2(0f, -36f);

        BuildSection(leftContent, "Status", out _statusBody, out _cargoSlider, out _cargoLabel, true, 200f);
        BuildSection(leftContent, "Energiversorgung", out _powerBody, out _powerSlider, out _powerLabel, true, 200f);
        BuildSection(leftContent, "Bordsysteme", out _boardSystemsBody, out _, out _, false, 170f);
        BuildSection(leftContent, "Eingebaute Ausrüstung", out _modulesBody, out _, out _, false, 190f);

        // Rechte Spalte: Ressourcen & Flotte
        var rightPanel = OverlayUiKit.CreatePanel(body, "RightColumn", new Vector2(0.5f, 0f), Vector2.one,
            new Vector2(8f, 0f), new Vector2(0f, 0f),
            _theme?.backgroundNormal ?? new Color(0.03f, 0.05f, 0.09f, 0.5f));
        OverlayUiKit.CreateTopLabel(rightPanel, "Ressourcen & Flotte", 16, FontStyles.Bold, _theme?.textAccent ?? Color.cyan);
        var rightScroll = OverlayUiKit.CreateVerticalScroll(rightPanel, out var rightContent);
        var rightScrollRt = rightScroll;
        rightScrollRt.anchorMin = Vector2.zero;
        rightScrollRt.anchorMax = Vector2.one;
        rightScrollRt.offsetMin = new Vector2(0f, 8f);
        rightScrollRt.offsetMax = new Vector2(0f, -36f);

        BuildSection(rightContent, "Cargo / Inventar", out _inventoryBody, out _, out _, false, 240f);
        BuildSection(rightContent, "Geräte im Feld", out _fleetBody, out _, out _, false, 240f);
        BuildSection(rightContent, "Forschungsfortschritt", out _researchBody, out _, out _, false, 160f);

        var fabBtn = OverlayUiKit.CreateButton(rightContent, "Fabrikator öffnen (F11)", 240f, 44f, _theme);
        fabBtn.onClick.AddListener(() => sceneController?.OpenFabricator());

        RefreshContent();
    }

    private void BuildSection(RectTransform parent, string title, out TextMeshProUGUI body,
        out Slider bar, out TextMeshProUGUI barLabel, bool withBar, float height)
    {
        var sectionGo = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        sectionGo.transform.SetParent(parent, false);
        sectionGo.GetComponent<Image>().color = _theme?.backgroundNormal ?? new Color(0.03f, 0.05f, 0.09f, 0.97f);
        var le = sectionGo.GetComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
        le.flexibleWidth = 1f;

        OverlayUiKit.CreateTopLabel(sectionGo.transform, title, 18, FontStyles.Bold, _theme?.textAccent ?? Color.cyan);

        if (withBar)
        {
            barLabel = OverlayUiKit.CreateTopLabel(sectionGo.transform, "", 12, FontStyles.Normal, _theme?.textSecondary ?? Color.gray);
            var barLabelRt = barLabel.rectTransform;
            barLabelRt.anchorMin = new Vector2(0f, 1f);
            barLabelRt.anchorMax = new Vector2(1f, 1f);
            barLabelRt.pivot = new Vector2(0.5f, 1f);
            barLabelRt.sizeDelta = new Vector2(-32f, 20f);
            barLabelRt.anchoredPosition = new Vector2(0f, -72f);

            bar = OverlayUiKit.CreateSlider(sectionGo.transform, new Vector2(16f, -100f), new Vector2(-16f, -124f),
                _theme?.progressEmpty ?? new Color(0.08f, 0.1f, 0.16f, 1f),
                _theme?.progressFull ?? new Color(0.18f, 0.8f, 0.53f, 1f));
        }
        else
        {
            bar = null;
            barLabel = null;
        }

        var bodyGo = new GameObject("Body", typeof(RectTransform));
        bodyGo.transform.SetParent(sectionGo.transform, false);
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(16f, 12f);
        bodyRt.offsetMax = new Vector2(-16f, withBar ? -132f : -40f);

        body = bodyGo.AddComponent<TextMeshProUGUI>();
        body.fontSize = 14;
        body.alignment = TextAlignmentOptions.TopLeft;
        body.color = _theme?.textSecondary ?? Color.gray;
        body.textWrappingMode = TextWrappingModes.Normal;
        body.raycastTarget = false;
    }

    private void RefreshContent()
    {
        var data = ProbeManagementService.GetSnapshot();
        if (!data.HasProbe)
        {
            const string empty = "Keine Sonde in der Spielwelt gefunden.";
            if (_statusBody) _statusBody.text = empty;
            if (_boardSystemsBody) _boardSystemsBody.text = empty;
            if (_modulesBody) _modulesBody.text = empty;
            if (_powerBody) _powerBody.text = empty;
            if (_researchBody) _researchBody.text = empty;
            if (_inventoryBody) _inventoryBody.text = empty;
            if (_fleetBody) _fleetBody.text = empty;
            return;
        }

        if (_statusBody != null)
        {
            _statusBody.text =
                $"Sonde: {data.ProbeName} ({data.ProbeId})\n" +
                $"Geschwindigkeit: {data.Speed:0.0} u/s\n" +
                $"Nav-Ziel: {data.NavTarget}\n" +
                $"Autopilot: {(data.AutopilotActive ? "Aktiv" : "Aus")}\n" +
                $"Mining: {(data.IsMining ? $"Ja ({data.MiningMode})" : "Nein")}";
        }

        if (_cargoSlider != null && _cargoLabel != null && data.CargoMax > 0f)
        {
            _cargoSlider.value = data.CargoUsed / data.CargoMax;
            _cargoLabel.text = $"Cargo: {data.CargoUsed:0.#} / {data.CargoMax:0.#} m³";
        }

        if (_inventoryBody != null)
        {
            if (data.Inventory.Count == 0)
                _inventoryBody.text = $"Lager leer ({data.CargoUsed:0.#} / {data.CargoMax:0.#} m³ belegt).";
            else
            {
                var lines = $"Belegt: {data.CargoUsed:0.#} / {data.CargoMax:0.#} m³\n\n";
                foreach (var item in data.Inventory)
                {
                    string kind = item.isProduct ? "Produkt" : "Material";
                    lines += $"• {item.displayName}: {item.amount} ({kind})\n";
                }
                _inventoryBody.text = lines.TrimEnd();
            }
        }

        if (_boardSystemsBody != null)
        {
            if (data.Modules.Count == 0)
                _boardSystemsBody.text = "Keine Bordsysteme erkannt.";
            else
            {
                var lines = "";
                foreach (var module in data.Modules)
                    lines += $"• {module.Name}: {module.Detail}\n";
                _boardSystemsBody.text = lines.TrimEnd();
            }
        }

        if (_modulesBody != null)
        {
            if (data.InstalledEquipment.Count == 0)
                _modulesBody.text = "Noch keine Zusatzmodule eingebaut.\n(Fabrikator → Einbau-Equipment)";
            else
            {
                var lines = "";
                foreach (var module in data.InstalledEquipment)
                    lines += $"• {module.DisplayName}\n  {module.Description}\n";
                _modulesBody.text = lines.TrimEnd();
            }
        }

        if (_fleetBody != null)
        {
            if (data.FleetAssets.Count == 0)
                _fleetBody.text = "Keine autonomen Einheiten im Einsatz.\n(Fabrikator → Externe Einheiten)";
            else
            {
                var lines = "";
                foreach (var asset in data.FleetAssets)
                {
                    lines += $"• {asset.DisplayName}\n" +
                             $"  Status: {asset.Status} | Aufgabe: {asset.Task}\n" +
                             $"  Position: {asset.LastPosition.x:0}, {asset.LastPosition.y:0}, {asset.LastPosition.z:0}\n";
                    if (asset.Speed > 0f)
                        lines += $"  Geschwindigkeit: {asset.Speed:0.#} u/s\n";
                }
                _fleetBody.text = lines.TrimEnd();
            }
        }

        if (_powerBody != null)
        {
            _powerBody.text =
                $"Quelle: {data.Power.PrimarySource}\n" +
                $"Erzeugung: {data.Power.GenerationKw:0} kW\n" +
                $"Verbrauch: {data.Power.ConsumptionKw:0} kW\n" +
                $"Bilanz: {(data.Power.GenerationKw - data.Power.ConsumptionKw):+0;-0} kW";
        }

        if (_powerSlider != null && _powerLabel != null)
        {
            _powerSlider.value = data.Power.StoragePercent;
            _powerLabel.text = $"Energiespeicher: {data.Power.StoragePercent * 100f:0}%";
        }

        if (_researchBody != null)
        {
            var research = data.Research;
            _researchBody.text =
                $"Erforscht: {research.ResearchedCount} / {research.TotalCount}\n" +
                (research.HasActiveResearch
                    ? $"Aktuell: {research.ActiveTitle}\nVerbleibend: {ScienceTreeService.FormatDuration(research.RemainingSeconds)}"
                    : "Keine laufende Forschung.");
        }
    }
}
