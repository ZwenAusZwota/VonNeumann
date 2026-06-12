using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Verhindert, dass ein zweites EventSystem OnEnable erreicht (Unity-Warnung).
/// Muss auf demselben GameObject wie EventSystem sitzen.
/// </summary>
[DefaultExecutionOrder(-32000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(EventSystem))]
public class EventSystemDuplicateGuard : MonoBehaviour
{
    private static EventSystem _claimed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnSubsystemRegistration()
    {
        ResetClaim();
    }

    private void Awake()
    {
        var eventSystem = GetComponent<EventSystem>();
        if (eventSystem == null)
        {
            Destroy(this);
            return;
        }

        if (_claimed != null && _claimed != eventSystem)
        {
            eventSystem.enabled = false;
            gameObject.SetActive(false);
            DestroyImmediate(gameObject);
            return;
        }

        if (_claimed == null)
            _claimed = eventSystem;
    }

    private void OnDestroy()
    {
        if (_claimed == GetComponent<EventSystem>())
            _claimed = null;
    }

    internal static void RegisterPrimary(EventSystem eventSystem)
    {
        _claimed = eventSystem;
    }

    internal static void ClearClaim(EventSystem eventSystem)
    {
        if (_claimed == eventSystem)
            _claimed = null;
    }

    internal static void ResetClaim()
    {
        _claimed = null;
    }
}
