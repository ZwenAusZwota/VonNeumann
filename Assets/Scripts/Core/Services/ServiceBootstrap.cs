using UnityEngine;
using SpaceGame.Mining;
using SpaceGame.Core.Managers;

/// <summary>
/// Automatische Service-Registrierung beim Spielstart.
/// Registriert alle wichtigen Services im ServiceContainer.
/// </summary>
public class ServiceBootstrap : MonoBehaviour
{
    [Header("Auto-Registration")]
    [Tooltip("Services automatisch beim Start registrieren")]
    public bool autoRegisterOnStart = true;
    
    [Tooltip("Services automatisch bei Awake registrieren")]
    public bool autoRegisterOnAwake = true;

    private void Awake()
    {
        if (autoRegisterOnAwake)
        {
            RegisterAllServices();
        }
    }

    private void Start()
    {
        if (autoRegisterOnStart)
        {
            RegisterAllServices();
        }
    }

    /// <summary>Alle Services registrieren</summary>
    public void RegisterAllServices()
    {
        var container = ServiceContainer.Instance;
        
        // Manager-Services registrieren
        RegisterManagerServices(container);
        
        // World-Services registrieren
        RegisterWorldServices(container);
        
        // Gameplay-Services registrieren
        RegisterGameplayServices(container);
        
        // UI-Services registrieren
        RegisterUIServices(container);
        
        Debug.Log("[ServiceBootstrap] All services registered successfully");
    }

    private void RegisterManagerServices(ServiceContainer container)
    {
        // HUDBindingService
        var hudService = FindFirstObjectByType<HUDBindingService>();
        if (hudService != null)
        {
            container.RegisterSingleton<HUDBindingService>(hudService);
        }

        // InputRouter
        var inputRouter = FindFirstObjectByType<InputRouter>();
        if (inputRouter != null)
        {
            container.RegisterSingleton<InputRouter>(inputRouter);
        }

        // SceneRouter
        var sceneRouter = FindFirstObjectByType<SceneRouter>();
        if (sceneRouter != null)
        {
            container.RegisterSingleton<SceneRouter>(sceneRouter);
        }

        // SaveSystem
        var saveSystem = FindFirstObjectByType<SaveSystem>();
        if (saveSystem != null)
        {
            container.RegisterSingleton<SaveSystem>(saveSystem);
        }

        // AssetProvider
        var assetProvider = FindFirstObjectByType<AssetProvider>();
        if (assetProvider != null)
        {
            container.RegisterSingleton<AssetProvider>(assetProvider);
        }
    }

    private void RegisterWorldServices(ServiceContainer container)
    {
        // WorldRoot
        var worldRoot = FindFirstObjectByType<WorldRoot>();
        if (worldRoot != null)
        {
            container.RegisterSingleton<WorldRoot>(worldRoot);
        }

        // PlanetRegistry
        var planetRegistry = FindFirstObjectByType<PlanetRegistry>();
        if (planetRegistry != null)
        {
            container.RegisterSingleton<PlanetRegistry>(planetRegistry);
        }

        // AsteroidPool
        var asteroidPool = FindFirstObjectByType<AsteroidPool>();
        if (asteroidPool != null)
        {
            container.RegisterSingleton<AsteroidPool>(asteroidPool);
        }

        // HubRegistry
        var hubRegistry = FindFirstObjectByType<HubRegistry>();
        if (hubRegistry != null)
        {
            container.RegisterSingleton<HubRegistry>(hubRegistry);
        }

        // WorldRegistry
        var worldRegistry = FindFirstObjectByType<WorldRegistry>();
        if (worldRegistry != null)
        {
            container.RegisterSingleton<WorldRegistry>(worldRegistry);
        }

        // EntityFactory
        var entityFactory = FindFirstObjectByType<EntityFactory>();
        if (entityFactory != null)
        {
            container.RegisterSingleton<EntityFactory>(entityFactory);
        }
    }

    private void RegisterGameplayServices(ServiceContainer container)
    {
        // MiningTaskManager
        var miningTaskManager = FindFirstObjectByType<MiningTaskManager>();
        if (miningTaskManager != null)
        {
            container.RegisterSingleton<MiningTaskManager>(miningTaskManager);
        }
    }

    /// <summary>Services manuell registrieren</summary>
    public void RegisterService<T>(T service) where T : class
    {
        ServiceContainer.Instance.RegisterSingleton(service);
    }

    /// <summary>Alle Services auflisten</summary>
    [ContextMenu("List All Services")]
    public void ListAllServices()
    {
        ServiceContainer.Instance.ListServices();
    }

    /// <summary>Alle Services löschen</summary>
    [ContextMenu("Clear All Services")]
    public void ClearAllServices()
    {
        ServiceContainer.Instance.ClearAll();
    }

    private void RegisterUIServices(ServiceContainer container)
    {
        // UIPanelManager
        var uiPanelManager = FindFirstObjectByType<UIPanelManager>();
        if (uiPanelManager != null)
        {
            container.RegisterSingleton<UIPanelManager>(uiPanelManager);
        }
    }
}
