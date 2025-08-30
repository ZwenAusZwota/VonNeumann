using UnityEngine;
using Cysharp.Threading.Tasks;

public class MainMenuActions : MonoBehaviour
{
    public void OnNewGameClicked() => SceneRouter.I.ToNewGame().Forget();
    public void OnContinueClicked() => SceneRouter.I.ToLoadGame().Forget();
    public void OnMainMenuClicked() => SceneRouter.I.ToMainMenu().Forget();
}
