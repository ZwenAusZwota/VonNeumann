// Assets/Scripts/World/PooledAsteroid.cs
using UnityEngine;

/// <summary>
/// Komponente für gepoolte Asteroiden
/// </summary>
public class PooledAsteroid : MonoBehaviour
{
    public AsteroidPool Pool { get; set; }

    private Renderer[] renderers;
    private Collider[] colliders;
    private MineableAsteroid mineableComponent;
    private bool isVisible = true;

    void Awake()
    {
        // Cache components
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();
        mineableComponent = GetComponent<MineableAsteroid>();

        // Subscribe to mining events
        if (mineableComponent != null)
        {
            mineableComponent.OnFullyMined += HandleFullyMined;
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        if (mineableComponent != null)
        {
            mineableComponent.OnFullyMined -= HandleFullyMined;
        }
    }

    /// <summary>
    /// Reset asteroid to initial state for reuse
    /// </summary>
    public void ResetAsteroid()
    {
        // Reset mineable component
        if (mineableComponent != null)
        {
            mineableComponent.ResetToStartValues();
        }

        // Reset visibility
        SetVisible(true);

        // Reset any other components as needed
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;
    }

    /// <summary>
    /// Set visibility of asteroid
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (isVisible == visible) return;

        isVisible = visible;

        // Toggle renderers
        foreach (var renderer in renderers)
        {
            if (renderer != null)
                renderer.enabled = visible;
        }

        // Toggle colliders (optional - keep physics even when invisible?)
        foreach (var collider in colliders)
        {
            if (collider != null)
                collider.enabled = visible;
        }
    }

    /// <summary>
    /// Handle when asteroid is fully mined
    /// </summary>
    void HandleFullyMined()
    {
        // Return to pool after brief delay
        Invoke(nameof(ReturnToPool), 1f);
    }

    void ReturnToPool()
    {
        Pool?.ReturnAsteroid(this);
    }

    /// <summary>
    /// Force return to pool (for LOD system)
    /// </summary>
    public void ForceReturnToPool()
    {
        CancelInvoke(nameof(ReturnToPool)); // Cancel any pending return
        Pool?.ReturnAsteroid(this);
    }

    // Getter for current state
    public bool IsVisible => isVisible;
    public bool IsFullyMined => mineableComponent?.IsFullyMined ?? false;
    public float RemainingUnits => mineableComponent?.UnitsRemaining ?? 0f;
}