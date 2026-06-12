// Assets/Scripts/UI/UITheme.cs
using UnityEngine;

/// <summary>
/// Zentrales Theme-System für konsistente UI-Farben im gesamten Spiel
/// </summary>
[CreateAssetMenu(fileName = "UITheme", menuName = "SpaceGame/UI/Theme")]
public class UITheme : ScriptableObject
{
    [Header("Background Colors")]
    public Color backgroundNormal = new Color(0.03f, 0.05f, 0.09f, 0.97f);
    public Color backgroundHover = new Color(0.06f, 0.10f, 0.17f, 0.98f);
    public Color backgroundSelected = new Color(0.10f, 0.25f, 0.41f, 1f);
    public Color backgroundDisabled = new Color(0.05f, 0.07f, 0.11f, 0.55f);
    
    [Header("Text Colors")]
    public Color textPrimary = new Color(0.84f, 0.93f, 1f, 1f);
    public Color textSecondary = new Color(0.43f, 0.56f, 0.69f, 1f);
    public Color textAccent = new Color(0.24f, 0.84f, 0.96f, 1f);
    public Color textDisabled = new Color(0.30f, 0.38f, 0.48f, 1f);
    
    [Header("Type Indicator Colors")]
    public Color asteroidColor = new Color(0.6f, 0.4f, 0.2f, 1f);
    public Color planetColor = new Color(0.2f, 0.6f, 0.8f, 1f);
    public Color starColor = new Color(1f, 0.9f, 0.3f, 1f);
    public Color stationColor = new Color(0.5f, 0.8f, 0.3f, 1f);
    public Color unknownColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    
    [Header("Status Colors")]
    public Color successColor = new Color(0.24f, 0.86f, 0.55f, 1f);
    public Color warningColor = new Color(1f, 0.72f, 0.22f, 1f);
    public Color errorColor = new Color(0.95f, 0.28f, 0.32f, 1f);
    public Color infoColor = new Color(0.24f, 0.72f, 0.95f, 1f);
    
    [Header("Resource Colors")]
    public Color ironColor = new Color(0.6f, 0.6f, 0.65f, 1f);
    public Color goldColor = new Color(1f, 0.84f, 0.0f, 1f);
    public Color silverColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    public Color carbonColor = new Color(0.2f, 0.2f, 0.25f, 1f);
    public Color iceColor = new Color(0.7f, 0.9f, 1f, 1f);
    
    [Header("UI Element Colors")]
    public Color panelBackground = new Color(0.04f, 0.07f, 0.13f, 0.93f);
    public Color panelBorder = new Color(0.18f, 0.61f, 0.79f, 0.85f);
    public Color panelHeaderBackground = new Color(0.06f, 0.11f, 0.19f, 0.98f);
    public Color scrollPanelBackground = new Color(0.02f, 0.03f, 0.055f, 0.98f);
    public Color scrollViewportBackground = new Color(0.015f, 0.025f, 0.045f, 0.99f);
    public Color scrollTrackBackground = new Color(0.05f, 0.08f, 0.14f, 0.80f);
    public Color scrollHandle = new Color(0.18f, 0.45f, 0.62f, 0.90f);
    public Color buttonNormal = new Color(0.08f, 0.16f, 0.27f, 1f);
    public Color buttonHover = new Color(0.12f, 0.24f, 0.40f, 1f);
    public Color buttonPressed = new Color(0.04f, 0.09f, 0.16f, 1f);
    
    [Header("Progress & Bars")]
    public Color progressEmpty = new Color(0.08f, 0.10f, 0.16f, 1f);
    public Color progressFull = new Color(0.18f, 0.80f, 0.53f, 1f);
    public Color progressWarning = new Color(1f, 0.7f, 0.2f, 1f);
    public Color progressCritical = new Color(0.9f, 0.2f, 0.2f, 1f);
    
    /// <summary>Singleton-Zugriff auf das aktive Theme</summary>
    private static UITheme _instance;
    public static UITheme Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<UITheme>("UITheme");
                if (_instance == null)
                {
                    Debug.LogWarning("[UITheme] Kein Theme in Resources gefunden. Erstelle Default-Theme.");
                    _instance = CreateInstance<UITheme>();
                }
            }
            return _instance;
        }
    }
    
    /// <summary>Gibt die Farbe für einen Material-Typ zurück</summary>
    public Color GetMaterialColor(string materialId)
    {
        if (string.IsNullOrEmpty(materialId))
            return unknownColor;
            
        return materialId.ToLower() switch
        {
            "iron" => ironColor,
            "gold" => goldColor,
            "silver" => silverColor,
            "carbon" => carbonColor,
            "ice" => iceColor,
            _ => unknownColor
        };
    }
    
    /// <summary>Gibt die Farbe für einen Progress-Wert zurück (0-1)</summary>
    public Color GetProgressColor(float progress)
    {
        if (progress <= 0f)
            return progressEmpty;
        if (progress >= 1f)
            return progressFull;
        if (progress < 0.25f)
            return progressCritical;
        if (progress < 0.5f)
            return progressWarning;
        
        return progressFull;
    }
    
    /// <summary>Gibt eine interpolierte Farbe zwischen zwei Farben zurück</summary>
    public Color Lerp(Color a, Color b, float t)
    {
        return Color.Lerp(a, b, Mathf.Clamp01(t));
    }
}


