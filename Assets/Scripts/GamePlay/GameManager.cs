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
    public HUDControllerModular hud; // Geändert zu HUDControllerModular
    //public PlanetIndicatorManager indicatorManager;

    [Header("Spawn Settings")]
    [Tooltip("Faktor *Planet-Radius* für Spawn-Distanz der Sonde")]
    public float spawnRadiusFactor = 10f;

    readonly List<SystemObject> _systemObjects = new();
    public IReadOnlyList<SystemObject> SystemObjects => _systemObjects;

    //readonly List<SystemObject> _scanObjects = new();
    //public IReadOnlyList<SystemObject> ScanObjects => _scanObjects;

    /* ───────────────────────── Alte Liste rein für Planeten‑UI ──────────── */
    readonly List<PlanetDto> _planets = new();
    public IReadOnlyList<PlanetDto> Planets => _planets;
    readonly List<AsteroidBeltDto> _belts = new();
    public IReadOnlyList<AsteroidBeltDto> Belts => _belts;

    Transform _probeTf;

    /*──────────────────────────── Unity Lifecycle */
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        connector.RegisterInitListener(OnInit);
        //connector.OnMineResult += hud.HandleMineResult;
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

        // Pool vor Planeten-Erstellung initialisieren
        if (!AsteroidPool.Instance)
        {
            GameObject poolGO = new GameObject("AsteroidPool");
            poolGO.AddComponent<AsteroidPool>();
        }

        /* --------------------------------------------------------------
         * 1) Welt‑Payload filtern – Planeten & Gürtel trennen
         * -------------------------------------------------------------- */
        foreach (var body in payload.planets)
        {
            //if (body.object_type == "planet")
                _planets.Add(body);
        }

        /* 2) Planeten nach Abstand sortieren */
        
        _planets.Sort((a, b) => a.star_distance_km.CompareTo(b.star_distance_km));

        /* 3) Planeten anzeigenamen vergeben, Objekte erzeugen, registrieren */

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

        /* --------------------------------------------------------------
         * 4) Welt‑Payload filtern – Planeten & Gürtel trennen
         * -------------------------------------------------------------- */
        foreach (var body in payload.belts)
        {
            //if (body.object_type == "planet")
            _belts.Add(body);
        }

        /* 5) Planeten nach Abstand sortieren */

        _belts.Sort((a, b) => a.star_distance_km.CompareTo(b.star_distance_km));

        roman = 1;
        /* 6) Gürtel anzeigenamen vergeben, Objekte erzeugen, registrieren */
        foreach (var b in _belts)
        {
            b.displayName = $"{payload.star.name}-Belt {Roman(roman++)}";
            GameObject beltGO = planetGenerator.CreateAsteroidBelt(b);
            _systemObjects.Add(new SystemObject
            {
                Kind = SystemObject.ObjectKind.AsteroidBelt,
                Id = b.id,
                Name = b.displayName,
                DisplayName = b.displayName,
                Dto = b,
                GameObject = beltGO
            });
        }


        /* 6) HUD‑ & Indicator‑Aufbau (verwendet weiterhin die Planetenliste) */
        //indicatorManager.BuildIndicators(_planets);
        hud.HandleInit(payload);
        hud.SetSystemObjects(_systemObjects);

        /* 7) Sonde spawnen, Kamera & HUD verbinden */
        _probeTf = SpawnProbeNearObject(_planets[3]);
        //_probeTf = SpawnProbeNearObject(_planets[Random.Range(0, _planets.Count)]);
        //_probeTf = SpawnProbeNearObject((ObjectDto)_systemObjects[_systemObjects.Count-1].Dto);

        cameraController.target = _probeTf;
        cameraController.ResetToDefaultView();
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

    Transform SpawnProbeNearObject(ObjectDto _object)
    {
        float unitsPerKm = 1f / PlanetScale.KM_PER_UNIT;
        Vector3 objectPos = _object.position.ToVector3(unitsPerKm);
        float radiusUnits = _object.radius_km * unitsPerKm;
        float safeDist = radiusUnits * spawnRadiusFactor;

        Vector3 spawnPos = objectPos + Random.onUnitSphere * safeDist;
        var probe = Instantiate(probePrefab, spawnPos, Quaternion.identity);
        return probe.transform;
    }

    public void ExitGame()
    {
        // Hier können Sie Logik hinzufügen, um das Spiel zu beenden
        //Debug.Log("Exiting game...");
        Application.Quit();
    }

}