// Assets/Scripts/UI/PlanetIndicatorManager.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlanetIndicatorManager : MonoBehaviour
{
    [Header("UI-Referenzen")]
    public Camera mainCam;
    public RectTransform hudRect;

    [Header("Sprites & Prefab")]
    public Sprite squareSprite;
    public GameObject indicatorPrefab;

    [Header("Größe & Rand")]
    public float iconSizePx = 8f;
    public float borderMargin = 6f;

    class Indicator
    {
        public PlanetDto planet;
        public Transform planetTf;
        public RectTransform rt;
        public Image icon;
        public TextMeshProUGUI label;
    }
    readonly List<Indicator> inds = new();

    /* 🔸 Nutzt die globale Reihenfolge ohne eigene Sortierung */
    public void BuildIndicators(List<PlanetDto> planets)
    {
        foreach (PlanetDto p in planets)
        {
            GameObject go = Instantiate(indicatorPrefab, hudRect);
            var ind = new Indicator
            {
                planet = p,
                planetTf = GameObject.Find(string.IsNullOrEmpty(p.displayName) ? p.name : p.displayName).transform,
                rt = go.GetComponent<RectTransform>(),
                icon = go.transform.Find("Icon").GetComponent<Image>(),
                label = go.transform.Find("Label").GetComponent<TextMeshProUGUI>()
            };
            ind.label.text = string.IsNullOrEmpty(p.displayName) ? p.name : p.displayName;
            inds.Add(ind);
        }
    }

    /* ------------ Laufzeit-Update --------------- */
    void LateUpdate()
    {
        foreach (var ind in inds)
        {
            Vector3 vp = mainCam.WorldToViewportPoint(ind.planetTf.position);
            bool inFront = vp.z > 0f;
            bool onScreen = inFront && vp.x is > 0 and < 1 && vp.y is > 0 and < 1;

            ind.icon.sprite = squareSprite;
            ind.icon.transform.rotation = Quaternion.identity;
            ind.rt.sizeDelta = new(iconSizePx, iconSizePx);

            if (onScreen)
            {
                ind.rt.anchoredPosition = ViewportToHUD(vp);
            }
            else
            {
                Vector2 vp2 = new(vp.x, vp.y);
                if (!inFront) vp2 *= -1f;           // hinter Kamera flippen
                vp2 = ClampToEdge(vp2);
                ind.rt.anchoredPosition = ViewportToHUD(new Vector3(vp2.x, vp2.y, 0));
            }
        }
    }

    /* ------------ Helper ------------------------ */
    Vector2 ViewportToHUD(Vector3 vp) =>
        new Vector2(
            (vp.x - 0.5f) * hudRect.sizeDelta.x,
            (vp.y - 0.5f) * hudRect.sizeDelta.y);

    Vector2 ClampToEdge(Vector2 vp)
    {
        vp -= Vector2.one * 0.5f;
        float max = 0.5f - borderMargin / Mathf.Min(hudRect.sizeDelta.x, hudRect.sizeDelta.y);
        vp = Vector2.ClampMagnitude(vp, max);
        return vp + Vector2.one * 0.5f;
    }

    static string Roman(int n)
    {
        string[] r = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
        return r[(n - 1) % r.Length];
    }
}
