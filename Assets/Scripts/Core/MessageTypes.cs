// Assets/Scripts/Network/MessageTypes.cs
using System;
using System.Collections.Generic;
using UnityEngine;


    /// <summary>
    /// Wrapper für alle eingehenden/ausgehenden Nachrichten.
    /// </summary>
    [Serializable]
    public class MessageWrapper
    {
        public string method;
        public string args;

        public MessageWrapper(string method, string args)
        {
            this.method = method;
            this.args = args;
        }
    }

    /// ----------  DTOs  ----------

    [Serializable]
    public class Vec3Dto
    {
        public float x, y, z;
        public Vector3 ToVector3(float scale = 1f) => new(x * scale, y * scale, z * scale);
    }

[Serializable]
public class ObjectDto
{
    public string id;
    public string name;
    public string object_type;                 // "planet" | "asteroid_belt"
    public string displayName;
    public Vec3Dto position;
    public float radius_km;
}

[Serializable]
public class StarDto
{
    public string name;
    public string spect;                 // z.B. "G2V"
    public float luminosity_solar = 1f;  // L/L☉
    public float colorTemperatureK = 5772f;
}

[Serializable]
public class PlanetDto
{
    public string object_type = "planet";
    public string name;
    public string displayName;
    public float radius_km;              // physischer Radius
    public float star_distance_km;       // Bahn-Halbachse ~ km
    public float orbital_period_days;    // für OrbitAnimation
    public string atmosphere;            // "kein", "dünn", "sauerstoffarm", "stickstoffreich"
    public Vec3Dto position;             // wird von PlanetGenerator gesetzt/benutzt
}

[Serializable]
public class AsteroidBeltDto
{
    public string name;
    public float inner_radius_km;
    public float outer_radius_km;
}

[Serializable]
    public class InitPayload
    {
        public PlayerDto player;
        public List<PlanetDto> planets;      // Planeten und Gürtel; Typsicherheit via Polymorphie später möglich
        public List<AsteroidBeltDto> belts; // Gürtel
        public StarDto star;          // ← neu
    }

    [Serializable]
    public class PlayerDto
    {
        public string id;
        public string name;
        public float credit;
        public Dictionary<string, float> cargo;
        public string location;
    }

    // Beispiel: Ergebnis einer Mining‑Anfrage
    [Serializable]
    public class MineResult
    {
        public string resource;
        public float amount;
        public float newCredit;
        public int cooldown; 
    }

/* ────────────────────────── Neues Datenmodell ───────────────────────── */

/// <summary>
/// Kapselt sämtliche relevanten Infos zu einem Himmelskörper im aktuellen
/// Sonnensystem. So haben wir DTO und GameObject an einer Stelle.
/// </summary>
public class SystemObject
{
    public enum ObjectKind { Star, Planet, AsteroidBelt, Asteroid, ScannedObject }

    public ObjectKind Kind;        // Typ des Objekts
    public string Id;              // ID aus DTO – bei Star ggf. spect. ID
    public string Name;            // Anzeigename (HUD / UI)
    public string DisplayName;            // Anzeigename (HUD / UI)
    public object Dto;             // StarDto, PlanetDto oder AsteroidBeltDto
    public GameObject GameObject;  // Referenz auf die Instanz in der Szene
    public readonly List<SystemObject> Children = new();
}


