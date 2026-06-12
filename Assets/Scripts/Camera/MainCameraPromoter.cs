// Assets/Scripts/Camera/MainCameraPromoter.cs
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class MainCameraPromoter : MonoBehaviour
{
    [Tooltip("Optional: Welcher Szenenname soll bevorzugt werden? (leer = egal)")]
    public string preferredSceneName = "10_Game";

    Camera self;
    AudioListener selfListener;

    void Awake()
    {
        self = GetComponent<Camera>();
        selfListener = GetComponent<AudioListener>() ?? gameObject.AddComponent<AudioListener>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        PromoteAsMain(); // gleich beim Aktivieren sicherstellen
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        PromoteAsMain(); // Falls OnEnable zu fr�h war, hier nochmal
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Bei Single- oder Additive-Loads erneut sicherstellen
        if (mode == LoadSceneMode.Single || scene == gameObject.scene || string.IsNullOrEmpty(preferredSceneName) || scene.name == preferredSceneName)
        {
            // eine Frame warten, bis alles aktiviert ist
            StartCoroutine(WaitAndPromote());
        }
    }

    System.Collections.IEnumerator WaitAndPromote()
    {
        yield return null;
        PromoteAsMain();
    }

    void PromoteAsMain()
    {
        // Diese Kamera soll die einzige MainCamera sein
        gameObject.tag = "MainCamera";
        self.enabled = true;
        if (selfListener) selfListener.enabled = true;

        // Alle anderen Kameras in geladenen Szenen deaktivieren und entmainen
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                    DemoteCamera(cam);
            }
        }
    }

    void DemoteCamera(Camera cam)
    {
        if (cam == null || cam == self) return;

        if (cam.CompareTag("MainCamera"))
            cam.tag = "Untagged";

        cam.enabled = false;

        var al = cam.GetComponent<AudioListener>();
        if (al) al.enabled = false;
    }
}
