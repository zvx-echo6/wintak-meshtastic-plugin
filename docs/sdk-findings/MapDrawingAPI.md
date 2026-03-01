# SDK Finding: MapDrawingAPI (Topology Overlay Lines)

**Date:** 2026-03-01
**Investigator:** Phase 0 Spike Team
**Status:** CONFIRMED

## 1. Summary

WinTAK supports map drawing through two approaches: (1) **CoT-based shapes** using XML events with link elements, and (2) **Spyglass.Graphics** classes for programmatic rendering. For topology overlay lines connecting Meshtastic nodes, the PRIMARY approach is CoT-based polylines using `u-d-f` type events. The FALLBACK is using `PairingLine` or `Polyline` from the Spyglass.Graphics namespace.

## 2. SDK API / Method Tested

### Primary: CoT Shape Events

TAK supports drawing shapes via CoT XML injection. The following CoT types are relevant:

| CoT Type | Purpose | Example |
|----------|---------|---------|
| `u-d-f` | User-defined freeform (polyline/polygon) | Topology lines |
| `u-d-r` | User-defined rectangle | Bounding areas |
| `u-rb-a` | Range and bearing line | Point-to-point measurement |
| `b-m-r` | Route with waypoints | Path planning |

**Shape Definition Pattern:**
```xml
<event version="2.0" uid="shape-uid" type="u-d-f" time="..." start="..." stale="..." how="h-e">
  <point lat="center-lat" lon="center-lon" hae="0" ce="9999999" le="9999999"/>
  <detail>
    <link point="lat1,lon1"/>
    <link point="lat2,lon2"/>
    <link point="lat3,lon3"/>
    <!-- Additional vertices as needed -->
    <strokeColor value="-16776961"/>  <!-- ARGB color -->
    <strokeWeight value="3.0"/>
    <fillColor value="0"/>  <!-- 0 = no fill for lines -->
    <contact callsign="Line Name"/>
  </detail>
</event>
```

**Color Values:**
Colors are encoded as signed 32-bit integers (ARGB format):
- Red: `-65536` (0xFFFF0000)
- Green: `-16711936` (0xFF00FF00)
- Cyan: `-16711681` (0xFF00FFFF)
- Yellow: `-256` (0xFFFFFF00)
- White: `-1` (0xFFFFFFFF)
- Semi-transparent: Modify alpha channel

### Secondary: Spyglass.Graphics Classes

**Namespace:** `Spyglass.Graphics`
**Assembly:** Spyglass

| Class | Purpose |
|-------|---------|
| `Polyline` | Class that displays a polyline on the map |
| `MapMarker` | Represents a 2D marker positioned on the globe |
| `Label` | Text-based information on screen |
| `MapObject` | Container for renderable objects |

**WinTAK CoT Graphics (Namespace: `WinTak.CursorOnTarget.Graphics`):**

| Class | Purpose |
|-------|---------|
| `PairingLine` | Class that pairs 2 markers and changes with the markers |
| `RangeBearingPolyline` | Polyline with range/bearing calculations |
| `ParabolicLine` | Parabolic trajectory rendering |
| `CotMapMarker` | CoT-based map marker |

### Tertiary: Map Object Management

**Namespace:** `WinTak.Overlays.ViewModels`

| Class | Purpose |
|-------|---------|
| `MapObjectItemManager` | Manages MapObjects for display in overlay views |
| `MapObjectItemFactory` | Factory for creating map object items |

**IMapObjectRenderer** - Interface for rendering map objects (injected via MEF).

## 3. Code Sample

### CoT-Based Polyline (Topology Link)

