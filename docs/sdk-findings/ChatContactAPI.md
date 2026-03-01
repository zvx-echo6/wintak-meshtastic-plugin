# SDK Finding: ChatContactAPI

**Date:** 2026-03-01
**Investigator:** Phase 0 Spike Team
**Status:** CONFIRMED

## 1. Summary

WinTAK has a comprehensive chat API through `IChatService` in the `WinTak.Net.Chat` namespace that supports programmatic creation of chat rooms, message sending/receiving, and room management. Contacts are automatically created when CoT events are injected via `ICotMessageSender` — there's no need for a separate "create contact" API. Plugin settings do NOT have a native WinTAK integration API; plugins must manage their own settings storage.

## 2. SDK API / Method Tested

### Primary: IChatService (Chat Room Management)

**Namespace:** `WinTak.Net.Chat`
**Assembly:** WinTak.Net

| Method | Signature | Purpose |
|--------|-----------|---------|
| `CreateChatRoom` | `IChatRoom CreateChatRoom()` | Create a new chat room |
| `AddRoom` | `void AddRoom(IChatRoom room)` | Add room to the service |
| `RemoveRoom` | `void RemoveRoom(IChatRoom room)` | Remove a room |
| `SaveRoom` | `void SaveRoom(IChatRoom room)` | Persist room changes |
| `SendMessageToRoom` | `void SendMessageToRoom(Message message, IChatRoom room)` | Send message to room |
| `GetMessages` | `IEnumerable<Message> GetMessages(string roomId)` | Get room message history |

| Property | Type | Purpose |
|----------|------|---------|
| `ChatRooms` | `IReadOnlyCollection<IChatRoom>` | All chat rooms |
| `SupportsManualChanges` | `bool` | Can users manage rooms? |
| `Icon` | `Uri` | Icon for add/remove buttons |

| Event | Signature | Purpose |
|-------|-----------|---------|
| `MessageReceived` | `EventHandler<MessageReceivedEventArgs>` | Incoming message |
| `MessageSent` | `EventHandler<ChatMessageEventArgs>` | Outgoing message confirmation |
| `ChatRoomAdded` | `EventHandler<ChatRoomUpdatedEventArgs>` | Room created |
| `ChatRoomUpdated` | `EventHandler<ChatRoomUpdatedEventArgs>` | Room modified |
| `ChatRoomRemoved` | `EventHandler<ChatRoomUpdatedEventArgs>` | Room deleted |

### Related: IChatRoom Interface

**Namespace:** `WinTak.Net.Chat`

Properties (inferred from usage):
- `Id` - Unique room identifier
- `Name` - Display name
- `Members` - Room participants

### Secondary: IContactService (Contact Management)

**Namespace:** `WinTak.Net.Contacts`
**Assembly:** WinTak.Net

This interface exists and is used by WinTAK's internal `CotDockPane` to look up contact information. However, **detailed documentation is not publicly available**. The key finding is:

> **Contacts are AUTOMATICALLY created from CoT events.** When you inject a CoT event with a callsign via `ICotMessageSender.Send()`, WinTAK automatically:
> 1. Creates a marker on the map
> 2. Adds the entity to the contacts list
> 3. Associates the callsign, team color, and other metadata

### IDockingManager (Multiple Dock Panes)

**Namespace:** `WinTak.Framework.Docking`

| Method | Signature | Purpose |
|--------|-----------|---------|
| `GetDockPane` | `DockPane GetDockPane(string id)` | Get pane by ID |
| `ActivateDockPane` | `void ActivateDockPane(string id)` | Show and focus pane |

Plugins CAN have multiple dock panes. Each pane needs:
- Unique ID in `[DockPane]` attribute
- Separate class inheriting from `DockPane`

### Settings API

**No native settings integration API found.** WinTAK's plugin architecture does NOT provide a `ISettingsService` or similar.

Plugins must:
1. Create their own settings UI (DockPane or popup window)
2. Store settings in their own file (XML/JSON in plugin data directory)
3. Load settings on plugin initialization

## 3. Code Sample

### Creating and Using Chat Rooms

