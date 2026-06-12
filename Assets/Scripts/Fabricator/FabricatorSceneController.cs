using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Overlay-Szene für den Sonden-Fabrikator (F11). Spielwelt bleibt geladen.
/// </summary>
public class FabricatorSceneController : MonoBehaviour
{
    private bool _isBusy;
    private InputAction _cancelAction;

    private void Awake()
    {
        OverlaySceneCamera.Ensure();
        if (GetComponent<FabricatorUIController>() == null)
            gameObject.AddComponent<FabricatorUIController>();
    }

    private void OnEnable()
    {
        _cancelAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/escape");
        _cancelAction.performed += OnCancelPerformed;
        _cancelAction.Enable();
    }

    private void OnDisable()
    {
        if (_cancelAction == null) return;
        _cancelAction.performed -= OnCancelPerformed;
        _cancelAction.Disable();
        _cancelAction.Dispose();
        _cancelAction = null;
    }

    private void OnCancelPerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        CloseFabricator();
    }

    public void CloseFabricator()
    {
        if (_isBusy) return;
        ResumeToGameAsync().Forget();
    }

    private async UniTask ResumeToGameAsync()
    {
        _isBusy = true;
        try
        {
            Time.timeScale = 1f;
            var fabricator = SceneManager.GetSceneByName("14_Fabricator");
            if (fabricator.IsValid() && fabricator.isLoaded)
                await SceneManager.UnloadSceneAsync(fabricator).ToUniTask();

            var game = SceneManager.GetSceneByName("10_Game");
            if (game.IsValid())
                SceneManager.SetActiveScene(game);

#if UNITY_2023_1_OR_NEWER
            var hotkeys = Object.FindAnyObjectByType<SpaceGame.Input.GameHotkeys>(FindObjectsInactive.Include);
#else
            var hotkeys = Object.FindAnyObjectByType<SpaceGame.Input.GameHotkeys>();
#endif
            hotkeys?.ReenableGamePlay();
        }
        finally
        {
            _isBusy = false;
        }
    }
}