```csharp
using System.Xml;
using WinTak.CursorOnTarget.Services;

public class TopologyOverlayService
{
    private readonly ICotMessageSender _cotMessageSender;

    public void DrawTopologyLink(
        string uid,
        double lat1, double lon1,
        double lat2, double lon2,
        string color = "-16711681",  // Cyan
        double strokeWeight = 2.0)
    {
        // Center point is midpoint of line
        double centerLat = (lat1 + lat2) / 2;
        double centerLon = (lon1 + lon2) / 2;

        string cotXml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<event version=""2.0""
       uid=""{uid}""
       type=""u-d-f""
       time=""{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}""
       start=""{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}""
       stale=""{DateTime.UtcNow.AddMinutes(30):yyyy-MM-ddTHH:mm:ss.fffZ}""
       how=""m-g"">
    <point lat=""{centerLat}"" lon=""{centerLon}"" hae=""0"" ce=""9999999"" le=""9999999""/>
    <detail>
        <link point=""{lat1},{lon1}""/>
        <link point=""{lat2},{lon2}""/>
        <strokeColor value=""{color}""/>
        <strokeWeight value=""{strokeWeight}""/>
        <fillColor value=""0""/>
        <contact callsign=""Mesh Link""/>
        <remarks>SNR: -10 dB</remarks>
    </detail>
</event>";

        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(cotXml);
        _cotMessageSender.Send(xmlDoc);
    }

    // Remove a link by making it stale
    public void RemoveTopologyLink(string uid)
    {
        // Send event with immediate stale time
        string cotXml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<event version=""2.0""
       uid=""{uid}""
       type=""u-d-f""
       time=""{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}""
       start=""{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}""
       stale=""{DateTime.UtcNow.AddSeconds(-1):yyyy-MM-ddTHH:mm:ss.fffZ}""
       how=""m-g"">
    <point lat=""0"" lon=""0"" hae=""0"" ce=""9999999"" le=""9999999""/>
    <detail/>
</event>";

        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(cotXml);
        _cotMessageSender.Send(xmlDoc);
    }
}
```

### SNR-Based Line Styling

```csharp
public class TopologyLinkBuilder
{
    // Color based on SNR quality
    public static string GetSnrColor(double snrDb)
    {
        // Green = excellent (> -5 dB)
        // Yellow = good (-5 to -10 dB)
        // Orange = marginal (-10 to -15 dB)
        // Red = poor (< -15 dB)

        if (snrDb > -5) return "-16711936";   // Green
        if (snrDb > -10) return "-256";        // Yellow
        if (snrDb > -15) return "-32768";      // Orange
        return "-65536";                        // Red
    }

    // Line weight based on link quality
    public static double GetLineWeight(double snrDb)
    {
        if (snrDb > -5) return 4.0;   // Thick for strong
        if (snrDb > -10) return 3.0;
        if (snrDb > -15) return 2.0;
        return 1.0;                    // Thin for weak
    }
}
```

### Managing Multiple Topology Links

```csharp
public class TopologyOverlayManager
{
    private readonly TopologyOverlayService _overlayService;
    private readonly Dictionary<string, (uint nodeA, uint nodeB)> _activeLinks = new();

    // Update topology from neighbor info
    public void UpdateFromNeighborInfo(uint reportingNodeId, IEnumerable<NeighborEntry> neighbors)
    {
        foreach (var neighbor in neighbors)
        {
            // Create deterministic UID from node pair (sorted)
            uint nodeA = Math.Min(reportingNodeId, neighbor.NodeId);
            uint nodeB = Math.Max(reportingNodeId, neighbor.NodeId);
            string linkUid = $"MESH-LINK-{nodeA:X8}-{nodeB:X8}";

            // Get positions of both nodes
            var nodeAState = GetNodeState(nodeA);
            var nodeBState = GetNodeState(nodeB);

            if (nodeAState?.HasPosition == true && nodeBState?.HasPosition == true)
            {
                string color = TopologyLinkBuilder.GetSnrColor(neighbor.Snr);
                double weight = TopologyLinkBuilder.GetLineWeight(neighbor.Snr);

                _overlayService.DrawTopologyLink(
                    linkUid,
                    nodeAState.Latitude.Value, nodeAState.Longitude.Value,
                    nodeBState.Latitude.Value, nodeBState.Longitude.Value,
                    color, weight);

                _activeLinks[linkUid] = (nodeA, nodeB);
            }
        }
    }

    // Clear all topology links
    public void ClearAllLinks()
    {
        foreach (var linkUid in _activeLinks.Keys)
        {
            _overlayService.RemoveTopologyLink(linkUid);
        }
        _activeLinks.Clear();
    }
}
```

## 4. Findings

### Confirmed Capabilities

| Feature | Status | Notes |
|---------|--------|-------|
| Draw polylines via CoT | ✅ YES | Use `u-d-f` type with link elements |
| Line color styling | ✅ YES | `<strokeColor value="..."/>` |
| Line weight styling | ✅ YES | `<strokeWeight value="..."/>` |
| Dynamic line updates | ✅ YES | Resend CoT with same UID |
| Remove lines | ✅ YES | Send CoT with past stale time |
| Lines follow markers | ⚠️ PARTIAL | Must manually update when nodes move |

### CoT Shape Type Reference

