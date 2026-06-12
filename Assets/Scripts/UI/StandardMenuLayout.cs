using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Einheitliches Vollbild-Panel-Layout für Menü- und Ladebildschirme
/// (entspricht Management / Fabrikator / Forschung).
/// </summary>
public static class StandardMenuLayout
{
    public const float HeaderHeight = 72f;
    public const float BodyPadding = 16f;
    public static readonly Vector2 ReferenceResolution = new(1920f, 1080f);

    private const string BackdropName = "StandardLayout_Backdrop";
    private const string PanelName = "StandardLayout_Panel";
    private const string AccentBarName = "_HudAccentTop";

    public static void ApplyCanvas(Canvas canvas)
    {
        if (canvas == null) return;

        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
    }

    public static void ApplyLoadingScreen(
        Canvas canvas,
        ScrollRect logScroll,
        RectTransform progressArea,
        Slider progressBar,
        TextMeshProUGUI percentLabel,
        TextMeshProUGUI statusLabel)
    {
        if (canvas == null) return;

        var theme = UITheme.Instance;
        ApplyCanvas(canvas);
        EnsureBackdrop(canvas.transform, theme);

        var panel = EnsurePanel(canvas.transform, theme);
        var header = EnsureHeader(panel, "Initialisierung", theme);
        var body = EnsureBody(panel, theme, footerHeight: 108f);
        var footer = EnsureFooter(panel, theme, 108f);

        if (logScroll != null)
        {
            var scrollRt = logScroll.transform as RectTransform;
            scrollRt.SetParent(body, false);
            Stretch(scrollRt);
            StyleScrollRect(logScroll, theme);
        }

        if (progressArea != null)
        {
            progressArea.SetParent(footer, false);
            Stretch(progressArea);

            var footerLayout = progressArea.GetComponent<VerticalLayoutGroup>();
            if (footerLayout == null)
                footerLayout = progressArea.gameObject.AddComponent<VerticalLayoutGroup>();
            footerLayout.padding = new RectOffset(16, 16, 8, 8);
            footerLayout.spacing = 6f;
            footerLayout.childAlignment = TextAnchor.MiddleCenter;
            footerLayout.childControlWidth = true;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = true;
            footerLayout.childForceExpandHeight = false;
        }

        if (progressBar != null)
            StyleSlider(progressBar, theme);

        if (percentLabel != null)
            StyleValueText(percentLabel, theme);

        if (statusLabel != null)
            StyleSecondaryText(statusLabel, theme);

        header.SetAsFirstSibling();
    }

    public static void ApplyMainMenu(Canvas canvas, RectTransform menuRoot, TextMeshProUGUI titleLabel)
    {
        if (canvas == null || menuRoot == null) return;

        var theme = UITheme.Instance;
        ApplyCanvas(canvas);
        EnsureBackdrop(canvas.transform, theme);

        // Dekoratives Hintergrundbild ausblenden – Panel übernimmt das Theme.
        var decorativeBg = menuRoot.Find("Image")?.GetComponent<Image>();
        if (decorativeBg != null)
            decorativeBg.enabled = false;

        menuRoot.SetAsLastSibling();
        StretchWithPadding(menuRoot, BodyPadding);

        if (menuRoot.GetComponent<VerticalLayoutGroup>() is { } oldLayout)
            Object.Destroy(oldLayout);

        var panelImage = menuRoot.GetComponent<Image>();
        if (panelImage == null)
            panelImage = menuRoot.gameObject.AddComponent<Image>();
        panelImage.color = theme.panelBackground;
        panelImage.raycastTarget = true;

        string title = titleLabel != null ? titleLabel.text : "Von Neumann";
        var header = EnsureHeader(menuRoot, title, theme);
        var body = EnsureBody(menuRoot, theme, footerHeight: 0f);

        if (titleLabel != null)
            titleLabel.gameObject.SetActive(false);

        var toMove = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < menuRoot.childCount; i++)
        {
            var child = menuRoot.GetChild(i);
            if (child == header || child == body) continue;
            if (child.name == AccentBarName) continue;
            toMove.Add(child);
        }

        foreach (var child in toMove)
            child.SetParent(body, false);

