// Assets/Scripts/World/CameraSceneSetup.cs
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraSceneSetup : MonoBehaviour
{
    public float farClip = 5_000_000f;

    void Awake()
    {
        var cam = GetComponent<Camera>();
        cam.clearFlags = (cam.clearFlags == CameraClearFlags.Nothing) ? CameraClearFlags.Skybox : cam.clearFlags;
        cam.nearClipPlane = Mathf.Min(0.3f, cam.nearClipPlane);
        cam.farClipPlane = Mathf.Max(farClip, cam.farClipPlane);
        cam.cullingMask = ~0; // alles sichtbar (zum Testen); später gezielt einschränken
    }
}
