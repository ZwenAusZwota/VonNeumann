using UnityEngine;
using System;
using System.Collections.Generic;

public class ProbeScanner : MonoBehaviour
{

    [Header("Probe Scanner Settings")]
    public float scanRadiusKm = 75_000_000f;   // 1 ∙ 10⁵ km
    public float mainRangeKm = 1_000_000f; // 1 ∙ 10⁶ km

    readonly List<SystemObject> _scanObjects = new();
    public IReadOnlyList<SystemObject> ScanObjects => _scanObjects;

    public event Action<List<SystemObject>> ScanUpdated;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PerformNearScan()
    {
        // ---- Konstanten --------------------------------------------------
        
        float scanRadiusUnits = scanRadiusKm / PlanetScale.KM_PER_UNIT;
        float mainRangeUnits = mainRangeKm / PlanetScale.KM_PER_UNIT;
        float mainRangeSqr = mainRangeUnits * mainRangeUnits;

        Vector3 origin = transform.position;

        _scanObjects.Clear();  // Alte Scans löschen

        // ---- 1) Collider in Reichweite holen -----------------------------
        Collider[] hits = Physics.OverlapSphere(origin, scanRadiusUnits);

        Array.Sort(hits, (a, b) =>
        {
            float dA2 = (a.transform.position - origin).sqrMagnitude;   // Distanz²
            float dB2 = (b.transform.position - origin).sqrMagnitude;
            return dA2.CompareTo(dB2);                                  // kleiner = näher
        });

        foreach (Collider col in hits)
        {
            // Sonde ausschließen
            if (col.CompareTag("Player")) continue;

            // ---- 3) Neues SystemObject anlegen ---------------------------
            var newObj = new SystemObject
            {
                Kind = SystemObject.ObjectKind.ScannedObject,
                Id = col.GetInstanceID().ToString(),
                Name = col.tag,
                DisplayName = col.tag,
                Dto = col,
                GameObject = col.gameObject
            };

            _scanObjects.Add(newObj);
        }

        // ---- 4) UI aktualisieren (duplikatsicher) ------------------------
        if (_scanObjects.Count > 0)
            ScanUpdated?.Invoke(_scanObjects);
        ///hud.UpdateNearScan(_scanObjects);
    }

}
