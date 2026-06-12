using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gemeinsame UI-Helfer für Runtime-Overlay-Szenen (Management, Fabrikator, Forschung).
/// </summary>
public static class OverlayUiKit
{
    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public static RectTransform CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        go.GetComponent<Image>().color = color;
        return rt;
    }

    public static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    public static TextMeshProUGUI CreateLabel(Transform parent, string text, float size, FontStyles style,
        TextAlignmentOptions align, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        if (parent is RectTransform prt && prt.GetComponent<LayoutGroup>() != null)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 28f);
        }
        else
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    public static TextMeshProUGUI CreateTopLabel(Transform parent, string text, float size, FontStyles style, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 32f);
        rt.anchoredPosition = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.margin = new Vector4(16f, 8f, 16f, 0f);
        tmp.color = color;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    public static Button CreateCloseButton(Transform parent, UITheme theme, Action onClick)
    {
        var go = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(44f, 44f);
        rt.anchoredPosition = new Vector2(-12f, 0f);

        var img = go.GetComponent<Image>();
        img.color = theme != null ? theme.buttonNormal : new Color(0.08f, 0.16f, 0.27f, 1f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var label = CreateLabel(go.transform, "X", 24, FontStyles.Bold, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.zero, theme?.textPrimary ?? Color.white);
        label.raycastTarget = false;
        return btn;
    }

    public static Button CreateButton(Transform parent, string label, float width, float height, UITheme theme)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var le = go.GetComponent<LayoutElement>();
        le.minHeight = height + 8f;
        le.preferredHeight = height + 8f;
        le.minWidth = width;
        le.preferredWidth = width;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);

        var img = go.GetComponent<Image>();
        img.color = theme != null ? theme.buttonNormal : new Color(0.08f, 0.16f, 0.27f, 1f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        CreateLabel(go.transform, label, 14, FontStyles.Normal, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.zero, theme?.textPrimary ?? Color.white);
        return btn;
    }

    public static RectTransform CreateVerticalScroll(Transform parent, out RectTransform content)
    {
        var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scrollGo.transform.SetParent(parent, false);
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        Stretch(scrollRt);
        scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);

        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        var viewport = viewportGo.GetComponent<RectTransform>();
        Stretch(viewport);
        viewportGo.GetComponent<Image>().color = Color.white;
        viewportGo.GetComponent<Mask>().showMaskGraphic = false;

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewport, false);
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);

        var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 12f;
        vlg.padding = new RectOffset(12, 12, 12, 12);
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fitter = contentGo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        return scrollRt;
    }

    public static Slider CreateSlider(Transform parent, Vector2 offsetMin, Vector2 offsetMax, Color bg, Color fill)
    {
        var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        go.GetComponent<Image>().color = bg;

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(go.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        Stretch(fillRt);
        fillGo.GetComponent<Image>().color = fill;

        var slider = go.GetComponent<Slider>();
        slider.fillRect = fillRt;
        slider.targetGraphic = fillGo.GetComponent<Image>();
        slider.interactable = false;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        return slider;
    }
}
