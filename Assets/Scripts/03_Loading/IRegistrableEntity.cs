using UnityEngine;

public interface IRegistrableEntity
{
    SerializedGuid Guid { get; }
    string TypeId { get; } // z.B. Addressables-Key für Respawn
    HUDPayload GetHUDPayload(); // Daten fürs HUD
    EntitySaveData Capture();
    void Restore(EntitySaveData data);
}

