using UnityEngine;

[CreateAssetMenu(fileName = "New Material", menuName = "SpaceGame/Environment/Material")]
public class MaterialSO : ScriptableObject
{
    public enum MaterialType { solid, liquid, gas }

    [Header("IDs & Anzeige")]
    public string id;              // z.B. "Iron" (muss eindeutig sein)
    public string displayName;     // z.B. "Eisen"
    public Sprite icon;

    [Header("Eigenschaften")]
    [Tooltip("m³ pro Einheit")]
    public float volumePerUnit = 1f;

    [Tooltip("Einheiten/Sekunde bei 1x-Mining-Speed")]
    public float mineRate = 1f;

    [Tooltip("Relative Häufigkeit im Vorkommen (z.B. für Zufallsauswahlen)")]
    public int weight = 1;

    [Tooltip("Art des Materials (fest, flüssig, gasförmig)")]
    public MaterialType type = MaterialType.solid;

    [Header("Material für Rendering")]
    public Material renderMaterial;
}
