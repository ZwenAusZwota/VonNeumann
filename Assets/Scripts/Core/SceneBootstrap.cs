// Assets/Scripts/SceneBootstrap.cs
using UnityEngine;

public class SceneBootstrap : MonoBehaviour
{
    void Start()
    {
        // schwaches, gerichtetes Licht für minimale Sichtbarkeit
        var lightGO = new GameObject("KeyLight");
        var light = lightGO.AddComponent<Light>();

        light.type = LightType.Directional;
        light.intensity = 0.1f;
        lightGO.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
    }
}