        EnsureBodyLayout(body);
        StyleButtons(body, theme);
        StyleTexts(body, theme);
        header.SetAsFirstSibling();
    }

    private static void EnsureBackdrop(Transform canvas, UITheme theme)
    {
        var backdrop = canvas.Find(BackdropName) as RectTransform;
        if (backdrop == null)
        {
            var img = OverlayUiKit.CreateImage(canvas, BackdropName, theme.panelBackground);
            backdrop = img.rectTransform;
            backdrop.SetAsFirstSibling();
        }
        else
        {
            var img = backdrop.GetComponent<Image>();
            if (img != null) img.color = theme.panelBackground;
        }

        Stretch(backdrop);
    }

    private static RectTransform EnsurePanel(Transform canvas, UITheme theme)
    {
        var panel = canvas.Find(PanelName) as RectTransform;
        if (panel != null)
            return panel;

        panel = OverlayUiKit.CreatePanel(canvas, PanelName, Vector2.zero, Vector2.one,
            Vector2.one * BodyPadding, Vector2.one * -BodyPadding, theme.panelBackground);
        return panel;
    }

    private static RectTransform EnsureHeader(RectTransform panel, string title, UITheme theme)
    {
        var header = panel.Find("Header") as RectTransform;
        if (header == null)
        {
            header = OverlayUiKit.CreatePanel(panel, "Header",
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -HeaderHeight), Vector2.zero,
                theme.panelHeaderBackground);
            EnsureAccentBar(header, theme);
            OverlayUiKit.CreateLabel(header, title, 26, FontStyles.Bold, TextAlignmentOptions.MidlineLeft,
                new Vector2(24f, 0f), new Vector2(-24f, 0f), theme.textPrimary);
        }
        else
        {
            var titleTmp = header.GetComponentInChildren<TextMeshProUGUI>(true);
            if (titleTmp != null)
            {
                titleTmp.text = title;
                titleTmp.color = theme.textPrimary;
            }
            var headerImg = header.GetComponent<Image>();
            if (headerImg != null) headerImg.color = theme.panelHeaderBackground;
            EnsureAccentBar(header, theme);
        }

        return header;
    }

    private static RectTransform EnsureBody(RectTransform panel, UITheme theme, float footerHeight)
    {
        var body = panel.Find("Body") as RectTransform;
        float topInset = HeaderHeight;
        float bottomInset = BodyPadding + footerHeight;
        if (body == null)
        {
            body = OverlayUiKit.CreatePanel(panel, "Body", Vector2.zero, Vector2.one,
                new Vector2(0f, bottomInset), new Vector2(0f, -topInset),
                theme.scrollPanelBackground);
        }
        else
        {
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(0f, bottomInset);
            body.offsetMax = new Vector2(0f, -topInset);
            var bodyImg = body.GetComponent<Image>();
            if (bodyImg != null) bodyImg.color = theme.scrollPanelBackground;
        }

        return body;
    }

    private static RectTransform EnsureFooter(RectTransform panel, UITheme theme, float height)
    {
        var footer = panel.Find("Footer") as RectTransform;
        if (footer == null)
        {
            footer = OverlayUiKit.CreatePanel(panel, "Footer",
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, height),
                theme.backgroundNormal);
        }
        else
        {
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.offsetMin = Vector2.zero;
            footer.offsetMax = new Vector2(0f, height);
        }

        return footer;
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

    private static void StyleScrollRect(ScrollRect scroll, UITheme theme)
    {
        var scrollImg = scroll.GetComponent<Image>();
        if (scrollImg != null)
            scrollImg.color = theme.scrollViewportBackground;

        if (scroll.viewport != null)
        {
            var vpImg = scroll.viewport.GetComponent<Image>();
            if (vpImg != null)
                vpImg.color = theme.scrollViewportBackground;
        }

        StyleScrollbar(scroll.verticalScrollbar, theme);
        StyleScrollbar(scroll.horizontalScrollbar, theme);

        foreach (var tmp in scroll.GetComponentsInChildren<TextMeshProUGUI>(true))
            StyleSecondaryText(tmp, theme);
    }

    private static void StyleScrollbar(Scrollbar scrollbar, UITheme theme)
    {
        if (scrollbar == null) return;

        var bg = scrollbar.GetComponent<Image>();
        if (bg != null) bg.color = theme.scrollTrackBackground;

        if (scrollbar.handleRect != null)
        {
            var handle = scrollbar.handleRect.GetComponent<Image>();
            if (handle != null) handle.color = theme.scrollHandle;
        }
    }

    private static void StyleSlider(Slider slider, UITheme theme)
    {
        var bg = slider.GetComponent<Image>();
        if (bg != null) bg.color = theme.progressEmpty;

        if (slider.fillRect != null)
        {
            var fill = slider.fillRect.GetComponent<Image>();
            if (fill != null) fill.color = theme.progressFull;
        }
    }

    private static void StyleButtons(Transform root, UITheme theme)
    {
        foreach (var btn in root.GetComponentsInChildren<Button>(true))
        {
            var colors = btn.colors;
            colors.normalColor = theme.buttonNormal;
            colors.highlightedColor = theme.buttonHover;
            colors.pressedColor = theme.buttonPressed;
            colors.selectedColor = theme.buttonHover;
            colors.disabledColor = theme.backgroundDisabled;
            btn.colors = colors;

            var img = btn.GetComponent<Image>();
            if (img != null) img.color = theme.buttonNormal;

            var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                StyleValueText(label, theme);
        }
    }

    private static void StyleTexts(Transform root, UITheme theme)
    {
        foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.GetComponentInParent<Button>() != null) continue;
            StyleSecondaryText(tmp, theme);
        }
    }

    private static void StyleValueText(TextMeshProUGUI tmp, UITheme theme)
    {
        tmp.color = theme.textPrimary;
        if (tmp.fontSize < 13f) tmp.fontSize = 14f;
    }

    private static void StyleSecondaryText(TextMeshProUGUI tmp, UITheme theme)
    {
        tmp.color = theme.textSecondary;
        if (tmp.fontSize < 12f) tmp.fontSize = 13f;
    }

    private static void EnsureBodyLayout(RectTransform body)
    {
        var vlg = body.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
            vlg = body.gameObject.AddComponent<VerticalLayoutGroup>();

        vlg.padding = new RectOffset(24, 24, 24, 24);
        vlg.spacing = 10f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void StretchWithPadding(RectTransform rt, float padding)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.one * padding;
        rt.offsetMax = Vector2.one * -padding;
    }
}