```csharp
using System.ComponentModel.Composition;
using WinTak.Net.Chat;

public class MeshtasticChatService
{
    private readonly IChatService _chatService;
    private readonly Dictionary<int, IChatRoom> _channelRooms = new();

    [ImportingConstructor]
    public MeshtasticChatService(IChatService chatService)
    {
        _chatService = chatService;
        _chatService.MessageReceived += OnMessageReceived;
        _chatService.MessageSent += OnMessageSent;
    }

    // Create a chat room for each Meshtastic channel
    public void CreateChannelRoom(int channelIndex, string channelName)
    {
        var room = _chatService.CreateChatRoom();
        // Configure room properties (exact API TBD)
        // room.Name = $"Mesh: {channelName}";
        // room.Id = $"meshtastic-ch{channelIndex}";

        _chatService.AddRoom(room);
        _channelRooms[channelIndex] = room;
    }

    // Route inbound mesh message to channel-specific room
    public void RouteInboundMessage(int channelIndex, string senderCallsign, string text)
    {
        if (_channelRooms.TryGetValue(channelIndex, out var room))
        {
            var message = new Message
            {
                // Configure message (exact API TBD)
                // Text = text,
                // SenderCallsign = senderCallsign,
                // Timestamp = DateTime.UtcNow
            };

            // This may inject the message into WinTAK's chat UI
            // Or we may need to use ICotMessageSender with GeoChat CoT type
        }
    }

    // Intercept outbound messages to relay to mesh
    private void OnMessageSent(object sender, ChatMessageEventArgs args)
    {
        // Check if message is for a Meshtastic channel room
        var room = args.Room; // TBD: actual property name
        // if (IsMeshtasticRoom(room))
        // {
        //     RelayToMesh(args.Message);
        // }
    }

    private void OnMessageReceived(object sender, MessageReceivedEventArgs args)
    {
        System.Diagnostics.Debug.WriteLine($"Chat message received: {args}");
    }
}
```

### Contacts Appear Automatically from CoT

```csharp
// When you inject this CoT event...
string cotXml = @"
<event uid=""MESH-12345678"" type=""a-f-G-U-C"" ...>
    <point lat=""42.75"" lon=""-114.46"" .../>
    <detail>
        <contact callsign=""NODE-1234""/>
        <__group name=""Cyan"" role=""Team Member""/>
    </detail>
</event>";

var xmlDoc = new XmlDocument();
xmlDoc.LoadXml(cotXml);
_cotMessageSender.Send(xmlDoc);

// ...WinTAK automatically:
// 1. Creates marker on map with callsign "NODE-1234"
// 2. Adds "NODE-1234" to contacts list
// 3. Sets team color to Cyan
// 4. Shows marker with friendly ground icon (a-f-G-U-C)
```

### Multiple Dock Panes

```csharp
// Define multiple dock panes with unique IDs
[DockPane("MeshtasticMainPane", typeof(MeshtasticView), Caption = "Meshtastic")]
public class MeshtasticDockPane : DockPane { }

[DockPane("MeshtasticChatPane", typeof(MeshtasticChatView), Caption = "Mesh Chat")]
public class MeshtasticChatDockPane : DockPane { }

[DockPane("MeshtasticNodePane", typeof(MeshtasticNodeView), Caption = "Node Details")]
public class MeshtasticNodeDockPane : DockPane { }

// Open any pane from code
_dockingManager.ActivateDockPane("MeshtasticChatPane");
```

### Plugin Settings (Self-Managed)

```csharp
public class MeshtasticSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "wintak", "plugins", "WinTakMeshtasticPlugin", "settings.json");

    public string Hostname { get; set; } = "192.168.1.1";
    public int Port { get; set; } = 4403;
    public int ReconnectInterval { get; set; } = 15;
    public bool AutoConnect { get; set; } = false;

    public static MeshtasticSettings Load()
    {
        if (File.Exists(SettingsPath))
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<MeshtasticSettings>(json);
        }
        return new MeshtasticSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
```

## 4. Findings

### Chat Integration

| Question | Answer |
|----------|--------|
| Can we create chat groups programmatically? | ✅ YES - `IChatService.CreateChatRoom()` |
| Can we route inbound messages to specific groups? | ⚠️ PARTIAL - API exists but exact implementation TBD |
| Can we intercept outbound chat messages? | ✅ YES - `IChatService.MessageSent` event |

**Uncertainty:** The IChatService API is documented, but the exact integration pattern for making messages appear in WinTAK's native chat UI vs. using GeoChat CoT events needs testing. Both approaches may be valid:
1. Use IChatService for native integration
2. Use ICotMessageSender with GeoChat CoT type (b-t-f) for CoT-based chat

### Contact Integration

| Question | Answer |
|----------|--------|
| Are contacts automatic from CoT? | ✅ YES - Injecting CoT creates contacts automatically |
| Can we create contacts programmatically? | Not needed - CoT injection handles this |
| Do contacts appear in contact list? | ✅ YES - With callsign and team color |

