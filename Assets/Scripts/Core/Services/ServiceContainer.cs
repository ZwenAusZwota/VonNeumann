using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Service Container für Dependency Injection.
/// Ersetzt die vielen Singleton-Pattern durch ein zentrales Service-Management.
/// </summary>
public class ServiceContainer : MonoBehaviour
{
    private static ServiceContainer _instance;
    private Dictionary<Type, object> _services = new();
    private Dictionary<Type, Func<object>> _factories = new();

    public static ServiceContainer Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("ServiceContainer");
                _instance = go.AddComponent<ServiceContainer>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ==================== Service Registration ====================

    /// <summary>Service als Singleton registrieren</summary>
    public void RegisterSingleton<T>(T service) where T : class
    {
        _services[typeof(T)] = service;
        Debug.Log($"[ServiceContainer] Registered singleton: {typeof(T).Name}");
    }

    /// <summary>Service als Factory registrieren</summary>
    public void RegisterFactory<T>(Func<T> factory) where T : class
    {
        _factories[typeof(T)] = () => factory();
        Debug.Log($"[ServiceContainer] Registered factory: {typeof(T).Name}");
    }

    /// <summary>Service als Transient registrieren (neue Instanz bei jedem Aufruf)</summary>
    public void RegisterTransient<T>(Func<T> factory) where T : class
    {
        _factories[typeof(T)] = () => factory();
        Debug.Log($"[ServiceContainer] Registered transient: {typeof(T).Name}");
    }

    // ==================== Service Resolution ====================

    /// <summary>Service abrufen</summary>
    public T Get<T>() where T : class
    {
        var type = typeof(T);
        
        // 1. Prüfe Singleton-Services
        if (_services.TryGetValue(type, out var service))
        {
            return service as T;
        }
        
        // 2. Prüfe Factory-Services
        if (_factories.TryGetValue(type, out var factory))
        {
            var instance = factory() as T;
            Debug.Log($"[ServiceContainer] Created instance: {type.Name}");
            return instance;
        }
        
        // 3. Versuche automatische Registrierung für MonoBehaviour-Services
        if (typeof(MonoBehaviour).IsAssignableFrom(type))
        {
            var existing = FindFirstObjectByType(type) as T;
            if (existing != null)
            {
                RegisterSingleton(existing);
                return existing;
            }
        }
        
        Debug.LogWarning($"[ServiceContainer] Service not found: {type.Name}");
        return null;
    }

    /// <summary>Service abrufen oder erstellen</summary>
    public T GetOrCreate<T>() where T : class
    {
        var service = Get<T>();
        if (service == null)
        {
            // Versuche automatische Erstellung für MonoBehaviour-Services
            if (typeof(MonoBehaviour).IsAssignableFrom(typeof(T)))
            {
                var go = new GameObject(typeof(T).Name);
                service = go.AddComponent(typeof(T)) as T;
                RegisterSingleton(service);
                Debug.Log($"[ServiceContainer] Auto-created: {typeof(T).Name}");
            }
        }
        return service;
    }

    /// <summary>Prüfen ob Service registriert ist</summary>
    public bool IsRegistered<T>() where T : class
    {
        var type = typeof(T);
        return _services.ContainsKey(type) || _factories.ContainsKey(type);
    }

    // ==================== Service Management ====================

    /// <summary>Service entfernen</summary>
    public void Unregister<T>() where T : class
    {
        var type = typeof(T);
        _services.Remove(type);
        _factories.Remove(type);
        Debug.Log($"[ServiceContainer] Unregistered: {type.Name}");
    }

    /// <summary>Alle Services löschen</summary>
    public void ClearAll()
    {
        _services.Clear();
        _factories.Clear();
        Debug.Log("[ServiceContainer] Cleared all services");
    }

    /// <summary>Alle registrierten Services auflisten</summary>
    public void ListServices()
    {
        Debug.Log("=== Registered Services ===");
        foreach (var kvp in _services)
        {
            Debug.Log($"Singleton: {kvp.Key.Name}");
        }
        foreach (var kvp in _factories)
        {
            Debug.Log($"Factory: {kvp.Key.Name}");
        }
    }
}

// ==================== Service Extensions ====================

/// <summary>Erweiterungsmethoden für einfachere Service-Nutzung</summary>
public static class ServiceExtensions
{
    /// <summary>Service direkt abrufen (Kurzform)</summary>
    public static T GetService<T>(this MonoBehaviour caller) where T : class
    {
        return ServiceContainer.Instance.Get<T>();
    }

    /// <summary>Service abrufen oder erstellen (Kurzform)</summary>
    public static T GetOrCreateService<T>(this MonoBehaviour caller) where T : class
    {
        return ServiceContainer.Instance.GetOrCreate<T>();
    }
}
