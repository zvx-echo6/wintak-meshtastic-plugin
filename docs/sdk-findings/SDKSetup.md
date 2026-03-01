# SDK Finding: SDKSetup

**Date:** 2026-03-01
**Investigator:** Phase 0 Spike Team
**Status:** CONFIRMED

## 1. Summary

The WinTAK plugin SDK uses MEF (Managed Extensibility Framework) via Prism.Mef for plugin composition, NOT a custom IPlugin interface as initially assumed. Plugins target .NET Framework 4.8.1 (x64 only) and are deployed to `%appdata%\wintak\plugins\`. The SDK is distributed as NuGet packages in `C:\Program Files\WinTAK\NuGet`.

## 2. SDK API / Method Tested

### Entry Point
- **Interface**: `Microsoft.Practices.Prism.Modularity.IModule`
- **Assembly**: Prism.Core (via WinTak-Dependencies)
- **Attribute**: `[ModuleExport(typeof(T), InitializationMode = InitializationMode.WhenAvailable)]`
- **Method**: `void Initialize()` — called during WinTAK startup

### UI Components
- **DockPane**: `WinTak.Framework.Docking.DockPane` — base class for dockable panels
- **Button**: `WinTak.Framework.Tools.Button` — base class for ribbon bar buttons
- **IDockingManager**: `WinTak.Framework.Docking.IDockingManager` — injected via MEF, activates dock panes

### Plugin Metadata
- **Assembly**: WinTak.Framework
- **Attributes**: `[TakSdkVersion]`, `[PluginName]`, `[PluginDescription]`, `[PluginIcon]`

## 3. Code Sample

### Module Entry Point
```csharp
using System.ComponentModel.Composition;
using Microsoft.Practices.Prism.MefExtensions.Modularity;
using Microsoft.Practices.Prism.Modularity;
using Microsoft.Practices.Prism.PubSubEvents;

[ModuleExport(typeof(MeshtasticModule), InitializationMode = InitializationMode.WhenAvailable)]
public class MeshtasticModule : IModule
{
    private readonly IEventAggregator _eventAggregator;

    [ImportingConstructor]
    public MeshtasticModule(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
    }

    public void Initialize()
    {
        // Plugin initialization code
        System.Diagnostics.Debug.WriteLine("[Meshtastic] Plugin initialized");
    }
}
```

### Dockable Panel
```csharp
using System.ComponentModel.Composition;
using WinTak.Framework.Docking;

[DockPane(
    "WinTakMeshtasticPlugin.MeshtasticDockPane",
    typeof(MeshtasticView),
    Caption = "Meshtastic",
    StartupMode = DockPaneStartupMode.Unpinned,
    StartupState = DockPaneState.DockedLeft)]
[Export]
public class MeshtasticDockPane : DockPane
{
    // Use SetProperty() for bindable properties
    private string _status;
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }
}
```

### Ribbon Button
```csharp
using System.ComponentModel.Composition;
using WinTak.Framework.Docking;
using WinTak.Framework.Tools;

[Button(
    typeof(MeshtasticButton),
    "Meshtastic",
    LargeImagePath = "/WinTakMeshtasticPlugin;component/Assets/meshtastic_icon.png",
    SmallImagePath = "/WinTakMeshtasticPlugin;component/Assets/meshtastic_icon_24.png")]
[Export]
public class MeshtasticButton : Button
{
    private readonly IDockingManager _dockingManager;

    [ImportingConstructor]
    public MeshtasticButton(IDockingManager dockingManager)
    {
        _dockingManager = dockingManager;
    }

    protected override void OnClick()
    {
        _dockingManager.ActivateDockPane("WinTakMeshtasticPlugin.MeshtasticDockPane");
    }
}
```

### NuGet Configuration (nuget.config)
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="WinTAK" value="C:\Program Files\WinTAK\NuGet" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

## 4. Findings

### Confirmed Assumptions
- ✅ WinTAK supports third-party plugins via DLL loading
- ✅ Plugins can create dockable panels and ribbon buttons
- ✅ MEF provides dependency injection for WinTAK services (IEventAggregator, IDockingManager)
- ✅ WPF is fully supported for plugin UI

### Differences from Initial Assumptions
- ❌ **No IPlugin interface** — WinTAK uses Prism's IModule, not a custom interface
- ❌ **No CotDispatcher.Dispatch()** — CoT injection mechanism needs further investigation
- ⚠️ **.NET Framework 4.8.1** — Must use net481, NOT net48 or .NET 6+
- ⚠️ **x64 only** — AnyCPU will not work; must explicitly target x64

### Required Workarounds
1. Use `nuget.config` to reference local WinTAK NuGet source
2. Set `GenerateAssemblyInfo=false` and use manual AssemblyInfo.cs for plugin attributes
3. Button images must have Build Action = Resource

## 5. Impacted Requirements

| Requirement | Impact |
|-------------|--------|
| All requirements | SDK architecture confirmed — development can proceed |
| CON-02 | Use DockPane property binding for connection status indicator |
| CHN-02, MSG-01-03 | Chat integration needs separate investigation (ChatGroupAPI spike) |
| TEL-03, NBR-01, NOD-05 | Detail panel uses DockPane — PRIMARY design confirmed |
| NBR-02-04 | Map drawing API needs separate investigation (MapDrawingAPI spike) |
| Settings panel | Use DockPane or separate settings investigation needed |

## 6. Recommendation

**PRIMARY design is viable** for most features:
- **Dockable panels**: DockPane confirmed for telemetry, neighbor info, chat
- **Ribbon integration**: Button class confirmed for Meshtastic toolbar button
- **Event aggregation**: IEventAggregator for decoupled pub/sub messaging

**Pending investigations** (separate spikes needed):
- CoT injection mechanism (CotDispatchAPI spike)
- Chat group integration (ChatGroupAPI spike)
- Map drawing/overlay (MapDrawingAPI spike)
- Settings persistence (SettingsAPI spike)

## 7. CLAUDE.md Updates

The following changes have been applied to CLAUDE.md:
- Updated Tech Stack section with confirmed framework (net481, x64, MEF)
- Added Key WinTAK SDK Namespaces section
- Updated Architecture section with MEF pattern classes
- Added MEF Plugin Pattern code samples
- Added AssemblyInfo Plugin Attributes section
- Updated Gotchas with x64, NuGet source, and Build Action requirements

---

## Cross-References

This finding establishes the foundation for all subsequent SDK spikes. The following investigations are recommended next:

1. **CotDispatchAPI** — How to inject CoT events into WinTAK's event bus
2. **ChatGroupAPI** — Chat group creation and message routing
3. **MapDrawingAPI** — Polyline/shape drawing for topology overlay
4. **SettingsAPI** — Settings panel integration and persistence
