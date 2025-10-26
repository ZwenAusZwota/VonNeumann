using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceGame.UI; // f�r HUDMessageBus

[DisallowMultipleComponent]
public class HUDMessageLogUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private ScrollRect scrollRect;          // Dein ScrollView
    [SerializeField] private RectTransform content;          // Content unter ScrollView
    [SerializeField] private TMP_Text messageItemTemplate;   // Optionales TMP-Text-Prefab (inactive)

    [Header("Optionen")]
    [SerializeField] private bool appendTimestamp = true;
    [SerializeField] private string timeFormat = "HH:mm:ss";
    [SerializeField] private int maxEntries = 20;           // Ring-Puffer
    [SerializeField] private bool alwaysScrollToBottom = true;

    private readonly Queue<GameObject> _items = new();

    private void Awake()
    {
        AutoWireReferences();
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
        {
            Debug.LogWarning("[HUDMessageLogUI] 'content' ist nicht zugewiesen. Bitte im Inspector setzen (ScrollRect -> Content).");
        }
    }

    private void EnsureTemplateExists()
    {
        if (messageItemTemplate != null) return;

        // Erzeuge ein deaktiviertes Template neben dieser Komponente (nicht im Content),
        // davon wird dann f�r jeden Eintrag eine Instanz UNTER dem Content erzeugt.
        var go = new GameObject("MessageTemplate", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var tmp = go.AddComponent<TextMeshProUGUI>(); // WICHTIG: Konkreter Typ, nicht TMP_Text-Abstract
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.fontSize = 12;
        tmp.color = Color.white;
        tmp.margin = new Vector4(6, 2, 6, 2);
        tmp.text = "<template>";

        messageItemTemplate = tmp;
        messageItemTemplate.gameObject.SetActive(false);
    }

    private void HandleHudMessage(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) return;

        if (!content)
        {
            Debug.LogWarning("[HUDMessageLogUI] Kein Content zugewiesen � Nachricht wird verworfen.");
            return;
        }

        EnsureTemplateExists();

        // Instanz anlegen
        var item = Instantiate(messageItemTemplate, content);
        item.gameObject.name = $"Message_{_items.Count + 1}";
        item.gameObject.SetActive(true);

        item.text = appendTimestamp
            ? $"[{System.DateTime.Now.ToString(timeFormat)}]{msg}"
            : msg;

        _items.Enqueue(item.gameObject);
        TrimIfNeeded();

        // Layout & Auto-Scroll
        Canvas.ForceUpdateCanvases();
        if (alwaysScrollToBottom && scrollRect != null)
        {
            // Bei vertikalem ScrollRect: 0 = unten, 1 = oben
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

    // Optionale �ffentliche API (z.B. Testbutton im Editor)
    public void PostTest(string text) => HUDMessageBus.Post(text);
}
