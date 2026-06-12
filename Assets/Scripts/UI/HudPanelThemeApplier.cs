using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Wendet das zentrale UITheme auf HUD-Panels an (Hintergrund, Akzentlinie, Texte, Buttons).
/// </summary>
[DisallowMultipleComponent]
public class HudPanelThemeApplier : MonoBehaviour
{
    private const string AccentBarName = "_HudAccentTop";

    [SerializeField] private UITheme theme;
    [SerializeField] private bool applyOnAwake = false;
    [SerializeField] private float accentBarHeight = 2f;

    private enum TextStyle { Title, Section, Label, Value, Default }

    private void Awake()
    {
        if (applyOnAwake)
            Apply();
    }

    public void Apply()
    {
        theme ??= UITheme.Instance;
        if (theme == null) return;

        ApplyRootBackground();
        ApplyAccentBar();
        ApplyScrollAreas();
        ApplyButtons();
        ApplyTexts();
        ApplyCloseButtons();
    }

    private void ApplyCloseButtons()
    {
        foreach (var panel in GetComponentsInChildren<SpaceGame.UI.DraggableHudPanel>(true))
        {
            if (panel.closeButton != null)
                SpaceGame.UI.DraggableHudPanel.ApplyStandardCloseButton(panel.closeButton);
        }
    }

    public static void ApplyTo(Transform root)
    {
        if (root == null || !IsThemeablePanel(root))
            return;

        var applier = root.GetComponent<HudPanelThemeApplier>();
        if (applier == null)
            applier = root.gameObject.AddComponent<HudPanelThemeApplier>();

        applier.Apply();
    }

    public static void ApplyToAllUnder(Transform canvasRoot)
    {
        if (canvasRoot == null) return;
        foreach (var t in canvasRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!IsThemeablePanel(t))
                continue;
            ApplyTo(t);
        }
    }

    public static bool IsThemeablePanel(Transform root)
    {
        if (root == null || root is not RectTransform)
            return false;

        var name = root.name;
        if (name is "TopPanel" or "Canvas")
            return false;
        if (!name.EndsWith("Panel", System.StringComparison.Ordinal))
            return false;
        if (name.StartsWith("txt", System.StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("lbl", System.StringComparison.OrdinalIgnoreCase))
            return false;

        if (root.GetComponent<TMP_Text>() != null || root.GetComponent<Text>() != null)
            return false;

        return true;
    }

    private void ApplyRootBackground()
    {
        if (transform is not RectTransform)
            return;

        if (GetComponent<TMP_Text>() != null || GetComponent<Text>() != null)
            return;

        var img = GetComponent<Image>();
        if (img == null)
        {
            if (GetComponent<Graphic>() != null && GetComponent<Graphic>() is not Image)
                return;

            img = gameObject.AddComponent<Image>();
            if (img == null)
                return;
        }

        img.enabled = true;
        img.color = theme.panelBackground;
        img.type = Image.Type.Sliced;
    }

    private void ApplyAccentBar()
    {
        var rect = transform as RectTransform;
        if (rect == null) return;

        var accent = transform.Find(AccentBarName) as RectTransform;
        if (accent == null)
        {
            var go = new GameObject(AccentBarName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            accent = go.GetComponent<RectTransform>();
            accent.SetAsFirstSibling();
        }

        accent.anchorMin = new Vector2(0f, 1f);
        accent.anchorMax = new Vector2(1f, 1f);
        accent.pivot = new Vector2(0.5f, 1f);
        accent.anchoredPosition = Vector2.zero;
        accent.sizeDelta = new Vector2(0f, accentBarHeight);

        var accentImg = accent.GetComponent<Image>();
        accentImg.raycastTarget = false;
        accentImg.color = theme.panelBorder;
    }

    private void ApplyScrollAreas()
    {
        foreach (var scroll in GetComponentsInChildren<ScrollRect>(true))
        {
            var scrollImg = scroll.GetComponent<Image>();
            if (scrollImg != null)
                scrollImg.color = theme.scrollPanelBackground;

            if (scroll.viewport != null)
            {
                var vpImg = scroll.viewport.GetComponent<Image>();
                if (vpImg != null)
                    vpImg.color = theme.scrollViewportBackground;
            }

            StyleScrollbar(scroll.horizontalScrollbar);
            StyleScrollbar(scroll.verticalScrollbar);
        }
    }

    private void StyleScrollbar(Scrollbar scrollbar)
    {
        if (scrollbar == null) return;

        var bg = scrollbar.GetComponent<Image>();
        if (bg != null)
            bg.color = theme.scrollTrackBackground;

        if (scrollbar.handleRect != null)
        {
            var handle = scrollbar.handleRect.GetComponent<Image>();
            if (handle != null)
                handle.color = theme.scrollHandle;
        }
    }

    private void ApplyButtons()
    {
        foreach (var btn in GetComponentsInChildren<Button>(true))
        {
            var colors = btn.colors;
            colors.normalColor = theme.buttonNormal;
            colors.highlightedColor = theme.buttonHover;
            colors.pressedColor = theme.buttonPressed;
            colors.selectedColor = theme.buttonHover;
            colors.disabledColor = theme.backgroundDisabled;
            btn.colors = colors;
        }
    }

    private void ApplyTexts()
    {
        foreach (var tmp in GetComponentsInChildren<TMP_Text>(true))
        {
            switch (ClassifyText(tmp))
            {
                case TextStyle.Title:
                    tmp.color = theme.textAccent;
                    tmp.fontStyle = FontStyles.Bold;
                    if (tmp.fontSize < 15f) tmp.fontSize = 16f;
                    break;
                case TextStyle.Section:
                    tmp.color = theme.textAccent;
                    tmp.fontStyle = FontStyles.Bold;
                    break;
                case TextStyle.Label:
                    tmp.color = theme.textSecondary;
                    break;
                case TextStyle.Value:
                    tmp.color = theme.textPrimary;
                    tmp.fontStyle = FontStyles.Normal;
                    if (tmp.fontSize > 15f) tmp.fontSize = 14f;
                    break;
                default:
                    tmp.color = theme.textPrimary;
                    break;
            }
        }

        foreach (var legacy in GetComponentsInChildren<Text>(true))
            legacy.color = theme.textSecondary;
    }

    private static TextStyle ClassifyText(TMP_Text tmp)
    {
        var name = tmp.name;
        if (name is "txtNav" or "txtInventory" or "txtScans")
            return TextStyle.Title;

        if (name is "txtNearScan" or "txtFarScan")
            return TextStyle.Section;

        if (name is "txtItems" or "txtQueue" or "txtTasks" or "txtFabPanel" or "txtHeader")
            return TextStyle.Section;

        if (name.StartsWith("lbl"))
            return TextStyle.Label;

        if (name is "txtTarget" or "txtDistance" or "txtSpeed" or "txtCapacity" or "txtDestination"
            or "txtDescr" or "txtBauzeit")
            return TextStyle.Value;

        var text = tmp.text;
        if (text is "Navigation" or "Inventar" or "Scans" or "Management" or "Management Console")
            return TextStyle.Title;
        if (text is "Near Scan" or "Far Scan" or "Items" or "Production Queue" or "Tasks" or "Fabricator"
            or "New Task" or "Beschreibung")
            return TextStyle.Section;
        if (text is "Entfernung" or "Ziel" or "Geschwindigkeit" or "Name" or "Mode" or "Region")
            return TextStyle.Label;

        return TextStyle.Default;
    }
}
