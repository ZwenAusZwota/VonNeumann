using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Runtime Links")]
    public ServerConnector connector;
    public PlanetGenerator planetGenerator;
    public StarGenerator starGenerator;
    public GameObject probePrefab;
    public CameraController cameraController;
    public HUDController hud;
    public PlanetIndicatorManager indicatorManager;

    [Header("Spawn Settings")]
    [Tooltip("Faktor *Planet-Radius* für Spawn-Distanz der Sonde")]
    public float spawnRadiusFactor = 10f;

    readonly List<SystemObject> _systemObjects = new();
    public IReadOnlyList<SystemObject> SystemObjects => _systemObjects;

    readonly List<SystemObject> _scanObjects = new();
    public IReadOnlyList<SystemObject> ScanObjects => _scanObjects;

    /* ───────────────────────── Alte Liste rein für Planeten‑UI ──────────── */
    readonly List<PlanetDto> _planets = new();
    public IReadOnlyList<PlanetDto> Planets => _planets;

    Transform _probeTf;

    /*──────────────────────────── Unity Lifecycle */
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        connector.RegisterInitListener(OnInit);
        connector.OnMineResult += hud.HandleMineResult;
    }

    /*──────────────────────────── Initialisierung */
    void OnInit(InitPayload payload)
    {
        /* --------------------------------------------------------------
         * 0) Stern erzeugen & SystemObject‑Eintrag anlegen
         * -------------------------------------------------------------- */
        GameObject starGO = starGenerator.CreateStar(payload.star);
        _systemObjects.Add(new SystemObject
        {
            Kind = SystemObject.ObjectKind.Star,
            Id = payload.star.id.ToString(),
            Name = payload.star.name,
            DisplayName = payload.star.name,
            Dto = payload.star,
            GameObject = starGO
        });

        /* --------------------------------------------------------------
         * 1) Welt‑Payload filtern – Planeten & Gürtel trennen
         * -------------------------------------------------------------- */
        foreach (var body in payload.world)
        {
            //if (body.object_type == "planet")
                _planets.Add(body);
        }

        /* 2) Planeten nach Abstand sortieren */
        _planets.Sort((a, b) => SqrDist(a).CompareTo(SqrDist(b)));

        /* 3) Planeten anzeigenamen vergeben, Objekte erzeugen, registrieren */

        // Pool vor Planeten-Erstellung initialisieren
        if (!AsteroidPool.Instance)
        {
            GameObject poolGO = new GameObject("AsteroidPool");
            poolGO.AddComponent<AsteroidPool>();
        }

        int roman = 1;
        foreach (var p in _planets)
        {
            p.displayName = p.object_type == "planet" ? $"{payload.star.name} - {Roman(roman++)}" : $"{payload.star.name}-Belt {Roman(roman++)}";
            GameObject planetGO = planetGenerator.CreatePlanet(p);

            _systemObjects.Add(new SystemObject
            {
                Kind = p.object_type == "planet" ? SystemObject.ObjectKind.Planet : SystemObject.ObjectKind.AsteroidBelt,
                Id = p.id,
                Name = p.displayName,
                DisplayName = p.displayName,
                Dto = p,
                GameObject = planetGO
            });
        }

        /* 5) HUD‑ & Indicator‑Aufbau (verwendet weiterhin die Planetenliste) */
        //indicatorManager.BuildIndicators(_planets);
        hud.HandleInit(payload);
        hud.SetObjects(_systemObjects);
        hud.NearScanClicked += PerformNearScan;
        //hud.planetList.BuildList(_planets);

        /* 6) Sonde spawnen, Kamera & HUD verbinden */
        _probeTf = SpawnProbeNear(_planets[Random.Range(0, _planets.Count)]);
        hud.SetProbe(_probeTf.GetComponent<Rigidbody>());

        cameraController.target = _probeTf;
        cameraController.ResetCamera();
    }

    /*──────────────────────────── Helper */
    static float SqrDist(PlanetDto p) =>
        p.position.x * p.position.x +
        p.position.y * p.position.y +
        p.position.z * p.position.z;

    static string Roman(int n)
    {
        string[] r = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
        return r[(n - 1) % r.Length];
    }

    Transform SpawnProbeNear(PlanetDto planet)
    {
        float unitsPerKm = 1f / PlanetScale.KM_PER_UNIT;
        Vector3 planetPos = planet.position.ToVector3(unitsPerKm);
        float radiusUnits = planet.radius_km * unitsPerKm;
        float safeDist = radiusUnits * spawnRadiusFactor;

        Vector3 spawnPos = planetPos + Random.onUnitSphere * safeDist;
        var probe = Instantiate(probePrefab, spawnPos, Quaternion.identity);
        return probe.transform;
    }

    // GameManager.cs   (innerhalb der Klasse)
    public void PerformNearScan()
    {
        // ---- Konstanten --------------------------------------------------
        const float scanRadiusKm = 75_000_000f;   // 1 ∙ 10⁵ km
        const float mainRangeKm = 1_000_000f; // 1 ∙ 10⁶ km
        float scanRadiusUnits = scanRadiusKm / PlanetScale.KM_PER_UNIT;
        float mainRangeUnits = mainRangeKm / PlanetScale.KM_PER_UNIT;
        float mainRangeSqr = mainRangeUnits * mainRangeUnits;

        Vector3 origin = _probeTf.position;

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

            // Kind-Relation (optional nur als Transform-Elternschaft)
            /*if (nearestMain != null)
            {
                col.transform.SetParent(nearestMain.GameObject.transform, true);
                // nearestMain.Children.Add(newObj);
                // newObj.Parent = nearestMain;
            }*/

            _scanObjects.Add(newObj);
        }

        // ---- 4) UI aktualisieren (duplikatsicher) ------------------------
        if (_scanObjects.Count > 0)
            hud.UpdateScan(_scanObjects);
    }


}
