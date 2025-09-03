// Assets/Scripts/Camera/OneShotBootstrapCamera.cs
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Camera))]
public class OneShotBootstrapCamera : MonoBehaviour
{
    [Tooltip("Welche Szene soll die Kontrolle übernehmen?")]
    public string handoffToScene = "10_Game";

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == handoffToScene)
        {
            // Eine Frame warten, damit die Zielkamera sicher aktiv ist
            StartCoroutine(DisableNextFrame());
        }
    }

    System.Collections.IEnumerator DisableNextFrame()
    {
        yield return null;
        gameObject.SetActive(false); // Bootstrap-Kamera aus
    }
}
