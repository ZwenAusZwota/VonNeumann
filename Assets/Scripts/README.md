# VonNeumann-Sonde Bobiverse-Spiel - Script-Architektur

## 📁 Neue Ordnerstruktur

```
Scripts/
├── Core/                    # Kern-Systeme
│   ├── Events/              # Zentrales Event-System
│   │   └── GameEvents.cs   # Alle Spiel-Events
│   ├── Services/           # Service Container
│   │   └── ServiceContainer.cs
│   └── Managers/           # Manager-Scripts (ehemals 00_Manager)
│       ├── HUDBindingService.cs
│       ├── InputRouter.cs
│       ├── SceneRouter.cs
│       └── ...
├── Gameplay/               # Spielmechaniken
│   ├── Probe/             # Sonde-System
│   │   ├── ProbeController.cs
│   │   ├── ProbeAutopilot.cs
│   │   ├── ProbeMiner.cs
│   │   └── InventoryController.cs
│   ├── Mining/            # Mining-System
│   │   ├── MiningTask.cs
│   │   └── MiningTaskManager.cs
│   ├── Crafting/          # Crafting-System
│   │   ├── ProductBlueprint.cs
│   │   ├── FabricatorController.cs
│   │   └── FabricatorBay.cs
│   └── Scanning/          # Scanner-System
│       ├── BaseScannerController.cs
│       ├── NearScannerController.cs
│       └── FarScannerController.cs
├── UI/                    # Alle UI-Scripts
│   ├── Panels/            # UI-Panel-Controller
│   │   ├── InventoryPanelController.cs
│   │   ├── ScanPanelController.cs
│   │   └── NavPanelController.cs
│   └── DraggableHudPanel.cs
├── World/                 # Welt-Generation
│   ├── WorldRoot.cs
│   ├── PlanetGenerator.cs
│   ├── StarGenerator.cs
│   └── AsteroidBelt.cs
└── Utils/                 # Hilfs-Scripts
    └── GuidProvider.cs
```

## 🏗️ Architektur-Prinzipien

### 1. **Zentrales Event-System**
- Alle Events über `GameEvents` statt fragmentierte Systeme
- Typisierte Event-Parameter
- Einheitliche Event-Namen

### 2. **Service Container**
- Ersetzt Singleton-Pattern
- Dependency Injection
- Automatische Service-Erstellung

### 3. **Separation of Concerns**
- **Core**: Kern-Systeme (Events, Services, Manager)
- **Gameplay**: Spielmechaniken (Probe, Mining, Crafting, Scanning)
- **UI**: Alle Benutzeroberflächen
- **World**: Welt-Generation und -Verwaltung
- **Utils**: Hilfs-Scripts

## 🔄 Migration von Singleton zu Service Container

### Vorher (Singleton):
```csharp
public class HUDBindingService : MonoBehaviour
{
    public static HUDBindingService I { get; private set; }
    // ...
}
```

### Nachher (Service Container):
```csharp
// Service registrieren
ServiceContainer.Instance.RegisterSingleton<HUDBindingService>(this);

// Service verwenden
var hudService = ServiceContainer.Instance.Get<HUDBindingService>();
// oder
var hudService = this.GetService<HUDBindingService>();
```

## 📋 Nächste Schritte

1. **Singleton-Migration**: Alle Singleton-Pattern durch Service Container ersetzen
2. **Event-System-Integration**: Bestehende Events auf GameEvents umstellen
3. **Namespace-Bereinigung**: Einheitliche Namespaces pro Ordner
4. **Dokumentation**: XML-Dokumentation für alle öffentlichen APIs

## 🎯 Vorteile der neuen Architektur

- **Wartbarkeit**: Klare Trennung der Verantwortlichkeiten
- **Testbarkeit**: Dependency Injection ermöglicht einfache Tests
- **Erweiterbarkeit**: Service Container ermöglicht flexible Erweiterungen
- **Konsistenz**: Einheitliche Event- und Service-Patterns
- **Performance**: Optimierte Service-Auflösung
