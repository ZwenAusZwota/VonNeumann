using UnityEngine;

public class PauseController : MonoBehaviour
{
    public void SetPaused(bool paused)
    {
        Time.timeScale = paused ? 0f : 1f;
        InputRouter.I.SetGameplayEnabled(!paused);
        InputRouter.I.SetUIEnabled(paused);
        // Optional: EventSystem der GameScene deaktivieren, UI-EventSystem aktivieren
    }
}
