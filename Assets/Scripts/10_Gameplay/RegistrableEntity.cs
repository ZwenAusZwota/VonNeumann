using System;
using UnityEngine;

/// <summary>
/// Basis-Implementierung für registrierbare Entities.
/// - Stabile GUID über GuidProvider
/// - TypeId für Addressables/Factory
/// - Optionale Zustands-Provider-Komponente (IRegistrableStateProvider)
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(GuidProvider))]
public class RegistrableEntity : MonoBehaviour, IRegistrableEntity
{
    [Header("Identity")]
    [SerializeField] private GuidProvider guidProvider;

    [Tooltip("Addressables-/Factory-Key zum Respawn dieser Entität.")]
    [SerializeField] private string typeId;

    [Header("Behaviour")]
    [Tooltip("Beim Aktivieren automatisch in WorldRegistry registrieren.")]
    [SerializeField] private bool autoRegister = true;

    [Tooltip("Beim Zerstören automatisch aus WorldRegistry entfernen.")]
    [SerializeField] private bool autoUnregisterOnDestroy = true;

    [Header("State (optional)")]
    [Tooltip("Optionales Component, das Capture/Restore-State bereitstellt.")]
    [SerializeField] private MonoBehaviour stateProviderBehaviour; // sollte IRegistrableStateProvider implementieren

    private IRegistrableStateProvider _stateProvider;

    // ------------- IRegistrableEntity -------------
    public SerializedGuid Guid => guidProvider != null ? guidProvider.Id : default;
    public string TypeId => typeId;

    public virtual HUDPayload GetHUDPayload()
    {
        return new HUDPayload
        {
            Name = name,
            Position = transform.position
        };
    }

    public virtual EntitySaveData Capture()
    {
        string stateJson = null;
        if (_stateProvider != null)
        {
            try { stateJson = _stateProvider.CaptureStateJson(gameObject); }
            catch (Exception ex) { Debug.LogError($"[RegistrableEntity] CaptureStateJson error on {name}: {ex}"); }
        }

        return new EntitySaveData
        {
            Guid = Guid.Value.ToString(),
            TypeId = TypeId,
            Pos = transform.position,
            Rot = transform.rotation,
            StateJson = stateJson
        };
    }

    public virtual void Restore(EntitySaveData data)
    {
        transform.SetPositionAndRotation(data.Pos, data.Rot);

        if (_stateProvider != null && !string.IsNullOrEmpty(data.StateJson))
        {
            try { _stateProvider.RestoreStateJson(gameObject, data.StateJson); }
            catch (Exception ex) { Debug.LogError($"[RegistrableEntity] RestoreStateJson error on {name}: {ex}"); }
        }

        // Nach Wiederherstellung HUD informieren
        if (WorldRegistry.I != null) WorldRegistry.I.NotifyChanged(Guid);
    }

    // ------------- Unity lifecycle -------------

    protected virtual void Reset()
    {
        if (!guidProvider) guidProvider = GetComponent<GuidProvider>();
        if (!guidProvider) guidProvider = gameObject.AddComponent<GuidProvider>();
        if (string.IsNullOrWhiteSpace(typeId)) typeId = gameObject.name; // Fallback
    }

    protected virtual void OnValidate()
    {
        if (!guidProvider) guidProvider = GetComponent<GuidProvider>();
        if (stateProviderBehaviour != null && !(stateProviderBehaviour is IRegistrableStateProvider))
        {
            Debug.LogWarning($"[RegistrableEntity] Assigned stateProviderBehaviour on {name} does not implement IRegistrableStateProvider.");
            stateProviderBehaviour = null;
        }
    }

    protected virtual void Awake()
    {
        if (!guidProvider) guidProvider = GetComponent<GuidProvider>();
        _stateProvider = stateProviderBehaviour as IRegistrableStateProvider;

        // Falls nichts zugewiesen ist, optional automatisch nach IRegistrableStateProvider im Objekt suchen
        if (_stateProvider == null)
            _stateProvider = GetComponent<IRegistrableStateProvider>();
    }

    protected virtual void OnEnable()
    {
        if (autoRegister && WorldRegistry.I != null)
            WorldRegistry.I.Register(this);
    }

    protected virtual void OnDisable()
    {
        // Nichts – das Objekt könnte nur temporär disabled sein
    }

    protected virtual void OnDestroy()
    {
        if (autoUnregisterOnDestroy && WorldRegistry.I != null)
            WorldRegistry.I.Unregister(this);
    }

    // ------------- Convenience -------------

    /// <summary>Manuelles Setzen der TypeId (z. B. bei Instanziierung per Factory).</summary>
    public void SetTypeId(string newTypeId) => typeId = newTypeId;

    /// <summary>Manuelles Registrieren (falls autoRegister aus ist).</summary>
    public void RegisterNow()
    {
        if (WorldRegistry.I != null) WorldRegistry.I.Register(this);
    }

    /// <summary>Manuelles Deregistrieren (falls autoUnregister aus ist).</summary>
    public void UnregisterNow()
    {
        if (WorldRegistry.I != null) WorldRegistry.I.Unregister(this);
    }
}

/// <summary>
/// Optionales Interface, das ein separates State-Component implementieren kann.
/// So können spezialisierte Entitäten ihren Zustand selbst in/aus JSON serialisieren.
/// </summary>
public interface IRegistrableStateProvider
{
    /// <summary>Return JSON representing this entity's state.</summary>
    string CaptureStateJson(GameObject go);

    /// <summary>Restore from the given JSON into this entity's components.</summary>
    void RestoreStateJson(GameObject go, string json);
}
