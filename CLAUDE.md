# WinTAK Meshtastic Plugin

## Project
C# WinTAK plugin (.dll) providing native Meshtastic mesh network integration.
Replaces standalone Python gateway with a single DLL. No external services.
Full requirements: docs/requirements.docx and docs/requirements-supplement.docx

## Tech Stack
- Language: C# targeting WinTAK's .NET framework (see docs/sdk-findings/)
- Protobuf: Google.Protobuf NuGet, classes generated from proto/meshtastic/*.proto
- UI: WPF (WinTAK plugin panels)
- Transport: TCP to Meshtastic node (System.Net.Sockets)
- Build: dotnet build / MSBuild

## Commands
- `dotnet build src/` : Build the plugin
- `dotnet test tests/` : Run all tests
- `dotnet test tests/ --filter "FullyQualifiedName~HandlerName"` : Run single test class
- `protoc --csharp_out=proto/generated proto/meshtastic/*.proto` : Regenerate protobuf classes

## Architecture
- `src/Plugin/` : Entry point. Implements WinTAK IPlugin. Lifecycle: OnLoad, OnStart, OnStop.
- `src/Connection/` : MeshtasticTcpClient. Async read loop on background thread. Auto-reconnect (default 15s, configurable 5–60s).
- `src/Handlers/` : One class per portnum implementing IPacketHandler. See Portnum Handler Pattern below.
- `src/CoT/` : CoT XML builder utilities. IMPORTANT: All CoT must validate against TAK 5.x schema.
- `src/Models/` : NodeState keyed by (connectionId, meshNodeId) for future multi-node support. ChannelState, TelemetryData, NeighborInfo.
- `src/UI/` : WPF UserControls for settings panel, chat panel, node detail view, topology toggle.
- `src/Topology/` : Neighbor graph, SNR tracking, overlay line generation.
- `proto/generated/` : NEVER hand-edit. Regenerate with protoc command above.
- `tests/` : Mirrors src/ structure. Every handler needs unit tests.

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
- Protobuf unknown fields: preserve them, don't fail. Log unknown portnums at Debug level.
- Channel index 0 is always the default outbound channel
- NodeState must be keyed `(connectionId, nodeId)` not just `nodeId` — future multi-node support
- `proto/generated/` files are auto-generated — do not hand-edit
- TEL-02 environment sensors (gas resistance, IAQ) only available on BME680/688 — hide missing fields, don't show N/A
- No known C# unishox2 implementation — ATAK compressed messages may need P/Invoke wrapper or graceful discard
- Default Meshtastic TCP port: 4403
- Max outbound text message length: 228 bytes (Meshtastic limit)
