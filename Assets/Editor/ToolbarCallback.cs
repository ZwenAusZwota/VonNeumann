// Assets/Editor/ToolbarCallback.cs
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ToolbarCallback
{
    public static Action OnToolbarGUI;

    static ToolbarCallback()
    {
        ToolbarCallbackInternal.Initialize();
    }

    private class ToolbarCallbackInternal
    {
        static Type toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
        static Type guiViewType = typeof(Editor).Assembly.GetType("UnityEditor.GUIView");
        static PropertyInfo viewVisualTree = guiViewType.GetProperty("visualTree",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo imguiContainerOnGui = typeof(UnityEngine.UIElements.IMGUIContainer).GetField("m_OnGUIHandler",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        static object toolbar;
        static ScriptableObject currentToolbar;

        public static void Initialize()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        static void OnUpdate()
        {
            if (currentToolbar == null)
            {
                var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
                currentToolbar = toolbars.Length > 0 ? (ScriptableObject)toolbars[0] : null;

                if (currentToolbar != null)
                {
                    var root = viewVisualTree.GetValue(currentToolbar, null);
                    var children = (root as UnityEngine.UIElements.VisualElement).Children();

                    foreach (var child in children)
                    {
                        if (child.GetType().Name == "IMGUIContainer")
                        {
                            var handler = (Action)imguiContainerOnGui.GetValue(child);
                            handler += OnGUI;
                            imguiContainerOnGui.SetValue(child, handler);
                            break;
                        }
                    }
                }
            }
        }

        static void OnGUI()
        {
            OnToolbarGUI?.Invoke();
        }
    }
}