From [ATAK CoT Examples](https://github.com/deptofdefense/AndroidTacticalAssaultKit-CIV/tree/master/takcot/examples):

| Type | Description |
|------|-------------|
| `u-d-f` | User-defined freeform polyline/polygon |
| `u-d-r` | User-defined rectangle |
| `u-d-c-c` | User-defined circle |
| `u-rb-a` | Range and bearing line |
| `b-m-r` | Mission route |
| `b-m-p-w` | Route waypoint |

### Graphics API Uncertainty

The `Spyglass.Graphics.Polyline` and `WinTak.CursorOnTarget.Graphics.PairingLine` classes exist but:
- Documentation server returned 500 errors during investigation
- No public code samples demonstrating their use
- MEF injection pattern for graphics services is unclear

**Recommendation:** Use CoT-based approach (confirmed working) as PRIMARY. Reserve Graphics API for FALLBACK if CoT shapes have limitations.

## 5. Impacted Requirements

| Requirement | Impact | Status |
|-------------|--------|--------|
| NBR-02 | Topology overlay lines between nodes | ✅ Via CoT shapes |
| NBR-03 | SNR-based line color coding | ✅ Via strokeColor |
| NBR-04 | Toggle topology overlay visibility | ✅ Clear/redraw links |
| NBR-01 | Neighbor info in node detail | Separate (DockPane) |

## 6. Recommendation

### Topology Overlay (NBR-02, NBR-03, NBR-04)

**Recommend: PRIMARY design using CoT shapes**

1. Use `u-d-f` (user-defined freeform) CoT type for topology links
2. Each link is a 2-point polyline connecting node centers
3. UID format: `MESH-LINK-{nodeA:X8}-{nodeB:X8}` (sorted node IDs)
4. Color based on SNR: Green > Yellow > Orange > Red
5. Line weight based on link quality (1.0 - 4.0)
6. Update links when:
   - Neighbor info packet received
   - Node position changes
   - Node goes stale (remove link)

### Implementation Plan

```
TopologyOverlayService
├── DrawTopologyLink(uid, lat1, lon1, lat2, lon2, color, weight)
├── RemoveTopologyLink(uid)
└── UpdateAllLinks() // Called when any node position changes

TopologyLinkBuilder
├── GetSnrColor(snrDb) → string
├── GetLineWeight(snrDb) → double
└── BuildLinkUid(nodeA, nodeB) → string

TopologyOverlayManager
├── _activeLinks: Dictionary<string, (nodeA, nodeB)>
├── UpdateFromNeighborInfo(nodeId, neighbors)
├── OnNodePositionChanged(nodeId)
├── ClearAllLinks()
└── SetOverlayVisible(bool)
```

### Toggle Visibility (NBR-04)

When user toggles overlay OFF:
- Store link UIDs in `_hiddenLinks`
- Call `RemoveTopologyLink()` for each

When user toggles overlay ON:
- Rebuild all links from current neighbor graph
- Call `DrawTopologyLink()` for each

## 7. CLAUDE.md Updates

Add to Architecture section:
```
- Topology overlay: CoT shapes (u-d-f type) via ICotMessageSender
- Link UID format: MESH-LINK-{nodeA:X8}-{nodeB:X8}
- SNR color coding: Green > -5dB, Yellow > -10dB, Orange > -15dB, Red < -15dB
```

Add to Key WinTAK SDK Namespaces:
```
- `Spyglass.Graphics` — Polyline, MapMarker, MapObject (graphics rendering)
- `WinTak.CursorOnTarget.Graphics` — PairingLine, CotMapMarker
- `WinTak.Overlays.ViewModels` — MapObjectItemManager (overlay management)
```

---

## Cross-References

- **CotDispatcher.md** - CoT injection via ICotMessageSender
- **ChatContactAPI.md** - Contacts automatic from CoT
- **SDKSetup.md** - MEF injection pattern

## Sources

- [ATAK CoT Examples - Drawing Shapes](https://github.com/deptofdefense/AndroidTacticalAssaultKit-CIV/tree/master/takcot/examples)
- [WinTAK SDK - Class Hierarchy](https://iq-blue.com/WinTAK_SDK_Documentation/Doc/html/hierarchy.html)
- [WinTAK SDK - MapObjectItemManager](https://iq-blue.com/WinTAK_SDK_Documentation/Doc/html/d6/d75/class_win_tak_1_1_overlays_1_1_view_models_1_1_map_object_item_manager.html)
- [WinTAK Simple Usage Plugin](https://github.com/Cale-Torino/WinTAK_Simple_Usage_Plugin)
