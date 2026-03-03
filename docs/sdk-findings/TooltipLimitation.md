# WinTAK Map Tooltip Limitation

## Summary

The WinTAK quick tooltip (upper-right info panel that appears when clicking a map marker) has a **fixed format** that cannot be extended by plugins. Only specific CoT detail elements are displayed.

## What the Tooltip Shows

When you click a marker on the map, the tooltip displays:

1. **Callsign** - from `<contact callsign="..."/>`
2. **Coordinates** - from `<point lat="" lon=""/>`
3. **CE/LE** - Circular/Linear Error from `<point ce="" le=""/>`
4. **Track** - Speed and heading from `<track speed="" course=""/>`
   - Format: "Track: X Kmph 348° M"
   - If no track element: "Track: --- Kmph ---° M"

## CoT Elements WinTAK Reads Natively

Based on decompiling `WinTak.CursorOnTarget.dll` (`MapObjectInfo` class):

```xml
<detail>
    <!-- Callsign for display -->
    <contact callsign="NODE-NAME"/>

    <!-- Speed/heading for tooltip track display -->
    <track speed="5.0" course="348.0"/>

    <!-- GPS source info -->
    <precisionlocation geopointsrc="GPS" altsrc="GPS"/>

    <!-- Battery level (shows in Details pane, not tooltip) -->
    <status battery="85"/>

    <!-- Full telemetry (shows in User Details panel) -->
    <remarks>Battery: 85% | Uptime: 2d 5h</remarks>
</detail>
```

## What Cannot Be Shown in Tooltip

- Custom telemetry (battery, temperature, humidity, etc.)
- Extended node metadata
- Mesh-specific information

These **must** go in `<remarks>` which displays in the **User Details panel** (right-click marker > "User Details" or double-click marker).

## Plugin Implementation

The Meshtastic plugin adds all native CoT elements that WinTAK recognizes:

| Element | Purpose | Display Location |
|---------|---------|------------------|
| `<contact callsign=""/>` | Node name | Tooltip + Details |
| `<track speed="" course=""/>` | Speed/heading | Tooltip |
| `<precisionlocation/>` | GPS source | Tooltip (CE/LE area) |
| `<status battery=""/>` | Battery level | Details pane |
| `<remarks>` | Full telemetry | User Details panel |
| `<__meshtastic>` | Mesh metadata | Plugin internal use |

## Viewing Full Telemetry

Users can access complete node telemetry via:

1. **User Details Panel** - Right-click marker > select node > view Remarks section
2. **Telemetry Window** - Double-click marker (opens plugin telemetry popup)
3. **Node List** - Plugin side panel shows all tracked nodes

## Technical Details

The tooltip is rendered by `WinTak.CursorOnTarget.MapObjectInfo.UpdateFromMapObject()` which specifically reads:
- `<track>` element for speed/course
- `<contact>` element for callsign
- `<precisionlocation>` element for GPS source

The format string is hardcoded in `Resources.TrackSpeedAndRB` and cannot be modified without changing WinTAK itself.

## References

- Decompiled from: `C:\Program Files\WinTAK\WinTak.CursorOnTarget.dll`
- Classes: `MapObjectInfo`, `CotDockPane`
- Investigation date: 2024
