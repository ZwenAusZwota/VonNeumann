using System; // wichtig für System.Progress<T>
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using TMPro;

public class LoadingScreenController : MonoBehaviour
{
    [SerializeField] private PreloadCatalog catalog;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI percentLabel;

    private async void Start()
    {
        var p = new System.Progress<float>(v =>
        {
            if (progressBar) progressBar.value = v;
            if (percentLabel) percentLabel.text = $"{Mathf.RoundToInt(v * 100f)}%";
        });

        await AssetProvider.I.Initialize(catalog, p);

        // 🔸 wichtig: warten bis der Router frei ist
        await UniTask.WaitUntil(() => SceneRouter.I != null && !SceneRouter.I.IsBusy);

        // Ein Frame geben, damit die 100%-UI noch sichtbar werden kann
        await UniTask.Yield();

        // 🔸 jetzt Game & GameUI laden; die Loading-Szene wird dabei automatisch entladen
        await SceneRouter.I.LoadSet(new[] { AppScene.Game, AppScene.GameUI });
    }
}
