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

    [Header("Save/Flow")]
    [SerializeField] private string defaultSlotId = "slot_1";

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
        if (!SaveSystem.I.HasSlot(defaultSlotId)) { RefreshContinueUI(); return; }

        SetInteractable(false);
        try
        {
            // Ladepfad: Loading -> Game + GameUI, dann Save laden
            await SceneRouter.I.ToLoadGame();
            await SaveSystem.I.LoadAsync(defaultSlotId);
        }
        finally { SetInteractable(true); }
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
        bool has = SaveSystem.I != null && SaveSystem.I.HasSlot(defaultSlotId);
        if (btnContinue) btnContinue.interactable = has;

        if (continueDetailsLabel)
        {
            if (!has)
            {
                continueDetailsLabel.text = "Kein Spielstand gefunden";
            }
            else
            {
                var info = SaveSystem.I.ListSlots().FirstOrDefault(s => s.slotId == defaultSlotId);
                if (info.timestamp > 0)
                {
                    // Unix → lokale Zeit
                    var dt = DateTimeOffset.FromUnixTimeSeconds(info.timestamp).LocalDateTime;
                    continueDetailsLabel.text = $"Stand: {dt:yyyy-MM-dd HH:mm}";
                }
                else
                {
                    continueDetailsLabel.text = "Stand: unbekannt";
                }
            }
        }
    }

    private void SetInteractable(bool on)
    {
        if (btnNewGame) btnNewGame.interactable = on;
        if (btnContinue) btnContinue.interactable = on && (SaveSystem.I?.HasSlot(defaultSlotId) ?? false);
        if (btnOptions) btnOptions.interactable = on;
        if (btnQuit) btnQuit.interactable = on;
    }
}
