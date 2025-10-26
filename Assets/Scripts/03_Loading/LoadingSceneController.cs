using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using TMPro;


public class LoadingScreenController : MonoBehaviour
{
    [Header("Preload & UI")]
    [SerializeField] private PreloadCatalog catalog;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI percentLabel;
    [SerializeField] private TextMeshProUGUI txtStatus;
    [SerializeField] private ScrollRect scrollRect;          // Dein ScrollView
    [SerializeField] private RectTransform content;          // Content unter ScrollView
    [SerializeField] private TMP_Text messageItemTemplate;   // Optionales TMP-Text-Prefab (inactive)

    private readonly Queue<GameObject> _items = new();
    //[Header("Szenennamen (anpassen falls abweichend)")]
    //[SerializeField] private string gameSceneName = "10_Game";
    //[SerializeField] private string uiSceneName = "10_Game_UI";

    [Header("Warte-/Timeout-Settings")]
    [SerializeField] private float routerWaitTimeoutSeconds = 10f;

    private void SetStatus(string msg)
    {
        if (txtStatus) txtStatus.text = msg;
        Debug.Log($"[Loading] {msg}");
        HandleMessage(msg);
    }

    private void Awake()
    {
        AutoWireReferences();
    }

    private async void Start()
    {
        try
        {
            Time.timeScale = 1;

            // 1) Addressables/Preload
            SetStatus("Starte Initialisierung …");
            var p = new Progress<float>(v =>
            {
                if (progressBar) progressBar.minValue = 0f;
                if (progressBar) progressBar.maxValue = 1f;
                if (progressBar) progressBar.value = v;
                if (percentLabel) percentLabel.text = $"{Mathf.RoundToInt(v * 100f)}%";
            });
            await UniTask.Yield();

            SetStatus("Initialisiere Assets …");
            if (AssetProvider.I != null && txtStatus != null)
                AssetProvider.I.SetStatusTarget(txtStatus);
            await AssetProvider.I.Initialize(catalog, p);
            SetStatus("Assets initialisiert.");

            // 2) SceneRouter optional abwarten
            SetStatus("Warte auf SceneRouter …");
            bool routerReady = await UniTask
                .WaitUntil(() => SceneRouter.I != null && !SceneRouter.I.IsBusy)
                .TimeoutWithoutException(TimeSpan.FromSeconds(Mathf.Max(1f, routerWaitTimeoutSeconds)));

            if (!routerReady || SceneRouter.I == null)
            {
                // Fallback ohne Router: Game Single, UI additiv
                SetStatus("Kein SceneRouter – lade Spiel direkt …");
                await LoadSingle("10_Game");

                SetStatus("Lade Benutzeroberfläche (additiv) …");
                await EnsureAdditiveLoaded("10_Game_UI");

                SetStatus("Fertig. Viel Spaß!");
                return;
            }

            await UniTask.Yield(); // Frame warten, damit Router-Ready-Status stabil ist.

            // 3) Mit Router: Game + UI in EINEM Set laden (verhindert, dass UI nachträglich 'aufgeräumt' wird)
            SetStatus("Lade Spielwelt + Benutzeroberfläche …");
            await SceneRouter.I.LoadSet(new[] { AppScene.Game, AppScene.GameUI });

            // WICHTIG: keine nachträgliche Ensure-Additive-Ladung mehr – um Doppel-Loads zu vermeiden.
            SetStatus("Fertig. Viel Spaß!");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Loading] Ausnahme im Loader: {ex}");
            SetStatus("Fehler beim Laden. Details im Log.");
        }
    }

    /// <summary> Lädt eine Szene als Single und wartet bis fertig. </summary>
    private async UniTask LoadSingle(string sceneName)
    {
        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (op == null) throw new InvalidOperationException($"Scene '{sceneName}' konnte nicht gestartet werden.");
        while (!op.isDone) await UniTask.Yield();
        var s = SceneManager.GetSceneByName(sceneName);
        if (s.IsValid()) SceneManager.SetActiveScene(s);
    }

    /// <summary>
    /// Stellt sicher, dass eine Szene additiv geladen ist. Wenn nicht geladen, wird sie additiv nachgeladen.
    /// Setzt bei Erfolg die aktive Szene nicht um (damit Game aktiv bleibt).
    /// </summary>
    private async UniTask EnsureAdditiveLoaded(string sceneName)
    {
        var existing = SceneManager.GetSceneByName(sceneName);
        if (existing.IsValid() && existing.isLoaded)
        {
            Debug.Log($"[Loading] '{sceneName}' ist bereits geladen (additiv erwartet).");
            return;
        }

        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        if (op == null) throw new InvalidOperationException($"Additives Laden von '{sceneName}' fehlgeschlagen.");
        while (!op.isDone) await UniTask.Yield();

        var loaded = SceneManager.GetSceneByName(sceneName);
        if (!loaded.IsValid() || !loaded.isLoaded)
            throw new InvalidOperationException($"'{sceneName}' wurde nach LoadSceneAsync nicht als geladen gemeldet.");

        Debug.Log($"[Loading] '{sceneName}' additiv geladen.");
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
        // davon wird dann für jeden Eintrag eine Instanz UNTER dem Content erzeugt.
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

    private void HandleMessage(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) return;

        if (!content)
        {
            Debug.LogWarning("[HUDMessageLogUI] Kein Content zugewiesen – Nachricht wird verworfen.");
            return;
        }

        EnsureTemplateExists();

        // Instanz anlegen
        var item = Instantiate(messageItemTemplate, content);
        item.gameObject.name = $"Message_{_items.Count + 1}";
        item.gameObject.SetActive(true);

        item.text = msg;

        _items.Enqueue(item.gameObject);

        // Bei vertikalem ScrollRect: 0 = unten, 1 = oben
            //scrollRect.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
    }
}
