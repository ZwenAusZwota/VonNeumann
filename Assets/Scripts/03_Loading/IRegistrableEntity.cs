using UnityEngine;

public interface IRegistrableEntity
{
    SerializedGuid Guid { get; }
    string TypeId { get; } // z.B. Addressables-Key f�r Respawn
    HUDPayload GetHUDPayload(); // Daten f�rs HUD
    EntitySaveData Capture();
    void Restore(EntitySaveData data);
}

