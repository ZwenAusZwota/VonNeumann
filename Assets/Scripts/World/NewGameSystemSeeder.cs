// Assets/Scripts/World/NewGameSystemSeeder.cs
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class NewGameSystemSeeder : MonoBehaviour
{
    [Header("World Root (optional)")]
    [Tooltip("Zuweisbares World-GameObject. Wenn gesetzt, wird/bleibt dieses Objekt die WorldRoot-Instanz (DontDestroyOnLoad).")]
    [SerializeField] private GameObject worldRootGO;

    [Header("Asteroid Belt")]
    [Tooltip("Optional: Belt-Template in der Szene (templateOnly = true). Wird zur Laufzeit in den produktiven Belt umgewandelt.")]
    [SerializeField] private AsteroidBelt beltTemplate;
    [Tooltip("Falls bereits ein Belt unter WorldRoot existiert, wird kein weiterer erzeugt.")]
    public bool skipIfBeltExists = true;

    [Header("Refs")]
    public StarGenerator starGenerator;       // in Loading-Szene zuweisen
    public PlanetGenerator planetGenerator;   // in Loading-Szene zuweisen
    public TextAsset hygCsv;                  // Optional via Inspector, sonst aus Resources

    [Header("Random")]
    public int seed = 0;                      // 0 = aus Systemzeit

    [Header("Startobjekte")]
    [Tooltip("Addressables-Key oder -Label der Standardsonde, z. B. 'Probe_Default'.")]
    public string defaultProbeKeyOrLabel = "Probe_Default";
    public bool spawnProbeInBelt = true;

    private async void Start()
    {
        EnsureWorldRoot();
        if (WorldRoot.Instance == null)
        {
            Debug.LogError("[Seeder] Keine WorldRoot verfügbar. Abbruch.");
            return;
        }

        // Wenn bereits etwas existiert, nicht nochmal ein ganzes System bauen:
        if (WorldRoot.Instance.starRoot.childCount > 0 ||
            WorldRoot.Instance.planetsRoot.childCount > 0)
        {
            Debug.Log("[Seeder] Welt existiert bereits – Erzeugung übersprungen.");
            return;
        }

        // HYG laden
        if (hygCsv == null) hygCsv = Resources.Load<TextAsset>("Data/hygdata");
        UnityEngine.Random.InitState(seed == 0 ? Environment.TickCount : seed);

        HubRegistryBootstrap.Ensure();
        await EnsureAssetProviderReady(defaultProbeKeyOrLabel);

        // Stern erzeugen
        var star = PickRandomStar(hygCsv.text);
        var starDto = BuildStarDto(star);
        starGenerator.CreateStar(starDto); // parented in StarGenerator selbst

        // Planeten erzeugen
        await GeneratePlanets(starDto);

        // Belt erzeugen – bevorzugt: Template nutzen, sonst Fallback
        AsteroidBelt belt = await CreateOrAdoptAsteroidBeltAsync(starDto);

        // Optionale Sonde im Belt
        if (spawnProbeInBelt && belt != null && !string.IsNullOrWhiteSpace(defaultProbeKeyOrLabel))
        {
            await SpawnDefaultProbeInBeltAsync(belt, defaultProbeKeyOrLabel, registerInRegistry: true);
        }
    }

    private void EnsureWorldRoot()
    {
        if (WorldRoot.Instance != null) return;

        if (worldRootGO != null)
        {
            var wr = worldRootGO.GetComponent<WorldRoot>();
            if (wr == null) wr = worldRootGO.AddComponent<WorldRoot>();
            if (WorldRoot.Instance == null)
            {
                WorldRoot.Ensure();
                if (WorldRoot.Instance == null)
                {
                    DontDestroyOnLoad(worldRootGO);
                }
            }
        }
        else
        {
            WorldRoot.Ensure();
        }
    }

    private async UniTask EnsureAssetProviderReady(string maybeKeyOrLabel)
    {
        if (AssetProvider.I == null)
        {
            var go = new GameObject("AssetProvider");
            go.AddComponent<AssetProvider>();
        }

        if (!AssetProvider.I.IsInitialized)
            await AssetProvider.I.Initialize(null, null);

        if (!string.IsNullOrWhiteSpace(maybeKeyOrLabel))
            await AssetProvider.I.DownloadDependenciesAsync(new[] { maybeKeyOrLabel }, null);
    }

    // ------------------- Sterne/Planeten -------------------
    HygStarRecord PickRandomStar(string csv)
    {
        var lines = csv.Split('\n');
        var bag = new List<HygStarRecord>(1024);
        for (int i = 1; i < lines.Length; i++)
            if (HygStarRecord.TryParse(lines[i].Trim(), out var rec)) bag.Add(rec);
        return bag[UnityEngine.Random.Range(0, bag.Count)];
    }

    StarDto BuildStarDto(HygStarRecord s)
    {
        return new StarDto
        {
            name = $"{s.proper}",
            spect = s.spect,
            luminosity_solar = Mathf.Max(0.1f, s.lum),
            colorTemperatureK = EstimateTempK(s)
        };
    }

    float EstimateTempK(HygStarRecord s)
    {
        if (s.ci <= -0.4f || s.ci >= 2.0f) return 5772f;
        float t = 4600f * (1f / (0.92f * s.ci + 1.7f) + 1f / (0.92f * s.ci + 0.62f));
        return Mathf.Clamp(t, 2500f, 35000f);
    }

    private async UniTask GeneratePlanets(StarDto star)
    {
        float sqrtL = Mathf.Sqrt(Mathf.Max(0.05f, star.luminosity_solar));
        float hzInnerAU = 0.95f * sqrtL;
        float hzOuterAU = 1.67f * sqrtL;
        float snowLineAU = 2.7f * sqrtL;

        int nPlanets = UnityEngine.Random.Range(3, 9);

        var semiMajorAU = new List<float>();
        float aMin = 0.25f * sqrtL;
        float aMax = 50f;
        for (int i = 0; i < nPlanets; i++)
        {
            float a; int guard = 0;
            do
            {
                a = Mathf.Exp(UnityEngine.Random.Range(Mathf.Log(aMin), Mathf.Log(aMax)));
                guard++;
            } while (TooClose(a, semiMajorAU, 0.3f) && guard < 200);
            semiMajorAU.Add(a);
        }
        semiMajorAU.Sort();

        for (int i = 0; i < semiMajorAU.Count; i++)
        {
            float aAU = semiMajorAU[i];
            var type = ClassifyPlanetType(aAU, hzInnerAU, hzOuterAU, snowLineAU);
            var pDto = MakePlanetDto(star, $"P{i + 1}", aAU, type);
            var go = planetGenerator.CreatePlanet(pDto);
            WorldRoot.Instance.Attach(go.transform, WorldRoot.Category.Planet, worldPos: false);
            int moonCount =
                (type == PlanetType.GasGiant) ? UnityEngine.Random.Range(2, 7)
              : (type == PlanetType.Water || type == PlanetType.Rocky || type == PlanetType.Habitable) ? UnityEngine.Random.Range(0, 3)
              : 0;
            CreateMoons(go.transform, moonCount, type);
        }
    }

    enum PlanetType { Rocky, Water, Habitable, IceGiant, GasGiant }

    PlanetType ClassifyPlanetType(float aAU, float hzIn, float hzOut, float snow)
    {
        if (aAU < 0.5f * hzIn) return PlanetType.Rocky;
        if (aAU >= hzIn && aAU <= hzOut) return (UnityEngine.Random.value < 0.6f) ? PlanetType.Habitable : PlanetType.Water;
        if (aAU > snow * 0.8f && aAU < snow * 1.3f) return PlanetType.IceGiant;
        if (aAU >= snow) return (UnityEngine.Random.value < 0.6f) ? PlanetType.GasGiant : PlanetType.IceGiant;
        return PlanetType.Rocky;
    }

    PlanetDto MakePlanetDto(StarDto star, string name, float aAU, PlanetType type)
    {
        float radiusKm =
            type == PlanetType.Rocky ? UnityEngine.Random.Range(2500f, 7000f) :
            type == PlanetType.Water ? UnityEngine.Random.Range(6000f, 10000f) :
            type == PlanetType.Habitable ? UnityEngine.Random.Range(5500f, 7500f) :
            type == PlanetType.IceGiant ? UnityEngine.Random.Range(15000f, 30000f) :
                                          UnityEngine.Random.Range(40000f, 80000f);

        string atmo =
            (type == PlanetType.Rocky) ? (UnityEngine.Random.value < 0.5f ? "dünn" : "kein") :
            (type == PlanetType.Water) ? "sauerstoffarm" :
            (type == PlanetType.Habitable) ? "stickstoffreich" :
                                             "sauerstoffarm";

        float massSolar = Mathf.Pow(Mathf.Max(0.1f, star.luminosity_solar), 1f / 3.5f);
        float periodYears = Mathf.Sqrt(aAU * aAU * aAU / Mathf.Max(0.1f, massSolar));
        float periodDays = periodYears * 365.25f;

        return new PlanetDto
        {
            name = name,
            displayName = name,
            radius_km = radiusKm,
            star_distance_km = AU2Km(aAU),
            orbital_period_days = periodDays,
            atmosphere = atmo,
            position = new Vec3Dto()
        };
    }

    void CreateMoons(Transform planet, int count, PlanetType type)
    {
        for (int i = 0; i < count; i++)
        {
            float planetRadiusKm = planet.localScale.x * 0.5f * PlanetScale.KM_PER_UNIT / planetGenerator.sizeMultiplier;
            float aMoonKm = planetRadiusKm * UnityEngine.Random.Range(30f, 120f);
            float rMoonKm =
                (type == PlanetType.GasGiant || type == PlanetType.IceGiant) ? UnityEngine.Random.Range(800f, 3000f)
                                                                             : UnityEngine.Random.Range(200f, 1800f);

            var moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            moon.tag = "Moon";
            moon.name = $"{planet.name}-m{i + 1}";
            moon.transform.SetParent(planet, false);
            float theta = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float phi = UnityEngine.Random.Range(-10f, 10f) * Mathf.Deg2Rad;
            Vector3 posKm = new Vector3(
                Mathf.Cos(phi) * Mathf.Cos(theta) * aMoonKm,
                Mathf.Sin(phi) * aMoonKm,
                Mathf.Cos(phi) * Mathf.Sin(theta) * aMoonKm
            );
            moon.transform.localPosition = posKm / PlanetScale.KM_PER_UNIT;
            float visDiameterUnits = (rMoonKm / PlanetScale.KM_PER_UNIT) * 2f * planetGenerator.sizeMultiplier * planetGenerator.multiplier;
            moon.transform.localScale = Vector3.one * Mathf.Max(visDiameterUnits, 0.01f);

            var orbit = moon.AddComponent<OrbitAroundParent>();
            orbit.periodDays = UnityEngine.Random.Range(5f, 60f);
        }
    }

    static bool TooClose(float a, List<float> list, float minDeltaAU)
    {
        foreach (float b in list)
            if (Mathf.Abs(a - b) < minDeltaAU) return true;
        return false;
    }

    static float AU2Km(float au) => au * 149_597_870.7f;

    // ------------------- Belt-Erzeugung -------------------
    private async UniTask<AsteroidBelt> CreateOrAdoptAsteroidBeltAsync(StarDto star)
    {
        // Wenn schon ein Belt existiert, optional nichts mehr erzeugen
        if (skipIfBeltExists && WorldRoot.Instance?.beltsRoot != null && WorldRoot.Instance.beltsRoot.childCount > 0)
        {
            var existing = WorldRoot.Instance.beltsRoot.GetChild(0).GetComponent<AsteroidBelt>();
            if (existing != null) return existing;
        }

        // Wenn ein Template zugewiesen/gefunden wurde: dieses verwenden (und aktivieren)
        AsteroidBelt tpl = beltTemplate;
        if (tpl == null) tpl = FindFirstObjectByType<AsteroidBelt>(FindObjectsInactive.Include);
        if (tpl != null)
        {
            // Template in WorldRoot einhängen und produktiv schalten
            WorldRoot.Instance.Attach(tpl.transform, WorldRoot.Category.Belt, worldPos: false);
            tpl.templateOnly = false; // jetzt selbst spawnen lassen
            return tpl;
        }

        // Fallback: via PlanetGenerator erzeugen (wie bisher)
        float sqrtL = Mathf.Sqrt(Mathf.Max(0.05f, star.luminosity_solar));
        float beltCenAU = 2.5f * sqrtL;
        float beltHalfWidthAU = 0.5f * sqrtL;

        var beltDto = new AsteroidBeltDto
        {
            name = "Main Belt",
            inner_radius_km = AU2Km(Mathf.Max(0.8f * beltCenAU, beltCenAU - beltHalfWidthAU)),
            outer_radius_km = AU2Km(beltCenAU + beltHalfWidthAU)
        };
        var beltGo = planetGenerator.CreateAsteroidBelt(beltDto); // erzeugt neuen Belt
        WorldRoot.Instance.Attach(beltGo.transform, WorldRoot.Category.Belt, worldPos: false);
        return beltGo.GetComponent<AsteroidBelt>();
    }

    // ------------------- Probe-Spawn im Belt -------------------
    private async UniTask<GameObject> SpawnDefaultProbeInBeltAsync(
        AsteroidBelt belt,
        object probeKeyOrLabel,
        bool registerInRegistry = true)
    {
        if (belt == null)
        {
            Debug.LogWarning("SpawnDefaultProbeInBeltAsync: Kein AsteroidBelt vorhanden – Sonde wird nicht gespawnt.");
            return null;
        }

        // KORREKT: aufgelöste (Unity-Units) verwenden – NICHT belt.innerRadius (km/AU)!
        var cfg = belt.ToResolvedConfig(); // liefert *_UU
        float rInnerUU = Mathf.Max(0.1f, cfg.innerRadiusUU);
        float rOuterUU = Mathf.Max(rInnerUU + 0.1f, cfg.outerRadiusUU);
        float r = Mathf.Lerp(rInnerUU, rOuterUU, 0.5f);

        float theta = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float y = UnityEngine.Random.Range(-cfg.beltThicknessUU * 0.5f, cfg.beltThicknessUU * 0.5f);
        Vector3 local = new Vector3(Mathf.Cos(theta) * r, y, Mathf.Sin(theta) * r);
        Vector3 pos = belt.transform.position + local;

        GameObject probe = await AssetProvider.I.InstantiateAsync(probeKeyOrLabel, pos, Quaternion.identity);
        if (probe == null)
        {
            Debug.LogError($"SpawnDefaultProbeInBeltAsync: Konnte Addressable '{probeKeyOrLabel}' nicht instanziieren.");
            return null;
        }

        probe.name = "Standardsonde";
        probe.tag = "Probe";

        Vector3 radial = (pos - belt.transform.position).normalized;
        Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized;
        if (tangent.sqrMagnitude < 1e-6f) tangent = Vector3.forward;
        probe.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);

        var sig = probe.GetComponent<ScanSignature>() ?? probe.AddComponent<ScanSignature>();
        sig.displayNameOverride = "Standardsonde";
        sig.showAsSystemObject = true;

        if (registerInRegistry)
            PlanetRegistry.Instance?.RegisterProbe(probe.transform);

        return probe;
    }
}
