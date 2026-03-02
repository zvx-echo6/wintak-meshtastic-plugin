# Changelog

All notable changes to the WinTAK Meshtastic Plugin.

## [Unreleased]

### Added
- Packet reception logging to debug data flow

### Fixed
- (Pending) Verify packet reception from mesh network

---

## [0.1.0] - 2026-03-01

### Added
- **MEF Plugin Architecture**: Module loads correctly in WinTAK 5.4 with green status dot
- **TCP Connection**: Connect to Meshtastic nodes via TCP (default port 4403)
- **DockPane UI**: Dockable panel with connection controls (hostname, port, connect/disconnect)
- **Ribbon Button**: Quick access button to open the Meshtastic panel
- **Handler Registry**: Extensible packet handler system for different portnums
- **Position Handler**: POSITION_APP packets parsed and ready for CoT injection
- **NodeInfo Handler**: NODEINFO_APP packets for mesh node discovery
- **Text Message Handler**: TEXT_MESSAGE_APP with channel routing support
- **Channel Manager**: Track Meshtastic channel configuration
- **Settings Persistence**: Save/load connection settings to JSON
- **Outbound Messaging**: Send text messages to mesh channels
- **CoT Builder**: Generate CoT XML for node positions and chat messages
- **Auto-reconnect**: Configurable reconnection on connection loss

### Fixed
- **MEF Export**: Changed from `[Export(typeof(ITakModule))]` to `[ModuleExport]` for WinTAK compatibility
- **Dual Interface**: Implement both `IModule` (Prism) and `ITakModule` (WinTAK) interfaces
- **Namespace Corrections**: Fixed Prism namespace imports for WinTAK 5.4 SDK:
  - `Prism.Events` (not Microsoft.Practices.Prism.PubSubEvents)
  - `Prism.Commands` (not Microsoft.Practices.Prism.Commands)
  - `Prism.Mef.Modularity` for ModuleExportAttribute
  - `Prism.Modularity` for IModule and InitializationMode
- **Property Injection**: Use `[Import]` properties instead of `[ImportingConstructor]` for MEF services
- **Static Instance Pattern**: Enable cross-component access to MeshtasticModule from UI
- **Direct DLL References**: Reference WinTAK SDK DLLs directly with `<Private>false</Private>`

### Technical Details
- Target: .NET Framework 4.8.1, x64 only
- WinTAK SDK Version: 5.4
- Protobuf: Google.Protobuf with Meshtastic proto definitions
- UI Framework: WPF with Prism MVVM

---

## Phase 0 - SDK Investigation (2026-02-28)

### Confirmed APIs
- **CoT Injection**: `ICotMessageSender.Send(XmlDocument)` injects markers to map
- **CoT Reception**: `ICotMessageReceiver.MessageReceived` captures outbound PLI
- **Location Service**: `ILocationService` for operator position
- **Chat Service**: `IChatService` for chat room management (alternative: GeoChat CoT)
- **Contacts**: Auto-created from CoT with callsign in `<contact>` element
- **Map Drawing**: CoT shapes with type `u-d-f` for polylines (topology overlay)
- **Docking**: `DockPane` base class with `[DockPane]` attribute
- **Ribbon**: `Button` base class with `[Button]` attribute