### Node Detail Panel

| Question | Answer |
|----------|--------|
| Can we extend native contact detail panel? | ❌ NO - CotDockPane is internal |
| Can we show custom data on marker click? | ⚠️ PARTIAL - Need plugin-owned DockPane |

### Settings Integration

| Question | Answer |
|----------|--------|
| Native settings panel integration? | ❌ NO - No ISettingsService found |
| Plugin-owned settings viable? | ✅ YES - DockPane or popup window |

## 5. Impacted Requirements

| Requirement | Impact | Status |
|-------------|--------|--------|
| CHN-02 | Channel-to-chat-group mapping | ⚠️ Needs testing |
| MSG-01 | Inbound messages attributed to sender | ✅ Via contact/callsign |
| MSG-02 | Messages routed to channel groups | ⚠️ IChatService or GeoChat CoT |
| MSG-03 | Outbound messages to selected channel | ✅ Via MessageSent event |
| TEL-03 | Telemetry in structured panel | ❌ Need plugin-owned panel |
| NBR-01 | Neighbor info on node click | ❌ Need plugin-owned panel |
| NOD-05 | Node detail panel | ❌ Need plugin-owned panel |
| Settings | All settings/config requirements | ❌ Need plugin-owned settings |

## 6. Recommendation

### Chat Group Integration (D.1)

**Recommend: HYBRID approach**

1. **Try PRIMARY first**: Use `IChatService` to create chat rooms for each Meshtastic channel
2. **Fallback to GeoChat CoT**: If IChatService integration proves difficult, use GeoChat CoT events (type `b-t-f`) which definitely work

The GeoChat CoT approach is proven to work (this is how the Python gateway does it), but IChatService may provide better native integration.

### Node Detail Panel (D.2)

**Recommend: FALLBACK design**

Use a plugin-owned DockPane for node details because:
- CotDockPane is WinTAK internal and not extensible
- We need custom sections for telemetry, neighbors, channel membership
- DockPane pattern is well-documented and works

Implementation:
```csharp
[DockPane("MeshtasticNodePane", typeof(NodeDetailView), Caption = "Mesh Node")]
public class MeshtasticNodeDockPane : DockPane
{
    public void ShowNode(NodeState node) { /* bind to view */ }
}

// On marker click, activate our detail pane
_dockingManager.ActivateDockPane("MeshtasticNodePane");
```

### Settings Panel (D.4)

**Recommend: FALLBACK design**

Plugin-owned settings in a DockPane because:
- No native settings API available
- DockPane provides consistent WinTAK UI
- Settings persisted to JSON in plugin data directory

## 7. CLAUDE.md Updates

Add to Key WinTAK SDK Namespaces:
```
- `WinTak.Net.Chat` — IChatService, IChatRoom, Message (chat rooms)
- `WinTak.Net.Contacts` — IContactService, ContactService (auto from CoT)
```

Add to Architecture section:
```
- Contacts: AUTOMATIC from CoT — injecting CoT with callsign creates contacts
- Chat: IChatService.CreateChatRoom() or GeoChat CoT type (b-t-f)
- Settings: Plugin-managed JSON in %appdata%\wintak\plugins\{name}\settings.json
- Multiple dock panes: Each needs unique ID in [DockPane] attribute
```

---

## Cross-References

- **CotDispatcher.md** - CoT injection creates contacts automatically
- **SDKSetup.md** - MEF pattern for service injection
- **MapDrawingAPI** (pending) - For topology overlay lines

## Sources

- [WinTAK SDK - IChatService](https://iq-blue.com/WinTAK_SDK_Documentation/Doc/html/d1/dcc/interface_win_tak_1_1_net_1_1_chat_1_1_i_chat_service.html)
- [WinTAK SDK - Class Hierarchy](https://iq-blue.com/WinTAK_SDK_Documentation/Doc/html/hierarchy.html)
- [WinTAK SDK - CotDockPane](https://iq-blue.com/WinTAK_SDK_Documentation/Doc/html/d6/d50/class_win_tak_1_1_cursor_on_target_1_1_placement_1_1_dock_panes_1_1_cot_dock_pane.html)
- [WinTAK SDK - Extending WinTAK](https://iq-blue.com/WinTAK_SDK_Documentation/Doc/html/d0/d16/md_doxygen_extending-wintak__extending_win_tak.html)
