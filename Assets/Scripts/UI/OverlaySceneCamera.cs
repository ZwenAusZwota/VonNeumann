using UnityEngine;

/// <summary>
/// Verhindert die Unity-Meldung „No cameras rendering“ in reinen UI-Overlays.
/// </summary>
public static class OverlaySceneCamera
{
    public static void Ensure()
    {
#if UNITY_2023_1_OR_NEWER
        if (Object.FindAnyObjectByType<Camera>() != null) return;
#else
        if (Object.FindObjectOfType<Camera>() != null) return;
#endif
        var camGo = new GameObject("OverlayCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.07f, 0.13f, 1f);
        cam.cullingMask = 0;
        cam.depth = -100;
    }
}
