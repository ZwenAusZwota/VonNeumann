using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Overlay-Szene für den Forschungsbaum (F9). Pausiert das Spiel wie das Pause-Menü.
/// </summary>
public class ScienceTreeSceneController : MonoBehaviour
{
    private bool _isBusy;
    private InputAction _cancelAction;

    private void Awake()
    {
        OverlaySceneCamera.Ensure();
        if (GetComponent<ScienceTreeUIController>() == null)
            gameObject.AddComponent<ScienceTreeUIController>();
    }

    private void OnEnable()
    {
        _cancelAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/escape");
        _cancelAction.performed += OnCancelPerformed;
        _cancelAction.Enable();
    }

    private void OnDisable()
    {
        if (_cancelAction != null)
        {
            _cancelAction.performed -= OnCancelPerformed;
            _cancelAction.Disable();
            _cancelAction.Dispose();
            _cancelAction = null;
        }
    }

    private void OnCancelPerformed(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        CloseScienceTree();
    }

    public void CloseScienceTree()
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

            if (SceneRouter.I != null)
                await SceneRouter.I.LoadSet(new[] { AppScene.Game, AppScene.GameUI });

            var science = SceneManager.GetSceneByName("13_ScienceTree");
            if (science.IsValid() && science.isLoaded)
                await SceneManager.UnloadSceneAsync(science).ToUniTask();

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
