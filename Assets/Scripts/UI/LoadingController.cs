using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoadingController : MonoBehaviour
{
    [SerializeField] private Slider progressBar;   // Im Inspector zuweisen
    [SerializeField] private string targetSceneName = "SolarSystem";
    [SerializeField] private string loadingSceneName = "Loading";
    [SerializeField] private float fakeDelaySeconds = 0f; // optional

    private void Start()
    {
        StartCoroutine(LoadAndSwitch());
    }

    private IEnumerator LoadAndSwitch()
    {
        if (fakeDelaySeconds > 0f)
            yield return new WaitForSeconds(fakeDelaySeconds);

        // Szene additiv laden, Aktivierung zunächst verhindern, damit wir den Ladebalken sauber auf 100% bringen
        AsyncOperation op = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);
        if (op == null)
        {
            Debug.LogError($"Konnte Szene '{targetSceneName}' nicht laden.");
            yield break;
        }
        op.allowSceneActivation = false;

        // Fortschritt bis 0.9 anzeigen (Unity lädt bis 0.9, der Rest ist Aktivierung)
        while (op.progress < 0.9f)
        {
            UpdateProgress(op.progress / 0.9f); // normiert auf 0..1
            yield return null;
        }

        // Voll gefüllt anzeigen, kleine Pause für sauberes UI-Feedback (optional)
        UpdateProgress(1f);
        yield return null;

        // Zielszene referenzieren, aktiv setzen
        Scene targetScene = SceneManager.GetSceneByName(targetSceneName);
        if (!targetScene.IsValid())
        {
            // Falls noch nicht registriert, erst aktivieren, dann erneut holen
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            targetScene = SceneManager.GetSceneByName(targetSceneName);
        }

        // Jetzt aktivieren
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        if (targetScene.IsValid())
            SceneManager.SetActiveScene(targetScene);

        // --- HIER ggf. Objekt-/Datenübergaben vornehmen ---
        // Beispiel:
        // foreach (var root in targetScene.GetRootGameObjects()) { /* Übergabe/Verknüpfung */ }

        // Ladeszene entladen (erst NACH erfolgreichem Aktivieren)
        Scene loadingScene = SceneManager.GetSceneByName(loadingSceneName);
        if (loadingScene.IsValid())
            SceneManager.UnloadSceneAsync(loadingScene);
    }

    private void UpdateProgress(float normalized01)
    {
        if (progressBar != null)
        {
            progressBar.normalizedValue = Mathf.Clamp01(normalized01);
        }
    }
}
