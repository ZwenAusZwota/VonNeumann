# GUI Improvement Guide

## Verbesserte UI-Komponenten

Die UI-Komponenten wurden überarbeitet für ein moderneres und informativeres Design.

---

## 📋 ObjectItemUI (Scan-Panel Items)

### Neue Features:
- **Multi-Label-Layout**: Name, Typ, Distanz separat
- **Type Indicator**: Farbiger Indikator für Objekttyp
- **Material-Namen**: Zeigt Material-Displaynamen statt IDs
- **Hover-Effekte**: Leichte Vergrößerung und Farbwechsel
- **Verbesserte Farben**: Dunkler Hintergrund mit besserer Lesbarkeit

### Prefab-Layout (ObjectListItem.prefab):

```
ObjectListItem (RectTransform)
├─ Background (Image) ← background
│  └─ TypeIndicator (Image) ← typeIndicator
│     ├─ Position: Links, 5px Breite
│     └─ Color: Wird dynamisch gesetzt
├─ Icon (Image) ← icon (optional)
│  ├─ Position: Links neben Text
│  └─ Size: 32x32
├─ ContentPanel (Horizontal Layout Group)
│  ├─ LabelName (TextMeshProUGUI) ← labelName
│  │  ├─ Font Size: 14
│  │  ├─ Color: White
│  │  └─ Alignment: Left
│  ├─ LabelType (TextMeshProUGUI) ← labelType
│  │  ├─ Font Size: 11
│  │  ├─ Color: Gray (0.7, 0.7, 0.7)
│  │  └─ Alignment: Left
│  └─ LabelDistance (TextMeshProUGUI) ← labelDistance
│     ├─ Font Size: 12
│     ├─ Color: Cyan (0.5, 0.9, 1.0)
│     └─ Alignment: Right
```

### Empfohlene Einstellungen:
- **Background Color**: RGB(38, 38, 51, 217) - Dunkles Blau-Grau
- **Padding**: 10px rundum
- **Spacing**: 5px zwischen Elementen
- **Height**: 50-60px
- **Layout**: Horizontal Layout Group mit Flexible Width

---

## 📦 InventoryItemUI (Inventar-Panel Items)

### Neue Features:
- **Kompakte Darstellung**: Amount mit K/M-Suffixen
- **Volumen-Anzeige**: m³ mit automatischer Skalierung
- **Material-Icons**: Unterstützung für Material-Icons
- **Hover-Effekte**: Wie ObjectItemUI

### Prefab-Layout (InventoryListItem.prefab):

```
InventoryListItem (RectTransform)
├─ Background (Image) ← background
├─ Icon (Image) ← icon
│  ├─ Position: Links
│  └─ Size: 40x40
├─ ContentPanel (Vertical Layout Group)
│  ├─ LabelName (TextMeshProUGUI) ← labelName
│  │  ├─ Font Size: 14
│  │  ├─ Color: White
│  │  └─ Font Style: Bold
│  ├─ InfoPanel (Horizontal Layout Group)
│  │  ├─ LabelAmount (TextMeshProUGUI) ← labelAmount
│  │  │  ├─ Font Size: 12
│  │  │  ├─ Color: Yellow (1, 0.9, 0.3)
│  │  │  └─ Alignment: Left
│  │  └─ LabelVolume (TextMeshProUGUI) ← labelVolume
│  │     ├─ Font Size: 10
│  │     ├─ Color: Gray (0.6, 0.6, 0.6)
│  │     └─ Alignment: Right
└─ FillBar (Image) ← fillBar (optional)
   ├─ Position: Bottom, volle Breite
   ├─ Height: 3px
   └─ Color: Gradient von Grün nach Gelb
```

---

## 🎨 Farbpalette

### Haupt-UI-Farben:
- **Background Normal**: RGB(38, 38, 51) / #262633
- **Background Hover**: RGB(64, 89, 128) / #405980
- **Background Selected**: RGB(77, 153, 230) / #4D99E6
- **Text Primary**: RGB(255, 255, 255) / #FFFFFF
- **Text Secondary**: RGB(179, 179, 179) / #B3B3B3
- **Text Accent**: RGB(128, 230, 255) / #80E6FF

