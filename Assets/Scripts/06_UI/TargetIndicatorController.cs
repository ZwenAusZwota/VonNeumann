// Assets/Scripts/UI/TargetIndicatorController.cs
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class TargetIndicatorController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera worldCamera;              // Wird automatisch ermittelt, wenn leer
    [SerializeField] private RectTransform frameInView;       // UI-Rahmen (Image/Outline) für "im Bild"
    [SerializeField] private RectTransform offscreenMarker;   // Kleines Quadrat am Rand

    [Header("Target Source")]
    [Tooltip("Wenn aktiv, versucht der Controller zuerst ein Navigationsziel aus HUDBindingService.I auszulesen (SelectedNavigationTarget/… via Reflection).")]
    [SerializeField] private bool useSelectedNavigationTarget = true;

    [Header("Layout")]
    [SerializeField] private float edgePadding = 18f;         // Abstand vom Canvas-Rand
    [SerializeField] private Vector2 minFrameSize = new Vector2(24, 24);
    [SerializeField] private Vector2 maxFrameSize = new Vector2(420, 420);

    private Canvas canvas;
    private RectTransform canvasRect;

    // Optionaler Direktzugriff, falls du nicht über HUDBindingService gehst
    private Transform directTarget;

    private Coroutine cameraResolverCo;

    // Kandidatennamen für Navigationsziel in HUDBindingService
    private static readonly string[] NavTargetPropertyNames =
    {
        "SelectedNavigationTarget",
        "SelectedNavigation",
        "CurrentNavigationTarget",
        "SelectedNavTarget",
        "NavigationTarget",
        "CurrentTarget",
        "SelectedTarget",
    };

    void Awake()
    {
        canvas = GetComponent<Canvas>();
        canvasRect = canvas.transform as RectTransform;

        // Kinder automatisch auflösen, falls im Prefab nicht zugewiesen
        if (!frameInView) frameInView = transform.Find("FrameInView") as RectTransform;
        if (!offscreenMarker) offscreenMarker = transform.Find("OffscreenMarker") as RectTransform;

        if (frameInView) frameInView.gameObject.SetActive(false);
        if (offscreenMarker) offscreenMarker.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        // Kamera zyklisch auflösen, bis gefunden (für additive Szenen)
        if (cameraResolverCo == null) cameraResolverCo = StartCoroutine(CoResolveWorldCamera());
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        if (cameraResolverCo != null) { StopCoroutine(cameraResolverCo); cameraResolverCo = null; }
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnActiveSceneChanged(Scene a, Scene b)
    {
        // Bei Szenenwechsel erneut versuchen
        if (cameraResolverCo == null) cameraResolverCo = StartCoroutine(CoResolveWorldCamera());
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Bei additivem Laden erneut versuchen
        if (cameraResolverCo == null) cameraResolverCo = StartCoroutine(CoResolveWorldCamera());
    }

    IEnumerator CoResolveWorldCamera()
    {
        // Versuche mehrfach pro Sekunde, eine passende Kamera zu finden
        var wait = new WaitForSeconds(0.25f);

        while (!TryResolveCamera())
            yield return wait;

        cameraResolverCo = null;
    }

    bool TryResolveCamera()
    {
        // 1) Wenn Canvas Screen Space – Camera nutzt und worldCamera gesetzt ist: übernehmen
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null)
        {
            worldCamera = canvas.worldCamera;
            return true;
        }

        // 2) Falls bereits im Feld gesetzt und aktiv → gut
        if (worldCamera && worldCamera.isActiveAndEnabled) return true;

        // 3) Camera.main (MainCamera-Tag in der Spielszene 10_GAME)
        if (Camera.main && Camera.main.isActiveAndEnabled)
        {
            worldCamera = Camera.main;
            return true;
        }

        // 4) Fallback: aktive Kamera mit höchster Depth suchen (aus allen geladenen Szenen)
        Camera[] all = Camera.allCameras;
        Camera best = null;
        float bestDepth = float.NegativeInfinity;
        for (int i = 0; i < all.Length; i++)
        {
            var cam = all[i];
            if (!cam || !cam.isActiveAndEnabled) continue;
            if (cam.depth >= bestDepth)
            {
                best = cam;
                bestDepth = cam.depth;
            }
        }
        if (best != null)
        {
            worldCamera = best;
            return true;
        }

        return false;
    }

    void Update()
    {
        Transform targetTr = ResolveTargetTransform();

        if (!targetTr || !worldCamera)
        {
            SetActive(frameInView, false);
            SetActive(offscreenMarker, false);
            return;
        }

        // Sichtbarkeits-/Positionslogik
        if (!TryGetWorldBounds(targetTr, out Bounds worldBounds))
        {
            // Fallback: nur Transform-Punkt
            Vector3 sp = worldCamera.WorldToScreenPoint(targetTr.position);
            HandlePointOnly(sp);
            return;
        }

        if (TryGetScreenRectFromBounds(worldBounds, out Rect screenRect, out bool fullyBehindCamera))
        {
            if (!fullyBehindCamera && IsRectInsideViewport(screenRect))
            {
                PlaceInViewFrame(screenRect);
                SetActive(offscreenMarker, false);
            }
            else
            {
                PlaceOffscreenMarker(screenRect.center, fullyBehindCamera);
                SetActive(frameInView, false);
            }
        }
        else
        {
            PlaceOffscreenMarker((Vector2)worldCamera.WorldToScreenPoint(worldBounds.center), true);
            SetActive(frameInView, false);
        }
    }

    /* -------------------- Public API -------------------- */

    /// <summary>
    /// Optional: Ziel direkt setzen, falls du nicht über den HUDBindingService gehst.
    /// </summary>
    public void SetTarget(Transform t) => directTarget = t;

    /// <summary>
    /// Optional: Kamera direkt setzen (überschreibt Auto-Resolver).
    /// </summary>
    public void SetWorldCamera(Camera cam) => worldCamera = cam;

    /* -------------------- Target Resolution -------------------- */

    private Transform ResolveTargetTransform()
    {
        // 1) Direkte Zuweisung hat höchste Priorität
        if (directTarget) return directTarget;

        // 2) Versuche Navigationsziel aus HUDBindingService.I (Reflection),
        //    falls gewünscht und Service vorhanden
        if (useSelectedNavigationTarget && HUDBindingService.I != null)
        {
            var svc = HUDBindingService.I;
            var t = TryResolveTransformViaCandidates(svc, NavTargetPropertyNames);
            if (t) return t;
        }

        // 3) Fallback: SelectedItem aus HUDBindingService
        if (HUDBindingService.I != null)
        {
            // Erwartet: HUDBindingService.I.SelectedItem.Transform
            var siObj = GetPropertyValue(HUDBindingService.I, "SelectedItem");
            var t = ExtractTransform(siObj);
            if (t) return t;
        }

        return null;
    }

    private Transform TryResolveTransformViaCandidates(object host, string[] propNames)
    {
        foreach (var name in propNames)
        {
            var obj = GetPropertyValue(host, name);
            var t = ExtractTransform(obj);
            if (t) return t;
        }
        return null;
    }

    private static object GetPropertyValue(object host, string propName)
    {
        if (host == null) return null;
        var type = host.GetType();
        var prop = type.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null && prop.CanRead)
        {
            try { return prop.GetValue(host, null); }
            catch { /* ignorieren */ }
        }
        return null;
    }

    private static Transform ExtractTransform(object obj)
    {
        if (obj == null) return null;

        // 1) Selbst eine Transform?
        if (obj is Transform tr) return tr;

        // 2) Component/MonoBehaviour
        if (obj is Component comp) return comp.transform;

        // 3) Objekt mit Property "Transform"
        var prop = obj.GetType().GetProperty("Transform", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null && prop.CanRead)
        {
            try
            {
                var val = prop.GetValue(obj, null);
                if (val is Transform ptr) return ptr;
                if (val is Component pcomp) return pcomp.transform;
            }
            catch { /* ignorieren */ }
        }

        return null;
    }

    /* -------------------- In-View Frame -------------------- */

    void PlaceInViewFrame(Rect screenRect)
    {
        if (!frameInView) return;

        // Clamp auf sinnvolle Min/Max-Größe
        var size = screenRect.size;
        size.x = Mathf.Clamp(size.x, minFrameSize.x, maxFrameSize.x);
        size.y = Mathf.Clamp(size.y, minFrameSize.y, maxFrameSize.y);

        // Mittelpunkt des Rechtecks (Screen)
        Vector2 screenCenter = screenRect.center;

        // Screen → Canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenCenter,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : worldCamera,
            out Vector2 localCenter);

        frameInView.anchoredPosition = localCenter;
        frameInView.sizeDelta = size;

        SetActive(frameInView, true);
    }

    /* -------------------- Offscreen Marker -------------------- */

    void PlaceOffscreenMarker(Vector2 targetScreenCenter, bool behindCamera)
    {
        if (!offscreenMarker) return;

        // Richtung vom Bildschirmzentrum zur Zielprojektion
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 dir = (targetScreenCenter - screenCenter);

        // Hinter der Kamera → Richtung invertieren
        if (behindCamera) dir = -dir;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;

        // Position an Screen-Rand clampen
        Vector2 markerScreenPos = ProjectPointToScreenEdge(screenCenter, dir, edgePadding);

        // Screen → Canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            markerScreenPos,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : worldCamera,
            out Vector2 localPos);

        offscreenMarker.anchoredPosition = localPos;

        // Optional: Pfeilrotation statt Quadrat:
        // float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        // offscreenMarker.localEulerAngles = new Vector3(0, 0, angle - 90f);

        SetActive(offscreenMarker, true);
    }

    // Ray vom Zentrum in dir bis zum Rand des Bildschirm-Rechtecks, dann Padding
    Vector2 ProjectPointToScreenEdge(Vector2 center, Vector2 dir, float pad)
    {
        dir.Normalize();
        float left = pad;
        float right = Screen.width - pad;
        float bottom = pad;
        float top = Screen.height - pad;

        float tMin = float.NegativeInfinity;
        float tMax = float.PositiveInfinity;

        if (Mathf.Abs(dir.x) > 1e-5f)
        {
            float tx1 = (left - center.x) / dir.x;
            float tx2 = (right - center.x) / dir.x;
            tMin = Mathf.Max(tMin, Mathf.Min(tx1, tx2));
            tMax = Mathf.Min(tMax, Mathf.Max(tx1, tx2));
        }
        else
        {
            center.x = Mathf.Clamp(center.x, left, right);
        }

        if (Mathf.Abs(dir.y) > 1e-5f)
        {
            float ty1 = (bottom - center.y) / dir.y;
            float ty2 = (top - center.y) / dir.y;
            tMin = Mathf.Max(tMin, Mathf.Min(ty1, ty2));
            tMax = Mathf.Min(tMax, Mathf.Max(ty1, ty2));
        }
        else
        {
            center.y = Mathf.Clamp(center.y, bottom, top);
        }

        float t = Mathf.Clamp(tMin, 0f, tMax);
        Vector2 p = center + dir * t;

        p.x = Mathf.Clamp(p.x, left, right);
        p.y = Mathf.Clamp(p.y, bottom, top);
        return p;
    }

    /* -------------------- Helpers -------------------- */

    void HandlePointOnly(Vector3 screenPoint)
    {
        bool behind = screenPoint.z < 0f;

        bool inView = !behind &&
                      screenPoint.x >= 0 && screenPoint.x <= Screen.width &&
                      screenPoint.y >= 0 && screenPoint.y <= Screen.height;

        if (inView)
        {
            Rect r = new Rect(
                new Vector2(screenPoint.x, screenPoint.y) - (minFrameSize * 0.5f),
                minFrameSize);
            PlaceInViewFrame(r);
            SetActive(offscreenMarker, false);
        }
        else
        {
            PlaceOffscreenMarker(screenPoint, behind);
            SetActive(frameInView, false);
        }
    }

    bool TryGetWorldBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds();
        bool has = false;

        var renderers = s_ListRenderers; renderers.Clear();
        root.GetComponentsInChildren(true, renderers);

        foreach (var r in renderers)
        {
            if (!r.enabled) continue;
            if (!has)
            {
                bounds = r.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        return has;
    }

    bool TryGetScreenRectFromBounds(Bounds worldBounds, out Rect screenRect, out bool fullyBehindCamera)
    {
        fullyBehindCamera = false;

        Vector3 c = worldBounds.center;
        Vector3 e = worldBounds.extents;

        Vector3[] pts =
        {
            c + new Vector3(-e.x, -e.y, -e.z),
            c + new Vector3(-e.x, -e.y,  e.z),
            c + new Vector3(-e.x,  e.y, -e.z),
            c + new Vector3(-e.x,  e.y,  e.z),
            c + new Vector3( e.x, -e.y, -e.z),
            c + new Vector3( e.x, -e.y,  e.z),
            c + new Vector3( e.x,  e.y, -e.z),
            c + new Vector3( e.x,  e.y,  e.z)
        };

        float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;

        int behindCount = 0;
        for (int i = 0; i < pts.Length; i++)
        {
            Vector3 sp = worldCamera.WorldToScreenPoint(pts[i]);
            if (sp.z < 0f) behindCount++;

            minX = Mathf.Min(minX, sp.x);
            minY = Mathf.Min(minY, sp.y);
            maxX = Mathf.Max(maxX, sp.x);
            maxY = Mathf.Max(maxY, sp.y);
        }

        fullyBehindCamera = behindCount == pts.Length;

        screenRect = new Rect(new Vector2(minX, minY), new Vector2(maxX - minX, maxY - minY));

        if (float.IsNaN(screenRect.xMin) || float.IsInfinity(screenRect.xMin)) return false;

        if (screenRect.width < 0.5f || screenRect.height < 0.5f)
        {
            screenRect.size = minFrameSize;
        }
        return true;
    }

    bool IsRectInsideViewport(Rect r)
    {
        Vector2 center = r.center;
        return center.x >= 0 && center.x <= Screen.width &&
               center.y >= 0 && center.y <= Screen.height;
    }

    static void SetActive(Component c, bool on)
    {
        if (!c) return;
        if (c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
    }

    static readonly List<Renderer> s_ListRenderers = new List<Renderer>(32);
}
