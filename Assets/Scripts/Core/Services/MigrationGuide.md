# Migration Guide: Singleton zu Service Container

## 🔄 Schritt-für-Schritt Migration

### 1. **Service Registration**
```csharp
// Vorher (Singleton)
public class MyService : MonoBehaviour
{
    public static MyService I { get; private set; }
    
    private void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
    }
}

// Nachher (Service Container)
public class MyService : MonoBehaviour
{
    private void Awake()
    {
        ServiceContainer.Instance.RegisterSingleton<MyService>(this);
    }
}
```

### 2. **Service Access**
```csharp
// Vorher (Singleton)
var service = MyService.I;
if (service == null) return;

// Nachher (Service Container)
var service = ServiceContainer.Instance.Get<MyService>();
if (service == null) return;

// Oder mit Extension Method
var service = this.GetService<MyService>();
```

### 3. **Service Factory**
```csharp
// Für Services, die bei Bedarf erstellt werden
ServiceContainer.Instance.RegisterFactory<MyService>(() => 
{
    var go = new GameObject("MyService");
    return go.AddComponent<MyService>();
});
```

## 📋 Migration Checkliste

### **Phase 1: Service Container Setup**
- [x] ServiceContainer.cs erstellt
- [x] ServiceBootstrap.cs erstellt
- [x] ServiceExtensions.cs erstellt

### **Phase 2: Manager Services**
- [ ] HUDBindingService → HUDBindingServiceV2
- [ ] InputRouter → Service Container
- [ ] SceneRouter → Service Container
- [ ] SaveSystem → Service Container
- [ ] AssetProvider → Service Container

### **Phase 3: World Services**
- [x] WorldRoot → Service Container
- [x] PlanetRegistry → Service Container
- [x] AsteroidPool → Service Container
- [x] HubRegistry → Service Container
- [x] WorldRegistry → Service Container
- [x] EntityFactory → Service Container

### **Phase 4: Gameplay Services**
- [ ] MiningTaskManager → Service Container
- [ ] ProbeController → Service Container
- [ ] FabricatorController → Service Container

### **Phase 5: Event System Integration**
- [ ] HUDMessageBus → GameEvents
- [ ] InventoryController Events → GameEvents
- [ ] FabricatorController Events → GameEvents
- [ ] Scanner Events → GameEvents

## 🎯 Vorteile der Migration

### **Vorher (Singleton)**
```csharp
// Problematisch:
public class MyClass : MonoBehaviour
{
    void Start()
    {
        if (HUDBindingService.I == null)
        {
            Debug.LogError("Service not found!");
            return;
        }
        HUDBindingService.I.DoSomething();
    }
}
```

### **Nachher (Service Container)**
```csharp
// Sauber:
public class MyClass : MonoBehaviour
{
    void Start()
    {
        var service = this.GetService<HUDBindingService>();
        if (service == null)
        {
            Debug.LogError("Service not found!");
            return;
        }
        service.DoSomething();
    }
}
```

## 🔧 Automatische Migration

### **ServiceBootstrap.cs** registriert automatisch:
- Alle Manager-Services
- Alle World-Services  
- Alle Gameplay-Services

### **Backward Compatibility**
```csharp
// Alte Singleton-Zugriffe funktionieren weiterhin
[Obsolete("Use ServiceContainer.Instance.Get<MyService>() instead")]
public static MyService I => ServiceContainer.Instance.Get<MyService>();
```

## 📊 Performance-Vergleich

| Aspekt | Singleton | Service Container |
|--------|-----------|-------------------|
| **Initialisierung** | Awake() | On-Demand |
| **Memory** | Immer geladen | Lazy Loading |
| **Testbarkeit** | Schwer | Einfach |
| **Flexibilität** | Niedrig | Hoch |
| **Wartbarkeit** | Mittel | Hoch |

## 🚀 Nächste Schritte

1. **ServiceBootstrap** in Bootstrap-Szene hinzufügen
2. **Alte Singleton-Referenzen** durch Service Container ersetzen
3. **Event-System** auf GameEvents umstellen
4. **Tests** für Service Container schreiben
5. **Dokumentation** aktualisieren
