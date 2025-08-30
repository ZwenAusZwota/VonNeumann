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
        var progress = new System.Progress<float>(v =>
        {
            if (progressBar) progressBar.value = v;
            if (percentLabel) percentLabel.text = $"{Mathf.RoundToInt(v * 100f)}%";
        });

        await AssetProvider.I.Initialize(catalog, progress);
        await SceneRouter.I.LoadSet(new[] { AppScene.Game, AppScene.GameUI });
    }
}
