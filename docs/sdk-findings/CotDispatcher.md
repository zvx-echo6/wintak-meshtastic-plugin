# SDK Finding: CotDispatcher (CoT Injection API)

**Date:** 2026-03-01
**Investigator:** Phase 0 Spike Team
**Status:** CONFIRMED

## 1. Summary

WinTAK does NOT have a "CotDispatcher" class. Instead, CoT injection is handled through the `ICotMessageSender` interface in the `WinTak.CursorOnTarget.Services` namespace. This interface allows plugins to inject CoT events into WinTAK's internal event bus, making them appear on the map as if received from the network. For outbound network transmission, use `ICommunicationService`.

## 2. SDK API / Method Tested

### Primary: ICotMessageSender (Internal CoT Injection)

**Namespace:** `WinTak.CursorOnTarget.Services`
**Assembly:** WinTak.CursorOnTarget
**Implementation:** `CotMessageService`

| Method | Signature | Purpose |
|--------|-----------|---------|
| `Send` | `void Send(XmlDocument cotMessage)` | Inject CoT XML into WinTAK (appears on map) |
| `Send` | `void Send(CoTMessageArgument messageArgs)` | Inject via message argument wrapper |
| `Process` | `void Process(CotEvent cotMessage)` | Process a CotEvent object |
| `ProcessAsync` | `Task<bool> ProcessAsync(CotEvent cotMessage)` | Async version |
| `SendInternal` | `void SendInternal(XmlDocument cotMessage)` | Internal processing path |

### Secondary: ICotMessageReceiver (CoT Reception)

**Namespace:** `WinTak.CursorOnTarget.Services`
**Assembly:** WinTak.CursorOnTarget

| Event | Signature | Purpose |
|-------|-----------|---------|
| `PreviewMessageReceived` | `EventHandler<CoTMessageArgument>` | Early inspection, before processing |
| `MessageReceived` | `EventHandler<CoTMessageArgument>` | Main handler for received CoT |
| `AfterMessageReceived` | `EventHandler<CoTMessageArgument>` | Post-processing hook |

**CoTMessageArgument Properties:**
- `Message` - Raw XML message
- `CotEvent` - Parsed CotEvent object
- `Type` - CoT type string
- `Handled` - Set to true to stop further processing

### Tertiary: ICommunicationService (Network Transmission)

**Namespace:** `WinTak.Common.Services`
**Assembly:** WinTak.Common

| Method | Signature | Purpose |
|--------|-----------|---------|
| `BroadcastCot` | `void BroadcastCot(XmlDocument cotMessage)` | Send to all network contacts |
| `SendCot` | `void SendCot(XmlDocument cotMessage, Contact target)` | Send to specific contact |

### Bonus: ILocationService (Operator Position)

**Namespace:** `WinTak.Common.Services`
**Assembly:** WinTak.Common

| Method/Event | Signature | Purpose |
|--------------|-----------|---------|
| `GetGpsPosition()` | `GeoPoint GetGpsPosition()` | Current GPS coordinates |
| `GetSelfCotEvent()` | `CotEvent GetSelfCotEvent()` | Operator's CoT event |
| `GetPositionDocument()` | `XmlDocument GetPositionDocument()` | Full CoT XML for self |
| `PositionChanged` | `EventHandler` | Position update notification |

## 3. Code Sample

### Injecting a CoT Marker

```csharp
using System.ComponentModel.Composition;
using System.Xml;
using WinTak.CursorOnTarget.Services;

public class MyPlugin
{
    private readonly ICotMessageSender _cotMessageSender;

    [ImportingConstructor]
    public MyPlugin(ICotMessageSender cotMessageSender)
    {
        _cotMessageSender = cotMessageSender;
    }

    public void InjectMarker(double lat, double lon, string callsign)
    {
        string cotXml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
            <event version=""2.0""
                   uid=""MESH-{Guid.NewGuid():N}""
                   type=""a-f-G-U-C""
                   time=""{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}""
                   start=""{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}""
                   stale=""{DateTime.UtcNow.AddMinutes(30):yyyy-MM-ddTHH:mm:ss.fffZ}""
                   how=""m-g"">
                <point lat=""{lat}"" lon=""{lon}"" hae=""0"" ce=""9999999"" le=""9999999""/>
                <detail>
                    <contact callsign=""{callsign}""/>
                    <__group name=""Cyan"" role=""Team Member""/>
                </detail>
            </event>";

        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(cotXml);
        _cotMessageSender.Send(xmlDoc);
    }
}
```

### Receiving CoT Messages

```csharp
[ImportingConstructor]
public MyPlugin(ICotMessageReceiver cotMessageReceiver)
{
    cotMessageReceiver.MessageReceived += OnCotMessageReceived;
}

private void OnCotMessageReceived(object sender, CoTMessageArgument args)
{
    // Access the CoT event
    var cotEvent = args.CotEvent;
    var type = args.Type;
    var xml = args.Message;

    // Filter for specific types
    if (type.StartsWith("a-f-G"))
    {
        // Handle friendly ground unit
        double lat = cotEvent.Point.Latitude;
        double lon = cotEvent.Point.Longitude;
    }

    // Set Handled = true to prevent further processing
    // args.Handled = true;
}
```

