using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceGame.UI;

[DisallowMultipleComponent]
public class HUDMessageLogUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;
    [SerializeField] private TMP_Text messageItemTemplate;

    [Header("Theme")]
    [SerializeField] private UITheme theme;
    [SerializeField] private bool useTheme = true;

    [Header("Typografie")]
    [SerializeField] private float messageFontSize = 15f;
    [SerializeField] private float messageMinHeight = 18f;
    [SerializeField] private float contentPadding = 6f;
    [SerializeField] private float entrySpacing = 1f;

    [Header("Optionen")]
    [SerializeField] private bool appendTimestamp = true;
    [SerializeField] private string timeFormat = "HH:mm:ss";
    [SerializeField] private int maxEntries = 20;
    [SerializeField] private bool alwaysScrollToBottom = true;

    private readonly Queue<GameObject> _items = new();

    private void Awake()
    {
        if (useTheme && theme == null)
            theme = UITheme.Instance;
        AutoWireReferences();
        ConfigureContentLayout();
    }

    private void Reset()
    {
        AutoWireReferences();
    }

    private void OnEnable()
    {
        HUDMessageBus.OnHudMessage += HandleHudMessage;
    }

    private void OnDisable()
    {
        HUDMessageBus.OnHudMessage -= HandleHudMessage;
    }

    private void AutoWireReferences()
    {
        if (!scrollRect) scrollRect = GetComponentInChildren<ScrollRect>(true);
        if (!content && scrollRect) content = scrollRect.content;

        if (!content)
            Debug.LogWarning("[HUDMessageLogUI] 'content' ist nicht zugewiesen. Bitte im Inspector setzen (ScrollRect -> Content).");
    }

    private void ConfigureContentLayout()
    {
        if (!content) return;

        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) return;

        int pad = Mathf.RoundToInt(contentPadding);
        vlg.padding = new RectOffset(pad, pad, pad, pad);
        vlg.spacing = entrySpacing;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
    }

    private void EnsureTemplateExists()
    {
        if (messageItemTemplate != null) return;

        var go = new GameObject("MessageTemplate", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        ApplyMessageStyle(tmp);

        messageItemTemplate = tmp;
        messageItemTemplate.gameObject.SetActive(false);
    }

    private void ApplyMessageStyle(TMP_Text tmp)
    {
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.fontSize = messageFontSize;
        tmp.lineSpacing = 0f;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.color = useTheme && theme != null ? theme.textPrimary : Color.white;
        tmp.margin = Vector4.zero;
        tmp.raycastTarget = false;
    }

    private void ConfigureMessageItem(GameObject go, TMP_Text tmp)
    {
        ApplyMessageStyle(tmp);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, messageMinHeight);

        var layout = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        layout.minHeight = messageMinHeight;
        layout.preferredHeight = messageMinHeight;
        layout.flexibleWidth = 1f;
    }

    private void HandleHudMessage(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) return;

        if (!content)
        {
            Debug.LogWarning("[HUDMessageLogUI] Kein Content zugewiesen – Nachricht wird verworfen.");
            return;
        }

        EnsureTemplateExists();

        var item = Instantiate(messageItemTemplate, content);
        item.gameObject.name = $"Message_{_items.Count + 1}";
        item.gameObject.SetActive(true);
        ConfigureMessageItem(item.gameObject, item);

        item.text = appendTimestamp
            ? $"[{System.DateTime.Now.ToString(timeFormat)}] {msg}"
            : msg;

        item.ForceMeshUpdate();
        var preferred = Mathf.Max(messageMinHeight, item.preferredHeight);
        var layout = item.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.minHeight = preferred;
            layout.preferredHeight = preferred;
        }

        _items.Enqueue(item.gameObject);
        TrimIfNeeded();

        Canvas.ForceUpdateCanvases();
        if (alwaysScrollToBottom && scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
        }
    }

    private void TrimIfNeeded()
    {
        if (maxEntries <= 0) return;
        while (_items.Count > maxEntries)
        {
            var oldest = _items.Dequeue();
            if (oldest) Destroy(oldest);
        }
    }

    public void PostTest(string text) => HUDMessageBus.Post(text);
}
