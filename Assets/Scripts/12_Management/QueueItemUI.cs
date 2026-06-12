using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class QueueItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private Slider progress;
    [SerializeField] private TextMeshProUGUI txtProgress;
    [SerializeField] private TextMeshProUGUI txtEta;
    [SerializeField] private Button btnRemove;

    private FabricatorController controller;
    private ProductBlueprint bp;
    private int queueIndex = -1;     // Index in der echten Queue (0 = aktuell laufend)
    private bool isRunning;          // true = erstes Element → Fortschritt aktiv, kein Drag

    // Drag-Helfer
    private Transform parent;
    private int startSibling;
    private Vector2 dragOffset;

    /* ---------- Public Binding ---------- */

    // Laufendes Item (Index 0)
    public void BindRunningItem(FabricatorController c, ProductBlueprint b, int index, float pct, float timeRemaining)
    {
        controller = c;
        bp = b;
        queueIndex = index;
        isRunning = true;

        if (txtName) txtName.text = b.displayName;
        if (progress)
        {
            progress.gameObject.SetActive(true);
            progress.minValue = 0f;
            progress.maxValue = 1f;
            progress.value = pct;
        }
        if(txtProgress)
        {
            txtProgress.gameObject.SetActive(true);
            txtProgress.text = $"{Mathf.RoundToInt(pct * 100f)}%";
        }
        if (txtEta)
        {
            txtEta.gameObject.SetActive(true);
            txtEta.text = FormatEta(timeRemaining);
        }
        if (btnRemove)
        {
            btnRemove.interactable = true;
            btnRemove.onClick.RemoveAllListeners();
            btnRemove.onClick.AddListener(RemoveSelf);
        }
    }

    // Normales Item in der Warteschlange (Index >= 1)
    public void BindQueuedItem(FabricatorController c, ProductBlueprint b, int index)
    {
        controller = c;
        bp = b;
        queueIndex = index;
        isRunning = (index == 0);

        if (txtName) txtName.text = b.displayName;

        if (progress) progress.gameObject.SetActive(false);
        if (txtProgress) txtProgress.gameObject.SetActive(false);
        if (txtEta) txtEta.gameObject.SetActive(false);

        if (btnRemove)
        {
            btnRemove.onClick.RemoveAllListeners();
            btnRemove.onClick.AddListener(RemoveSelf);
            btnRemove.interactable = true;
        }
    }

    /* ---------- Entfernen ---------- */
    private void RemoveSelf()
    {
        if (controller == null) return;
        if (queueIndex < 0) return;
        controller.RemoveAt(queueIndex);
    }

    /* ---------- Drag & Drop ---------- */
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isRunning) return; // laufendes Item nicht ziehen
        parent = transform.parent;
        startSibling = transform.GetSiblingIndex();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)parent, eventData.position, eventData.pressEventCamera, out dragOffset);
        dragOffset = (Vector2)transform.localPosition - dragOffset;

        // Hebe das Item visuell hervor
        var g = GetComponent<CanvasGroup>();
        if (!g) g = gameObject.AddComponent<CanvasGroup>();
        g.blocksRaycasts = false;
        g.alpha = 0.85f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isRunning || parent == null) return;

        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)parent, eventData.position, eventData.pressEventCamera, out localPoint))
        {
            transform.localPosition = localPoint + dragOffset;

            // Reorder innerhalb der Geschwister nach Position Y
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i) as RectTransform;
                if (child == (RectTransform)transform) continue;

                if (transform.position.y > child.position.y && transform.GetSiblingIndex() > i)
                {
                    transform.SetSiblingIndex(i);
                }
                else if (transform.position.y < child.position.y && transform.GetSiblingIndex() < i)
                {
                    transform.SetSiblingIndex(i);
                }
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isRunning || parent == null) return;

        // Endgültige Zielposition
        int newSibling = transform.GetSiblingIndex();

        // CanvasGroup reset
        var g = GetComponent<CanvasGroup>();
        if (g)
        {
            g.blocksRaycasts = true;
            g.alpha = 1f;
        }

        // Mapping SiblingIndex → QueueIndex:
        // Im Panel wird (wenn ein laufender Job existiert) zuerst der laufende Job (Index 0) dargestellt,
        // danach die restlichen Queue-Items in gleicher Reihenfolge. Das heißt:
        // - Wenn es einen laufenden Job gibt, liegt der als erstes Kind im Container.
        // - Alle nachfolgenden Kinder entsprechen queueIndex 1..n
        // Wir verschieben nur die wartenden → also map: sichtbarer SiblingIndex → queueIndex
        int targetQueueIndex = newSibling;
        // Falls ein laufendes Item vorhanden ist, und dieses als erstes Element gerendert wurde,
        // dann beginnen die wartenden Items ab SiblingIndex 1 → queueIndex == SiblingIndex
        // (d.h. diese einfache Zuordnung passt bereits)
        // Sicherstellen, dass wir nicht versuchen, das laufende Item zu bewegen:
        if (targetQueueIndex == 0) targetQueueIndex = 1;

        // Controller informieren
        if (controller != null && queueIndex >= 0)
        {
            controller.MoveItem(queueIndex, targetQueueIndex);
        }

        // UI-Rebuild über Controller-Event läuft automatisch
    }

    /* ---------- Helpers ---------- */
    private static string FormatEta(float seconds)
    {
        if (seconds < 1f) return "sofort";
        int s = Mathf.CeilToInt(seconds);
        int m = s / 60;
        int r = s % 60;
        return m > 0 ? $"{m}m {r}s" : $"{r}s";
    }
}
