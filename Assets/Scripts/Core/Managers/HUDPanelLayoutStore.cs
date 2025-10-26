// Assets/Scripts/00_Manager/Save/HUDPanelLayoutStore.cs
using System;
using System.IO;
using UnityEngine;

namespace SpaceGame.UI
{
    [Serializable]
    public class DraggableHudPanelState
    {
        public string panelId;
        public float x;
        public float y;
        public bool visible = true;
    }

    /// <summary>
    /// Persistiert HUD-Panel-Layouts als einzelne JSON-Dateien:
    ///   Editor:   Assets/Saves/hud_{panelId}.json
    ///   Build:    persistentDataPath/Saves/hud_{panelId}.json
    /// </summary>
    public static class HUDPanelLayoutStore
    {
        private static string Root
        {
            get
            {
#if UNITY_EDITOR
                string p = Path.Combine(Application.dataPath, "Saves");
#else
                string p = Path.Combine(Application.persistentDataPath, "Saves");
#endif
                if (!Directory.Exists(p)) Directory.CreateDirectory(p);
                return p;
            }
        }

        private static string Sanitize(string id)
        {
            if (string.IsNullOrEmpty(id)) return "_";
            foreach (char c in Path.GetInvalidFileNameChars())
                id = id.Replace(c, '_');
            return id;
        }

        private static string PathFor(string panelId) =>
            Path.Combine(Root, $"hud_{Sanitize(panelId)}.json");

        public static DraggableHudPanelState Load(string panelId)
        {
            try
            {
                var file = PathFor(panelId);
                if (!File.Exists(file)) return null;
                var json = File.ReadAllText(file);
                return JsonUtility.FromJson<DraggableHudPanelState>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HUDPanelLayoutStore] Load failed for '{panelId}': {e}");
                return null;
            }
        }

        public static void Save(string panelId, Vector2 position, bool visible)
        {
            Save(new DraggableHudPanelState
            {
                panelId = panelId,
                x = position.x,
                y = position.y,
                visible = visible
            });
        }

        public static void Save(DraggableHudPanelState state)
        {
            try
            {
                var file = PathFor(state.panelId);
                var json = JsonUtility.ToJson(state, true);
                File.WriteAllText(file, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HUDPanelLayoutStore] Save failed for '{state?.panelId}': {e}");
            }
        }

        public static bool Delete(string panelId)
        {
            try
            {
                var file = PathFor(panelId);
                if (File.Exists(file))
                {
                    File.Delete(file);
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HUDPanelLayoutStore] Delete failed for '{panelId}': {e}");
            }
            return false;
        }
    }
}
