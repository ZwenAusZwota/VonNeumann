// Assets/Scripts/UI/OptionsPanelController.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OptionsPanelController : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Slider masterVolume;   // 0..1
    [SerializeField] private Toggle fullscreenToggle;

    const string PREF_VOL = "opt_masterVol";
    const string PREF_FS = "opt_fullscreen";

    void OnEnable()
    {
        float vol = PlayerPrefs.GetFloat(PREF_VOL, 1f);
        bool fs = PlayerPrefs.GetInt(PREF_FS, Screen.fullScreen ? 1 : 0) == 1;

        if (masterVolume)
        {
            masterVolume.SetValueWithoutNotify(vol);
            masterVolume.onValueChanged.AddListener(SetMasterVolume);
        }
        if (fullscreenToggle)
        {
            fullscreenToggle.SetIsOnWithoutNotify(fs);
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }

        Apply(vol, fs);
    }

    void OnDisable()
    {
        if (masterVolume) masterVolume.onValueChanged.RemoveAllListeners();
        if (fullscreenToggle) fullscreenToggle.onValueChanged.RemoveAllListeners();
    }

    public void Close()
    {
        if (panelRoot) panelRoot.SetActive(false);
        else gameObject.SetActive(false);
    }

    void SetMasterVolume(float v)
    {
        PlayerPrefs.SetFloat(PREF_VOL, v);
        Apply(v, fullscreenToggle ? fullscreenToggle.isOn : Screen.fullScreen);
    }

    void SetFullscreen(bool fs)
    {
        PlayerPrefs.SetInt(PREF_FS, fs ? 1 : 0);
        Apply(masterVolume ? masterVolume.value : 1f, fs);
    }

    void Apply(float vol, bool fs)
    {
        AudioListener.volume = Mathf.Clamp01(vol);
        if (Screen.fullScreen != fs) Screen.fullScreen = fs;
    }
}
