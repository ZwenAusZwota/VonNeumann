using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SpaceGame.UI;

/// <summary>
/// Wendet das HUD-Panel-Layout (UITheme) auf die Management-Szene an.
/// </summary>
public static class ManagementPanelChrome
{
    private const string AccentBarName = "_HudAccentTop";
    private const float HeaderHeight = 50f;
    private const float SidePanelWidth = 500f;
    private const float OuterPadding = 10f;

    public static void Apply(Transform canvasRoot, Button resumeButton, GameObject taskPanel, GameObject fabPanel)
    {
        var theme = UITheme.Instance;
        if (theme == null || canvasRoot == null) return;

        ApplyCanvasBackdrop(canvasRoot, theme);

        var header = canvasRoot.Find("TopPanel") as RectTransform;
        var fabRect = fabPanel != null ? fabPanel.transform as RectTransform : null;
        var taskRect = taskPanel != null ? taskPanel.transform as RectTransform : null;

        ApplyResponsiveLayout(header, fabRect, taskRect);
        ApplyContentPanels(canvasRoot, theme);
        ApplyHeader(header, resumeButton, theme);

        if (resumeButton != null)
            DraggableHudPanel.ApplyStandardCloseButton(resumeButton);
    }

    private static void ApplyCanvasBackdrop(Transform canvasRoot, UITheme theme)
    {
        var img = canvasRoot.GetComponent<Image>();
        if (img == null) return;

        img.sprite = null;
        img.type = Image.Type.Simple;
        var bg = theme.panelBackground;
        bg.a = 0.97f;
        img.color = bg;
    }

    private static void ApplyResponsiveLayout(RectTransform header, RectTransform fab, RectTransform task)
    {
        if (header != null)
        {
            SetStretch(header, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, -HeaderHeight));
            header.SetAsFirstSibling();
        }

        float topInset = HeaderHeight + OuterPadding;
        float taskLeft = SidePanelWidth + OuterPadding * 2f;

        if (task != null)
        {
            SetStretch(
                task,
                new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-taskLeft, OuterPadding),
                new Vector2(-OuterPadding, -topInset));
            task.gameObject.SetActive(true);
        }

        if (fab != null)
        {
            SetStretch(
                fab,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(OuterPadding, OuterPadding),
                new Vector2(-taskLeft, -topInset));
            fab.gameObject.SetActive(true);
        }
    }

    private static void ApplyHeader(RectTransform header, Button resumeButton, UITheme theme)
    {
        if (header == null) return;

        var img = header.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = null;
            img.type = Image.Type.Simple;
            img.color = theme.panelHeaderBackground;
            img.raycastTarget = true;
        }

        EnsureAccentBar(header, theme);

        var title = header.GetComponentInChildren<TMP_Text>(true);
        if (title != null)
        {
            if (string.IsNullOrWhiteSpace(title.text) || title.text == "Text (TMP)")
                title.text = "Management";

            title.color = theme.textAccent;
            title.fontStyle = FontStyles.Bold;
            title.fontSize = 16f;
            title.alignment = TextAlignmentOptions.MidlineLeft;

            var titleRect = title.rectTransform;
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(12f, 0f);
            titleRect.offsetMax = new Vector2(-44f, 0f);
        }

        if (resumeButton != null)
        {
            var label = resumeButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = "Zurück";
                label.color = theme.textPrimary;
            }
        }
    }

    private static void EnsureAccentBar(RectTransform header, UITheme theme)
    {
        var accent = header.Find(AccentBarName) as RectTransform;
        if (accent == null)
        {
            var go = new GameObject(AccentBarName, typeof(RectTransform), typeof(Image));
            accent = go.GetComponent<RectTransform>();
            accent.SetParent(header, false);
            accent.SetAsFirstSibling();
        }

        accent.anchorMin = new Vector2(0f, 1f);
        accent.anchorMax = new Vector2(1f, 1f);
        accent.pivot = new Vector2(0.5f, 1f);
        accent.anchoredPosition = Vector2.zero;
        accent.sizeDelta = new Vector2(0f, 2f);

        var accentImg = accent.GetComponent<Image>();
        accentImg.raycastTarget = false;
        accentImg.color = theme.panelBorder;
    }

    private static void ApplyContentPanels(Transform canvasRoot, UITheme theme)
    {
        foreach (var t in canvasRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!HudPanelThemeApplier.IsThemeablePanel(t))
                continue;

            HudPanelThemeApplier.ApplyTo(t);
        }

        ApplyInputFields(canvasRoot, theme);
        ApplyPanelSectionHeaders(canvasRoot, theme);
    }

    private static void ApplyPanelSectionHeaders(Transform root, UITheme theme)
    {
        foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (!IsSectionHeader(tmp.name)) continue;

            tmp.color = theme.textAccent;
            tmp.fontStyle = FontStyles.Bold;
            if (tmp.fontSize > 15f) tmp.fontSize = 14f;
        }
    }

    private static bool IsSectionHeader(string name)
    {
        return name is "txtItems" or "txtQueue" or "txtTasks" or "txtFabPanel" or "txtHeader";
    }

    private static void ApplyInputFields(Transform root, UITheme theme)
    {
        foreach (var input in root.GetComponentsInChildren<TMP_InputField>(true))
        {
            if (input.textComponent != null)
                input.textComponent.color = theme.textPrimary;

            if (input.placeholder is TMP_Text placeholder)
                placeholder.color = theme.textSecondary;

            var bg = input.GetComponent<Image>();
            if (bg != null)
                bg.color = theme.scrollPanelBackground;
        }

        foreach (var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            if (dropdown.captionText != null)
                dropdown.captionText.color = theme.textPrimary;

            var bg = dropdown.GetComponent<Image>();
            if (bg != null)
                bg.color = theme.scrollPanelBackground;
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
}
