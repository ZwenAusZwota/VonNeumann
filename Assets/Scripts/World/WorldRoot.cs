// Assets/Scripts/World/WorldRoot.cs
using UnityEngine;

public class WorldRoot : MonoBehaviour
{
    private static WorldRoot _instance;
    public static WorldRoot Instance
    {
        get
        {
            if (_instance != null) return _instance;

            var go = new GameObject("World");
            _instance = go.AddComponent<WorldRoot>();
            DontDestroyOnLoad(go);

            _instance.starRoot = new GameObject("Stars").transform;
            _instance.planetsRoot = new GameObject("Planets").transform;
            _instance.beltsRoot = new GameObject("Belts").transform;

            _instance.starRoot.SetParent(go.transform, false);
            _instance.planetsRoot.SetParent(go.transform, false);
            _instance.beltsRoot.SetParent(go.transform, false);

            return _instance;
        }
    }

    [HideInInspector] public Transform starRoot;
    [HideInInspector] public Transform planetsRoot;
    [HideInInspector] public Transform beltsRoot;

    public enum Category { Star, Planet, Belt, Other }

    public void Attach(Transform t, Category cat, bool worldPos = false)
    {
        if (t == null) return;
        switch (cat)
        {
            case Category.Star: t.SetParent(starRoot, worldPos); break;
            case Category.Planet: t.SetParent(planetsRoot, worldPos); break;
            case Category.Belt: t.SetParent(beltsRoot, worldPos); break;
            default: t.SetParent(transform, worldPos); break;
        }
        // Sicherstellen, dass die Layer sichtbar sind (Default):
        t.gameObject.layer = 0;
    }
}
