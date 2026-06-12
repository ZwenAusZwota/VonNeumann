// Assets/Scripts/UI/MainMenuController.cs
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

public class MainMenuController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button btnNewGame;
    [SerializeField] private Button btnContinue;
    [SerializeField] private Button btnOptions;
    [SerializeField] private Button btnQuit;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private TextMeshProUGUI continueDetailsLabel; // z. B. "Stand: 2025-08-30 14:33"

    // Save/Flow: Lädt automatisch den neuesten Spielstand

    void Awake()
    {
        ApplyStandardLayout();
    }

    void Start()
    {
        // Sicherheit: Zeit normalisieren, UI-Eingaben aktivieren
        Time.timeScale = 1f;
        InputRouter.I?.SwitchToUI();

        // Button-Handler
        if (btnNewGame) btnNewGame.onClick.AddListener(() => OnNewGameClicked().Forget());
        if (btnContinue) btnContinue.onClick.AddListener(() => OnContinueClicked().Forget());
        if (btnOptions) btnOptions.onClick.AddListener(OnOptionsClicked);
        if (btnQuit) btnQuit.onClick.AddListener(OnQuitClicked);

        // Optionspanel zu
        if (optionsPanel) optionsPanel.SetActive(false);

        RefreshContinueUI();
    }

    void OnEnable() => RefreshContinueUI();

    private void ApplyStandardLayout()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindAnyObjectByType<Canvas>();

        RectTransform menuRoot = null;
        TextMeshProUGUI titleLabel = null;

        if (btnNewGame != null)
            menuRoot = btnNewGame.transform.parent?.parent as RectTransform;

        if (menuRoot != null)
        {
            foreach (var tmp in menuRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp.text.Contains("VON NEUMANN", System.StringComparison.OrdinalIgnoreCase))
                {
                    titleLabel = tmp;
                    break;
                }
            }
        }

        if (canvas != null && menuRoot != null)
            StandardMenuLayout.ApplyMainMenu(canvas, menuRoot, titleLabel);

        if (optionsPanel != null)
            HudPanelThemeApplier.ApplyTo(optionsPanel.transform);
    }

    // ----------------- Actions -----------------
    private async UniTask OnNewGameClicked()
    {
        SetInteractable(false);
        try
        {
            await SceneRouter.I.ToNewGame();
        }
        finally { SetInteractable(true); }
    }

    private async UniTask OnContinueClicked()
    {
        // Versuche zuerst autosave, dann fallback
        string slotToLoad = GetMostRecentSaveSlot();
        
        if (string.IsNullOrEmpty(slotToLoad))
        {
            Debug.LogWarning("[MainMenu] Kein Spielstand zum Laden gefunden.");
            RefreshContinueUI();
            return;
        }

        SetInteractable(false);
        try
        {
            // Ladepfad: Loading -> Game + GameUI, dann Save laden
            await SceneRouter.I.ToLoadGame();
            var saveSystem = ServiceContainer.Instance?.Get<SaveSystem>();
            if (saveSystem == null)
            {
                Debug.LogError("[MainMenu] SaveSystem nicht verfügbar!");
                return;
            }
            
            bool success = await saveSystem.LoadAsync(slotToLoad);
            
            if (!success)
            {
                Debug.LogError($"[MainMenu] Fehler beim Laden von Slot: {slotToLoad}");
                HUDMessageBus.Post("Fehler beim Laden des Spielstands!");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MainMenu] Exception beim Laden: {ex}");
            HUDMessageBus.Post("Fehler beim Laden!");
        }
        finally
        {
            SetInteractable(true);
        }
    }
    
    /// <summary>
    /// Gibt den Slot mit dem neuesten Timestamp zurück
    /// </summary>
    private string GetMostRecentSaveSlot()
    {
        var saveSystem = ServiceContainer.Instance?.Get<SaveSystem>();
        if (saveSystem == null) return null;
        
        var slots = saveSystem.ListSlots();
        if (slots == null || slots.Count == 0) return null;
        
        // Sortiere nach Timestamp (neueste zuerst)
        slots.Sort((a, b) => b.timestamp.CompareTo(a.timestamp));
        
        return slots[0].slotId;
    }

    private void OnOptionsClicked()
    {
        if (optionsPanel) optionsPanel.SetActive(true);
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ----------------- UI Helpers -----------------
    private void RefreshContinueUI()
    {
        var saveSystem = ServiceContainer.Instance?.Get<SaveSystem>();
        if (saveSystem == null)
        {
            if (btnContinue) btnContinue.interactable = false;
            if (continueDetailsLabel) continueDetailsLabel.text = "SaveSystem nicht verfügbar";
            return;
        }
        
        var slots = saveSystem.ListSlots();
        bool hasAnySave = slots != null && slots.Count > 0;
        
        if (btnContinue) btnContinue.interactable = hasAnySave;

        if (continueDetailsLabel)
        {
            if (!hasAnySave)
            {
                continueDetailsLabel.text = "Kein Spielstand gefunden";
            }
            else
            {
                // Zeige neuesten Spielstand
                slots.Sort((a, b) => b.timestamp.CompareTo(a.timestamp));
                var newest = slots[0];
                
                if (newest.timestamp > 0)
                {
                    // Unix → lokale Zeit
                    var dt = DateTimeOffset.FromUnixTimeSeconds(newest.timestamp).LocalDateTime;
                    string slotName = newest.slotId == "autosave" ? "Auto-Save" : newest.slotId;
                    continueDetailsLabel.text = $"{slotName} - {dt:yyyy-MM-dd HH:mm}";
                }
                else
                {
                    continueDetailsLabel.text = $"{newest.slotId} - Stand: unbekannt";
                }
            }
        }
    }

    private void SetInteractable(bool on)
    {
        if (btnNewGame) btnNewGame.interactable = on;
        
        // Continue nur wenn mindestens ein Save vorhanden ist
        var saveSystem = ServiceContainer.Instance?.Get<SaveSystem>();
        bool hasAnySave = saveSystem != null && saveSystem.ListSlots().Count > 0;
        if (btnContinue) btnContinue.interactable = on && hasAnySave;
        
        if (btnOptions) btnOptions.interactable = on;
        if (btnQuit) btnQuit.interactable = on;
    }
}
