using UnityEngine;
using UnityEngine.UI;

public class ManagementSceneController : MonoBehaviour
{
    //[SerializeField] Button btnClose;      // „Zurück ins Spiel“ (oder „Schließen“)
    //[SerializeField] GameObject taskPanel; // dein TaskPanel-Root
    //[SerializeField] GameObject fabPanel;  // optional: Fabrikatorliste

    //void Start()
    //{
    //    if (btnClose) btnClose.onClick.AddListener(() => SceneRouter.Instance.CloseManagement());
    //    // Panels initial sichtbar?
    //    if (taskPanel) taskPanel.SetActive(true);
    //    if (fabPanel) fabPanel.SetActive(true);
    //}

    //public void CloseManagement()
    //{
    //    if (SceneRouter.Instance != null)
    //        SceneRouter.Instance.CloseManagement();   // zurück zur GameScene (additiv entladen)
    //    else
    //        Debug.LogWarning("SceneRouter.Instance ist null – ist die LoadingScene mit Router schon geladen?");
    //}

    //public void GoToGame()
    //{
    //    if (SceneRouter.Instance != null)
    //        SceneRouter.Instance.GoToGame();          // GameScene (Single) laden
    //}

    //public void ToggleManagement()
    //{
    //    if (SceneRouter.Instance != null)
    //        SceneRouter.Instance.ToggleManagement();  // falls du die Toggle‑Variante nutzt
    //}

}
