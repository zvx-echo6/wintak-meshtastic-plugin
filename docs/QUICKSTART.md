# WinTAK Meshtastic Plugin - Quick Start Guide

## Build & Deploy

### Prerequisites
- Visual Studio 2022 with .NET desktop development workload
- .NET Framework 4.8.1 SDK
- WinTAK installed (provides NuGet packages)

### Build Commands
```powershell
# From project root
cd E:\Documents\projects\winktak-meshtastic-plugin

# Release build (auto-deploys to %appdata%\wintak\plugins\WinTakMeshtasticPlugin\)
dotnet build src/ -c Release

# Or using MSBuild directly
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" src\WinTakMeshtasticPlugin.csproj /p:Configuration=Release
```

### Plugin Folder Contents
After a successful build, these files should appear in:
`%appdata%\wintak\plugins\WinTakMeshtasticPlugin\`

| File | Purpose | Expected Size |
|------|---------|---------------|
| WinTakMeshtasticPlugin.dll | Main plugin assembly | ~200-300 KB |
| WinTakMeshtasticPlugin.pdb | Debug symbols | ~100-200 KB |
| Google.Protobuf.dll | Protobuf runtime | ~400-500 KB |

To verify deployment:
```powershell
dir "$env:APPDATA\wintak\plugins\WinTakMeshtasticPlugin"
```

---

## Configuration

### 1. Connect to Meshtastic Node

**Before launching WinTAK:**
- Ensure your Meshtastic node is powered on
- Verify TCP is enabled on the node (via Meshtastic app or web interface)
- Note the node's IP address or hostname

**In WinTAK:**
1. Open the Meshtastic plugin settings panel (dock pane on the right)
2. Enter connection details:
   - **Hostname/IP**: Your Meshtastic node's IP (e.g., `192.168.1.100` or `meshtastic.local`)
   - **Port**: Default is `4403` (configurable if your node uses a different port)
3. Click **Connect**
4. Connection status indicator should turn **green** when connected

### 2. What You Should See When Working

**On the WinTAK Map:**
- Mesh node markers appear as **cyan team markers** (channel 0 default)
- Each node shows its shortname as the callsign
- Markers are CoT type `a-f-G-U-C` (friendly ground civilian)
- Router nodes show as `a-f-G-U-C-I` (infrastructure)
- Tracker nodes show as `a-f-G-E-S` (equipment/sensor)

**In the Plugin Panel:**
- **Status**: Green "Connected" indicator
- **Nodes tracked**: Count of mesh nodes seen
- **Channels**: List of channels with team colors
  - Ch0=Cyan, Ch1=Green, Ch2=Yellow, Ch3=Orange
  - Ch4=Red, Ch5=Purple, Ch6=White, Ch7=Magenta

**Chat:**
- Messages appear in channel-specific chat rooms
- Format: "Mesh: {ChannelName}" (e.g., "Mesh: LongFast")

---

## Troubleshooting

### Nothing Appears on the Map

**Check Connection:**
1. Verify status indicator shows "Connected" (green)
2. If "Disconnected" (red), verify:
   - Node IP/hostname is correct
   - Port 4403 is not blocked by firewall
   - Node has TCP enabled

**Check Node Activity:**
1. Open Meshtastic app on your phone
2. Verify your node shows other mesh members
3. If no other nodes visible, the mesh may be empty

**Check Plugin Loading:**
1. Look in WinTAK's plugin manager for "WinTakMeshtasticPlugin"
2. If not listed, verify DLL is in correct folder

### Log File Location

**WinTAK Debug Output:**
- Enable Debug Output: WinTAK menu > Tools > Options > Debug
- View Output: WinTAK menu > View > Output Window

**Plugin Debug Statements:**
All plugin logging uses `System.Diagnostics.Debug.WriteLine()` with prefix `[Meshtastic]`.

Look for entries like:
```
[Meshtastic] Plugin initializing...
[Meshtastic] Connecting to 192.168.1.100:4403...
[Meshtastic] Connection state: Disconnected -> Connected
[Meshtastic] Received MeshPacket from node 12345678, portnum PositionApp
[Meshtastic] Node state updated: !1234 @ 42.750000, -114.460000
[Meshtastic] Injected position for !1234
```

**Settings File:**
```
%appdata%\wintak\plugins\WinTakMeshtasticPlugin\settings.json
```

### Common Issues

| Symptom | Likely Cause | Fix |
|---------|--------------|-----|
| "Connecting..." forever | Wrong IP or port blocked | Verify IP, check firewall |
| Connected but no markers | No position packets yet | Wait for nodes to send GPS |
| Markers appear then disappear | Stale timeout (30 min default) | Nodes need to send position updates |
| Wrong team colors | Channel mismatch | Verify channel index mapping |
| Plugin not loading | Missing dependencies | Check Google.Protobuf.dll is present |

### Firewall Configuration

Allow inbound/outbound TCP on port 4403 (or your configured port):
```powershell
# Run as Administrator
netsh advfirewall firewall add rule name="Meshtastic TCP" dir=in action=allow protocol=TCP localport=4403
netsh advfirewall firewall add rule name="Meshtastic TCP" dir=out action=allow protocol=TCP localport=4403
```

---

## Settings Reference

| Setting | Default | Range | Description |
|---------|---------|-------|-------------|
| Hostname | localhost | - | Meshtastic node IP or hostname |
| Port | 4403 | 1-65535 | TCP port |
| Auto-connect | false | - | Connect on WinTAK startup |
| Reconnect interval | 15 | 5-60 sec | Auto-reconnect delay |
| Stale timeout | 24 | 1-168 hours | Remove inactive nodes |
| Outbound PLI | false | - | Send WinTAK position to mesh |
| PLI interval | 60 | 10-600 sec | Position broadcast frequency |

---

## Expected Behavior Summary

1. **Launch WinTAK** - Plugin loads automatically
2. **Configure & Connect** - Enter Meshtastic IP, click Connect
3. **Status turns green** - TCP connection established
4. **Channels populate** - As node reports config
5. **Markers appear** - As mesh nodes send position packets
6. **Chat works** - Messages route to channel-specific rooms
