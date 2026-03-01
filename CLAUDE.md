# WinTAK Meshtastic Plugin

## Project
C# WinTAK plugin (.dll) providing native Meshtastic mesh network integration.
Replaces standalone Python gateway with a single DLL. No external services.
Full requirements: docs/wintak-meshtastic-plugin-requirements.docx and docs/wintak-plugin-requirements-supplement.docx

## Tech Stack (Confirmed Phase 0)
- **Framework**: .NET Framework 4.8.1 (net481), x64 platform only
- **Plugin Architecture**: MEF (Managed Extensibility Framework) via Prism.Mef
- **Entry Point**: `IModule` with `[ModuleExport]` attribute, NOT a custom IPlugin interface
- **UI Framework**: WPF with DockPane for dockable panels, Button for ribbon bar
- **NuGet Source**: Local WinTAK packages at `C:\Program Files\WinTAK\NuGet`
- **Key Packages**: WinTak-Dependencies (includes TAK.Kernel, Prism.Core), Prism.Mef
- **Protobuf**: Google.Protobuf NuGet, classes generated from proto/meshtastic/*.proto
- **Transport**: TCP to Meshtastic node (System.Net.Sockets)
- **Deployment**: `%appdata%\wintak\plugins\{PluginName}\`

## Key WinTAK SDK Namespaces
- `WinTak.Framework` — Plugin attributes (TakSdkVersion, PluginName, PluginDescription, PluginIcon)
- `WinTak.Framework.Docking` — DockPane, IDockingManager, DockPaneStartupMode
- `WinTak.Framework.Tools` — Button base class for ribbon bar buttons
- `WinTak.CursorOnTarget.Services` — ICotMessageSender, ICotMessageReceiver (CoT injection)
- `WinTak.Common.Services` — ICommunicationService, ILocationService
- `WinTak.Common.CoT` — CoTMessageArgument, CotEvent
- `Microsoft.Practices.Prism.MefExtensions.Modularity` — ModuleExport attribute
- `Microsoft.Practices.Prism.Modularity` — IModule interface
- `Microsoft.Practices.Prism.PubSubEvents` — IEventAggregator for pub/sub messaging
- `Microsoft.Practices.Prism.Commands` — DelegateCommand for MVVM

## CoT Injection API (Confirmed Phase 0)
Inject mesh node positions into WinTAK map:
```csharp
[ImportingConstructor]
public MyClass(ICotMessageSender cotMessageSender) { _sender = cotMessageSender; }

// Inject CoT event - marker appears on map
var xmlDoc = new XmlDocument();
xmlDoc.LoadXml(cotXmlString);
_sender.Send(xmlDoc);
```

Receive CoT events (for outbound PLI capture):
```csharp
[ImportingConstructor]
public MyClass(ICotMessageReceiver receiver) {
    receiver.MessageReceived += (s, args) => {
        var cotEvent = args.CotEvent;  // Parsed event
        var type = args.Type;          // e.g., "a-f-G-U-C"
    };
}
```

Get operator position (for mesh outbound PLI):
```csharp
[ImportingConstructor]
public MyClass(ILocationService locationService) {
    var pos = locationService.GetGpsPosition();      // GeoPoint with Lat/Lon
    var selfCot = locationService.GetSelfCotEvent(); // Operator's CoT event
    locationService.PositionChanged += OnPositionChanged;
}
```

Team colors in CoT XML (set via `__group` element):
```xml
<detail>
    <contact callsign="NODE-NAME"/>
    <__group name="Cyan" role="Team Member"/>
</detail>
```

## Commands
- `dotnet build src/` : Build the plugin (auto-deploys to %appdata%\wintak\plugins\)
- `dotnet test tests/` : Run all tests
- `dotnet test tests/ --filter "FullyQualifiedName~HandlerName"` : Run single test class
- `protoc --csharp_out=proto/generated proto/meshtastic/meshtastic/*.proto` : Regenerate protobuf classes

## Architecture
- `src/Plugin/MeshtasticModule.cs` : MEF entry point. Implements IModule with [ModuleExport]. Initialize() called at startup.
- `src/Plugin/HandlerRegistry.cs` : Maps portnums to IPacketHandler implementations.
- `src/Connection/MeshtasticTcpClient.cs` : Async TCP client with 4-byte header protocol, auto-reconnect.
- `src/Handlers/` : One class per portnum implementing IPacketHandler.
- `src/CoT/CotBuilder.cs` : CoT XML builder. IMPORTANT: All CoT must validate against TAK 5.x schema.
- `src/Models/` : NodeState keyed by (connectionId, meshNodeId), ChannelState, TelemetryData, NeighborInfo.
- `src/UI/MeshtasticDockPane.cs` : ViewModel for dockable panel, inherits DockPane.
- `src/UI/MeshtasticButton.cs` : Ribbon bar button, inherits Button, uses IDockingManager.
- `src/UI/MeshtasticView.xaml` : WPF UserControl for dock pane content.
- `src/UI/Converters.cs` : XAML value converters.
- `src/Topology/` : Neighbor graph, SNR tracking, overlay line generation.
- `src/Assets/` : Icons (Build Action = Resource for button images).
- `proto/generated/` : NEVER hand-edit. Regenerate with protoc command above.
- `tests/` : Mirrors src/ structure. Every handler needs unit tests.

## MEF Plugin Pattern
```csharp
// Module entry point
[ModuleExport(typeof(MeshtasticModule), InitializationMode = InitializationMode.WhenAvailable)]
public class MeshtasticModule : IModule
{
    [ImportingConstructor]
    public MeshtasticModule(IEventAggregator eventAggregator) { ... }
    public void Initialize() { ... }
}

// Dockable panel
[DockPane("UniqueId", typeof(ViewType), Caption = "Name")]
[Export]
public class MyDockPane : DockPane { ... }

// Ribbon button
[Button(typeof(MyButton), "Display Name", LargeImagePath = "/Assembly;component/path.png")]
[Export]
public class MyButton : Button
{
    [ImportingConstructor]
    public MyButton(IDockingManager dockingManager) { ... }
    protected override void OnClick() { _dockingManager.ActivateDockPane("Id"); }
}
```

## AssemblyInfo Plugin Attributes
Required in `Properties/AssemblyInfo.cs`:
```csharp
[assembly: TakSdkVersion("5.0")]
[assembly: PluginName("Meshtastic")]
[assembly: PluginDescription("...")]
[assembly: PluginIcon("/WinTakMeshtasticPlugin;component/Assets/icon.png")]
```

## CoT Schema Rules
- Default CoT type for mesh nodes: `a-f-G-U-C` (friendly ground civilian)
- Router nodes: `a-f-G-U-C-I` (infrastructure)
- Tracker nodes: `a-f-G-E-S` (equipment/sensor)
- Unknown: `a-f-G` (friendly ground, unspecified)
- Default stale time: 30 minutes for node PLI
- Stale node cleanup timeout: 24 hours (configurable)
- ALWAYS XML-escape shortnames/longnames before embedding in CoT (SEC-07)
- NEVER include PSK values in CoT remarks or logs (SEC-04)
- Channel-to-team-color defaults: Ch0=Cyan, Ch1=Green, Ch2=Yellow, Ch3=Orange, Ch4=Red, Ch5=Purple, Ch6=White, Ch7=Magenta

## Portnum Handler Pattern
When adding a new portnum handler, follow this pattern exactly:
1. Create `src/Handlers/{Name}Handler.cs` implementing `IPacketHandler`
2. Register in `src/Plugin/HandlerRegistry.cs` with the portnum enum value
3. Build CoT XML output using `src/CoT/` builder utilities
4. Create `tests/Handlers/{Name}HandlerTests.cs` with at least:
   - Happy path: valid packet → correct CoT output
   - Missing fields: partial data → graceful degradation (show "Unknown" or hide field)
   - Malformed: bad protobuf → logged at Warning level and discarded, no crash
5. Run `dotnet build src/` and `dotnet test tests/` to verify
6. Update CHANGELOG.md with the new handler

## Security Rules
- Admin channel is excluded from outbound channel selector by default (SEC-02)
- Admin channel visually distinguished in channel list with lock icon (SEC-02)
- Sanitize ALL mesh string data before CoT XML injection (SEC-07)
- Do not log or display PSK values anywhere — UI, logs, CoT remarks (SEC-04)
- Validate incoming node IDs are plausible; reject spoofed self-ID (SEC-08)
- TCP to Meshtastic node is unencrypted. Recommend VPN for remote connections.

## Threading Rules
- NEVER make blocking calls on the WinTAK UI thread
- All Meshtastic TCP I/O runs on background threads
- CoT injection rate: max 1 event/second/node (configurable)
- Use async/await for TCP operations, not synchronous blocking

## Testing
- Every handler must have unit tests before merging
- Integration tests in `tests/Integration/` use a mock TCP server
- Test CoT output by parsing XML with XmlDocument and asserting element values — NOT string matching
- Run `dotnet test tests/` before every commit (enforced by pre-commit hook)

## Gotchas
- WinTAK is x64 only — build must target x64 platform
- NuGet packages from `C:\Program Files\WinTAK\NuGet` — add nuget.config with this source
- Button images must have Build Action = Resource in project
- Protobuf unknown fields: preserve them, don't fail. Log unknown portnums at Debug level.
- Channel index 0 is always the default outbound channel
- NodeState must be keyed `(connectionId, nodeId)` not just `nodeId` — future multi-node support
- `proto/generated/` files are auto-generated — do not hand-edit
- TEL-02 environment sensors (gas resistance, IAQ) only available on BME680/688 — hide missing fields, don't show N/A
- No known C# unishox2 implementation — ATAK compressed messages may need P/Invoke wrapper or graceful discard
- Default Meshtastic TCP port: 4403
- Max outbound text message length: 228 bytes (Meshtastic limit)