### Typ-Farben:
- **Asteroid**: RGB(153, 102, 51) / #996633
- **Planet**: RGB(51, 153, 204) / #3399CC
- **Star**: RGB(255, 230, 77) / #FFE64D
- **Station**: RGB(128, 204, 77) / #80CC4D

---

## 🔧 Unity Editor Setup

### 1. ObjectListItem Prefab erstellen:

1. Rechtsklick im Hierarchy → UI → Panel
2. Benenne es "ObjectListItem"
3. Füge `ObjectItemUI`-Komponente hinzu
4. Erstelle Child-Objekte wie oben beschrieben
5. Verlinke alle Referenzen im Inspector
6. Speichere als Prefab in `Assets/Prefabs/UI/`

### 2. InventoryListItem Prefab erstellen:

1. Rechtsklick im Hierarchy → UI → Panel
2. Benenne es "InventoryListItem"
3. Füge `InventoryItemUI`-Komponente hinzu
4. Erstelle Child-Objekte wie oben beschrieben
5. Verlinke alle Referenzen im Inspector
6. Speichere als Prefab in `Assets/Prefabs/UI/`

### 3. ScrollView Layout-Einstellungen:

Für beide Panel-Typen (Scan & Inventory):

**Content (Parent der Items):**
- Component: Vertical Layout Group
  - Padding: Top=10, Bottom=10, Left=10, Right=10
  - Spacing: 5
  - Child Alignment: Upper Left
  - Child Force Expand: Width=True, Height=False
- Component: Content Size Fitter
  - Horizontal Fit: Unconstrained
  - Vertical Fit: Preferred Size

**Viewport:**
- Component: Mask
- Component: Image (für Clipping)

**Scrollbar (Vertical):**
- Direction: Bottom to Top
- Width: 15px
- Handle Colors: Nutze die Haupt-UI-Farben

---

## 📱 Responsive Design

### Layout Groups verwenden:
- **Horizontal Layout Group**: Für nebeneinander liegende Elemente
- **Vertical Layout Group**: Für untereinander liegende Elemente
- **Grid Layout Group**: Für Inventar-Grids

### Content Size Fitter:
- Aktivieren für automatische Größenanpassung
- "Preferred Size" für dynamische Inhalte

### Anchors richtig setzen:
- Items: Stretch horizontal, Top-aligned
- Labels: Entsprechend dem gewünschten Alignment

---

## 🎯 Best Practices

1. **Konsistente Größen**: Alle List-Items sollten die gleiche Höhe haben
2. **Padding beachten**: Mindestens 5-10px rundum für bessere Lesbarkeit
3. **Kontrast**: Heller Text auf dunklem Hintergrund
4. **Hover-Feedback**: Immer visuelles Feedback bei Interaktion
5. **Icons**: 32x32 oder 40x40 für gute Sichtbarkeit
6. **Spacing**: Mindestens 5px zwischen Items für klare Trennung

---

## 🔄 Migration bestehender Prefabs

Falls bereits Prefabs existieren:

1. Öffne das Prefab im Prefab-Editor
2. Füge die neuen UI-Komponenten hinzu
3. Verlinke die Referenzen
4. Passe das Layout an
5. Speichere das Prefab
6. Die Änderungen werden automatisch überall übernommen

---

## 🐛 Troubleshooting

### Problem: Items werden nicht angezeigt
- ✓ Prüfe ob Content Size Fitter aktiviert ist
- ✓ Prüfe ob Layout Group vorhanden ist
- ✓ Prüfe Anchors und Pivots

### Problem: Layout bricht
- ✓ Deaktiviere/Aktiviere die Layout-Komponenten
- ✓ Prüfe Min/Max-Größen
- ✓ Rebuild Layout manuell: `LayoutRebuilder.ForceRebuildLayoutImmediate()`

### Problem: Hover funktioniert nicht
- ✓ Prüfe ob GraphicRaycaster auf dem Canvas ist
- ✓ Prüfe ob EventSystem in der Szene ist
- ✓ Prüfe Raycast Target auf Images

---

## 📚 Weitere Ressourcen

- Unity UI Best Practices: https://unity.com/how-to/ui-design-and-implementation-unity
- TextMeshPro Documentation: https://docs.unity3d.com/Packages/com.unity.textmeshpro@latest
- Layout Groups: https://docs.unity3d.com/Manual/comp-UIAutoLayout.html


