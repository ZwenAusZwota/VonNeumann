using UnityEngine;
/// <summary>
/// Optional: keep the bottom dock out of device safe areas (notch/home bar).
/// Attach to the root panel that holds mainBar + subBar.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaPadding : MonoBehaviour
{
    public bool bottomOnly = true;
    Rect _last;
    RectTransform _rt;

    void Awake() => _rt = GetComponent<RectTransform>();
    void OnEnable() => Apply();
    void Update() { if (_last != Screen.safeArea) Apply(); }

    void Apply()
    {
        _last = Screen.safeArea;
        var sa = _last;

        // Convert safe area to anchor min/max
        Vector2 min = sa.position;
        Vector2 max = sa.position + sa.size;
        min.x /= Screen.width; min.y /= Screen.height;
        max.x /= Screen.width; max.y /= Screen.height;

        if (bottomOnly)
        {
            // Only push up from bottom
            _rt.anchorMin = new Vector2(0, 0);
            _rt.anchorMax = new Vector2(1, 0);
            _rt.pivot = new Vector2(0.5f, 0);
            _rt.offsetMin = new Vector2(0, Mathf.Round(sa.y));
            _rt.offsetMax = new Vector2(0, 0);
        }
        else
        {
            _rt.anchorMin = min; _rt.anchorMax = max;
            _rt.offsetMin = _rt.offsetMax = Vector2.zero;
        }
    }
}