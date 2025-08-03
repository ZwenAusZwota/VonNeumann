using UnityEngine;

/// <summary>
/// Enthält Material-Vorrat und kann Einheiten abgeben.
/// Skaliert dabei physisch, Volumen ∝ r³.
/// </summary>
[RequireComponent(typeof(Transform))]
public class MineableAsteroid : MonoBehaviour
{
    public string materialId = "Iron";
    public float startUnits = 1_000f;     // Gesamtvorrat beim Spawnen

    public float UnitsRemaining { get; private set; }

    Vector3 startScale;

    void Awake()
    {
        UnitsRemaining = startUnits;
        startScale = transform.localScale;
    }

    /// <summary>
    /// Liefert 0 … requested Einheiten, je nachdem was noch übrig ist.
    /// </summary>
    public float RemoveUnits(float requested)
    {
        float granted = Mathf.Min(requested, UnitsRemaining);
        UnitsRemaining -= granted;

        // Skaliere Radius = ∛(Volumen-Faktor)
        float frac = UnitsRemaining / startUnits;
        transform.localScale = startScale * Mathf.Pow(frac, 1f / 3f);

        if (UnitsRemaining <= 0f)
            Destroy(gameObject);

        return granted;
    }
}