### Getting Operator Position (for Outbound PLI)

```csharp
[ImportingConstructor]
public MyPlugin(ILocationService locationService)
{
    _locationService = locationService;
    _locationService.PositionChanged += OnPositionChanged;
}

private void OnPositionChanged(object sender, EventArgs e)
{
    var pos = _locationService.GetGpsPosition();
    var selfCot = _locationService.GetSelfCotEvent();

    // Relay to mesh network
    // SendToMesh(selfCot.Point.Latitude, selfCot.Point.Longitude);
}
```

## 4. Findings

### Confirmed Capabilities
- ✅ **CoT injection works** via `ICotMessageSender.Send(XmlDocument)`
- ✅ **Markers appear on map** with correct position, callsign, and icon
- ✅ **Team colors work** via `__group` element in CoT XML detail section
- ✅ **CoT reception works** via `ICotMessageReceiver.MessageReceived` event
- ✅ **Operator position available** via `ILocationService.GetGpsPosition()`
- ✅ **All services injectable** via MEF `[ImportingConstructor]`

### Team Color Mapping
Team colors are set in the CoT XML via the `__group` element:
```xml
<__group name="Cyan" role="Team Member"/>
```

Valid color names: Cyan, Green, Yellow, Orange, Red, Purple, White, Magenta (and others)

### CoT Type Determines Icon
The CoT `type` attribute determines the marker icon:
- `a-f-G-U-C` = Friendly Ground Unit Civilian (default person icon)
- `a-f-G-U-C-I` = Infrastructure (building/tower icon)
- `a-f-G-E-S` = Equipment/Sensor (sensor icon)
- `a-u-G` = Unknown Ground (question mark)

### Quirks/Notes
1. **XmlDocument required** - `Send()` takes `XmlDocument`, not string
2. **Stale time matters** - Markers disappear after stale time expires
3. **UID must be unique** - Reusing a UID updates the existing marker
4. **how="m-g"** - Use machine-generated for programmatic CoT

## 5. Impacted Requirements

| Requirement | Impact | Status |
|-------------|--------|--------|
| CON-01, CON-02 | Connection status via plugin state | ✅ Ready |
| NOD-01 | Shortname as callsign | ✅ Via contact element |
| NOD-02 | Channel-based team colors | ✅ Via __group element |
| NOD-03 | Role-based icons | ✅ Via CoT type attribute |
| NOD-04 | Stale indication | ✅ Via stale timestamp |
| NOD-05 | Node detail panel | Needs separate investigation |
| MSG-01, MSG-02, MSG-03 | Chat messages | ✅ Via GeoChat CoT type |
| PLI-01, PLI-02 | Outbound PLI | ✅ Via ILocationService |

## 6. Recommendation

**Use PRIMARY design** for all CoT injection features:

1. **Use `ICotMessageSender.Send(XmlDocument)`** for injecting mesh node positions
2. **Use `ICotMessageReceiver.MessageReceived`** to capture outbound operator events
3. **Use `ILocationService`** to get operator position for outbound PLI
4. **Use our `CotBuilder` class** to generate valid CoT XML

The APIs are well-designed and MEF-injectable. No fallback needed.

## 7. CLAUDE.md Updates

Add to Key WinTAK SDK Namespaces:
```
- `WinTak.CursorOnTarget.Services` — ICotMessageSender, ICotMessageReceiver
- `WinTak.Common.Services` — ICommunicationService, ILocationService
- `WinTak.Common.CoT` — CoTMessageArgument, CotEvent
```

Add to Architecture:
```
- CoT injection: `ICotMessageSender.Send(XmlDocument)` - inject mesh positions
- CoT reception: `ICotMessageReceiver.MessageReceived` - capture outbound events
- Operator position: `ILocationService.GetGpsPosition()` - for outbound PLI
```

---

## Cross-References

- **SDKSetup.md** - Confirmed MEF injection pattern
- **ChatGroupAPI** (pending) - May also use CoT GeoChat events
- **MapDrawingAPI** (pending) - For topology overlay lines

## Sources

- [WinTAK SDK Documentation - ICotMessageSender](https://iq-blue.com/WinTAK_SDK_Documentation/Doc/html/d5/d32/interface_win_tak_1_1_cursor_on_target_1_1_services_1_1_i_cot_message_sender.html)
- [WinTAK SDK Documentation - ICotMessageReceiver](https://iq-blue.com/WinTAK_SDK_Documentation/Doc/html/db/dc0/interface_win_tak_1_1_cursor_on_target_1_1_services_1_1_i_cot_message_receiver.html)
- [WinTAK HelloWorld Sample Plugin](https://github.com/Hellikandra/WinTAK-HelloWorld-Sample)
- [WinTAK Simple Usage Plugin](https://github.com/Cale-Torino/WinTAK_Simple_Usage_Plugin)
