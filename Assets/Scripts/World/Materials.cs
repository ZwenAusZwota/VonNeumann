// Assets/Scripts/Common/Materials.cs
// -------------------------------------------------------------
// Globale Rohstoff-Datenbank mit gewichteter Zufallsverteilung
// -------------------------------------------------------------
// using System.Collections.Generic;
// using System.Linq;
// using UnityEngine;

// /// <summary>Stammdaten eines abbaubaren Rohstoffs.</summary>
// public class MaterialDef
// {
//     public readonly string id;            // eindeutiger Bezeichner
//     public readonly float volumePerUnit; // m³ pro Einheit
//     public readonly float mineRate;      // Einheiten / Sekunde bei 1×-Speed
//     public readonly int weight;        // Wahrscheinlichkeit 1 … ∞ (höher = häufiger)

//     public MaterialDef(string id, float volumePerUnit, float mineRate, int weight = 1)
//     {
//         this.id = id;
//         this.volumePerUnit = volumePerUnit;
//         this.mineRate = mineRate;
//         this.weight = Mathf.Max(1, weight);   // nie <1 zulassen
//     }
// }

// /// <summary>
// /// Zentrale Registry mit bequemen Helfern:
// ///   • MaterialRegistry.Get("Iron")  → komplette Definition
// ///   • MaterialRegistry.GetRandom()  → zufällige Definition (gewichtet)
// ///   • MaterialRegistry.GetRandomId()→ nur die ID
// /// </summary>
// public static class MaterialRegistry
// {
//     // ---------- Rohstoffe eintragen ----------
//     // Gewicht (letzter Parameter) bestimmt die relative Häufigkeit
//     // Id, Name, Volumen, Abbaurate, Gewicht
//     // man könnte diese Materialien alle als scriptableObjects anlegen und dann die ganzen Attribute im Inspector setzen,
//     static readonly Dictionary<string, MaterialDef> table = new()
//     {  // Id, Name, Volumen, Abbaurate, Gewicht
//         { "Iron",   new MaterialDef("Iron",   0.50f, 10f,  7) },
//         { "Nickel", new MaterialDef("Nickel", 0.40f,  8f,  6) },
//         { "Ice",    new MaterialDef("Ice",    1.00f, 20f, 10) },
//         { "Cobalt", new MaterialDef("Cobalt", 0.45f,  7f,  3) },
//         { "Gold",   new MaterialDef("Gold",   0.30f,  4f,  1) },
//         { "Carbon", new MaterialDef("Carbon", 0.73f, 15f,  8) },

//     };

//     /* ---------- vorberechneter Pool für O(1)-Zugriff ---------- */
//     static readonly MaterialDef[] weightedPool;

//     static MaterialRegistry()
//     {
//         // Liste mit Vorkommen entsprechend weight befüllen
//         var list = new List<MaterialDef>();
//         foreach (var def in table.Values)
//             for (int i = 0; i < def.weight; i++)
//                 list.Add(def);

//         weightedPool = list.ToArray();
//     }

//     /* ---------- Öffentliche API ---------- */

//     /// <summary>Rohstoff per ID nachschlagen (wirft KeyNotFound bei Tippfehlern).</summary>
//     public static MaterialDef Get(string id) => table[id];

//     /// <summary>Gewichtete Zufallsauswahl aus allen Materialien.</summary>
//     public static MaterialDef GetRandom() =>
//         weightedPool[Random.Range(0, weightedPool.Length)];

//     /// <summary>Nur die ID des zufällig gewählten Rohstoffs.</summary>
//     public static string GetRandomId() => GetRandom().id;
// }
