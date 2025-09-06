// Assets/Editor/BootstrapPlayToolbar.cs
// Platziert einen "▶ Bootstrap"-Button direkt im Play-Mode-Cluster der Unity-Toolbar.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using System.Reflection;

[InitializeOnLoad]
public static class BootstrapPlayToolbar
{
    // Passe den Pfad bei Bedarf an:
    private const string BootstrapScenePath = "Assets/Scenes/00_Bootstrap.unity";

    // interner Guard, damit wir nur einmal einhängen
    private static bool _added;
    private static Button _button;

    static BootstrapPlayToolbar()
    {
        // Beim Editor-Update versuchen wir, den Button in die Toolbar einzubauen.
        EditorApplication.update -= TryAddButton;
        EditorApplication.update += TryAddButton;
    }

    private static void TryAddButton()
    {
        if (_added) return;

        // Über Reflection die Toolbar finden
        var toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
        if (toolbarType == null) return;

        var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
        if (toolbars == null || toolbars.Length == 0) return;

        // Die erste (und i.d.R. einzige) Toolbar nehmen
        var toolbar = toolbars[0];

        // Auf das GUIView.visualTree zugreifen
        var guiViewType = typeof(Editor).Assembly.GetType("UnityEditor.GUIView");
        var visualTreeProp = guiViewType?.GetProperty("visualTree",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (visualTreeProp == null) return;

        var root = visualTreeProp.GetValue(toolbar) as VisualElement;
        if (root == null) return;

        // Die Play-Mode-Zone suchen (zentrale Zone mit Play/Pause/Step).
        // Gängiger Name in neueren Unity-Versionen: "ToolbarZonePlayMode"
        var playZone = root.Q("ToolbarZonePlayMode");
        if (playZone == null)
        {
            // Fallback: Manche Versionen nutzen andere Zonennamen
            // (sehr selten, aber wir versuchen es der Vollständigkeit halber):
            playZone = root.Q(name: "PlayControls") ?? root.Q(className: "ToolbarZonePlayMode");
        }
        if (playZone == null) return;

        // Button erstellen und direkt in diese Zone hängen
        _button = new Button(OnClickPlayBootstrap)
        {
            text = "▶ Bootstrap",
            tooltip = "Startet immer die Szene 00_Bootstrap"
        };

        // dezente Maße, damit er sich optisch einfügt
        _button.style.marginLeft = 6;
        _button.style.marginRight = 2;
        _button.style.height = 18;     // orientiert an den Play-Buttons
        _button.style.unityTextAlign = TextAnchor.MiddleCenter;

        playZone.Add(_button);

        _added = true;

        // Optional: Bei Domain-Reloads Styling refreshen
        EditorApplication.playModeStateChanged += _ => _button?.MarkDirtyRepaint();
    }

    private static void OnClickPlayBootstrap()
    {
        // Wenn bereits im Playmode -> erst stoppen (zweiter Klick startet dann wieder aus der Bootstrap-Szene)
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            return;
        }

        // Ungespeicherte Änderungen sichern
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // Bootstrap-Szene laden und Play starten
        var scene = EditorSceneManager.OpenScene(BootstrapScenePath);
        if (scene.IsValid())
        {
            EditorApplication.isPlaying = true;
        }
        else
        {
            Debug.LogError($"BootstrapPlayToolbar: Szene nicht gefunden oder ungültig: {BootstrapScenePath}");
        }
    }
}
