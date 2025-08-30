using UnityEngine;

/// <summary>
/// Vergibt und h�lt eine stabile GUID f�r ein GameObject/Prefab.
/// Wichtig f�r Save/Load und WorldRegistry.
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public class GuidProvider : MonoBehaviour
{
    [SerializeField] private SerializedGuid id;

    public SerializedGuid Id => id;

    private void Reset()
    {
        id.Ensure();
    }

    private void OnValidate()
    {
        // Sorgt daf�r, dass im Editor und zur Laufzeit immer eine GUID vorhanden ist.
        id.Ensure();
    }

    /// <summary>
    /// Erzeugt bewusst eine neue GUID. Vorsicht: Das kann bestehende Savegames invalidieren.
    /// </summary>
    [ContextMenu("Force New GUID")]
    public void ForceNewGuid()
    {
        // SerializedGuid.Ensure() erzeugt nur, wenn leer.
        // Daher setzen wir auf "leer" zur�ck und erzeugen dann neu.
        var empty = new SerializedGuid();
        typeof(SerializedGuid).GetField("_value", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValueDirect(__makeref(empty), null);
        // Zuweisen und neu erzeugen
        id = empty;
        id.Ensure();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}
