using System;
using System.Linq;
using System.Security;
using System.Text;
using System.Xml;
using WinTakMeshtasticPlugin.Models;

namespace WinTakMeshtasticPlugin.CoT
{
    /// <summary>
    /// Builder for Cursor-on-Target (CoT) XML messages.
    /// All CoT must validate against TAK 5.x schema.
    /// </summary>
    public class CotBuilder : ICotBuilder
    {
        /// <summary>
        /// Default stale time for node PLI: 30 minutes.
        /// </summary>
        public static readonly TimeSpan DefaultStaleTime = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Display name mode for map callsigns.
        /// ShortName = Use short name as callsign (default).
        /// LongName = Use long name as callsign.
        /// </summary>
        public Models.DisplayNameMode DisplayNameMode { get; set; } = Models.DisplayNameMode.ShortName;

        /// <summary>
        /// Build a Position Location Information (PLI) CoT event for a mesh node.
        /// </summary>
        /// <param name="nodeState">The node state with position and identity info.</param>
        /// <param name="staleTime">How long until the event goes stale. Default: 30 minutes.</param>
        /// <returns>CoT XML string.</returns>
        public string BuildNodePli(NodeState nodeState, TimeSpan? staleTime = null)
        {
            if (nodeState == null)
                throw new ArgumentNullException(nameof(nodeState));

            if (!nodeState.Latitude.HasValue || !nodeState.Longitude.HasValue)
                throw new ArgumentException("Node must have position data", nameof(nodeState));

            var effectiveStaleTime = staleTime ?? DefaultStaleTime;
            var now = DateTime.UtcNow;
            var staleAt = now + effectiveStaleTime;

            // Get CoT type based on device role (per CLAUDE.md)
            var cotType = GetCotTypeForRole(nodeState.Role);

            // Generate unique event UID from connection and node ID
            var uid = $"MESH-{nodeState.ConnectionId}-{nodeState.NodeId:X8}";

            // Callsign based on DisplayNameMode (SEC-07: XML-escape all mesh strings)
            var callsign = XmlEscape(GetDisplayCallsign(nodeState, DisplayNameMode));

            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.Append("<event version=\"2.0\"");
            sb.AppendFormat(" uid=\"{0}\"", uid);
            sb.AppendFormat(" type=\"{0}\"", cotType);
            sb.AppendFormat(" time=\"{0}\"", FormatCotTime(now));
            sb.AppendFormat(" start=\"{0}\"", FormatCotTime(now));
            sb.AppendFormat(" stale=\"{0}\"", FormatCotTime(staleAt));
            sb.Append(" how=\"m-g\""); // Machine-generated
            sb.Append(">");

            // Point element with position
            sb.Append("<point");
            sb.AppendFormat(" lat=\"{0}\"", nodeState.Latitude.Value.ToString("F7"));
            sb.AppendFormat(" lon=\"{0}\"", nodeState.Longitude.Value.ToString("F7"));
            sb.AppendFormat(" hae=\"{0}\"", nodeState.Altitude?.ToString("F1") ?? "9999999");
            sb.Append(" ce=\"9999999\" le=\"9999999\"/>"); // Unknown accuracy

            // Detail element
            sb.Append("<detail>");

            // Contact with callsign
            sb.AppendFormat("<contact callsign=\"{0}\"/>", callsign);

            // Team color based on channel membership
            var teamColor = GetTeamColorForChannel(nodeState.PrimaryChannel);
            sb.AppendFormat("<__group name=\"{0}\" role=\"Team Member\"/>", teamColor);

            // Track element with speed/course (WinTAK displays this in tooltip)
            // Speed is in m/s, course in degrees
            var speed = nodeState.GroundSpeed ?? 0;
            var course = nodeState.GroundTrack ?? 0;
            sb.AppendFormat("<track speed=\"{0}\" course=\"{1}\"/>",
                speed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                course.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));

            // Precisionlocation for GPS source (WinTAK displays this in tooltip)
            sb.Append("<precisionlocation geopointsrc=\"GPS\" altsrc=\"GPS\"/>");

            // Status element with battery level (WinTAK displays this natively)
            if (nodeState.DeviceTelemetry?.BatteryLevel.HasValue == true)
            {
                sb.AppendFormat("<status battery=\"{0}\"/>", nodeState.DeviceTelemetry.BatteryLevel.Value);
            }

            // Remarks with node info (excluding PSK per SEC-04)
            var remarks = BuildRemarksText(nodeState, DisplayNameMode);
            if (!string.IsNullOrEmpty(remarks))
            {
                sb.AppendFormat("<remarks>{0}</remarks>", XmlEscape(remarks));
            }

            // Custom mesh node metadata
            sb.Append("<__meshtastic>");
            sb.AppendFormat("<nodeId>{0}</nodeId>", nodeState.NodeIdHex);
            if (!string.IsNullOrEmpty(nodeState.LongName))
                sb.AppendFormat("<longName>{0}</longName>", XmlEscape(nodeState.LongName));
            if (!string.IsNullOrEmpty(nodeState.HardwareModel))
                sb.AppendFormat("<hardware>{0}</hardware>", XmlEscape(nodeState.HardwareModel));
            if (!string.IsNullOrEmpty(nodeState.FirmwareVersion))
                sb.AppendFormat("<firmware>{0}</firmware>", XmlEscape(nodeState.FirmwareVersion));
            sb.AppendFormat("<role>{0}</role>", nodeState.Role);
            sb.AppendFormat("<lastHeard>{0}</lastHeard>", FormatCotTime(nodeState.LastHeard));
            sb.Append("</__meshtastic>");

            sb.Append("</detail>");
            sb.Append("</event>");

            return sb.ToString();
        }

        /// <summary>
        /// Build a GeoChat CoT event for a text message.
        /// </summary>
        public string BuildGeoChat(
            string senderUid,
            string senderCallsign,
            string message,
            string? chatRoom = null,
            string? destinationUid = null)
        {
            var now = DateTime.UtcNow;
            var staleAt = now + TimeSpan.FromMinutes(5);
            var messageId = Guid.NewGuid().ToString();

            // SEC-07: Sanitize all string input
            senderCallsign = XmlEscape(senderCallsign);
            message = XmlEscape(message);
            chatRoom = chatRoom != null ? XmlEscape(chatRoom) : null;

            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.Append("<event version=\"2.0\"");
            sb.AppendFormat(" uid=\"GeoChat.{0}.{1}\"", senderUid, messageId);
            sb.Append(" type=\"b-t-f\""); // Broadcast-text-freeform
            sb.AppendFormat(" time=\"{0}\"", FormatCotTime(now));
            sb.AppendFormat(" start=\"{0}\"", FormatCotTime(now));
            sb.AppendFormat(" stale=\"{0}\"", FormatCotTime(staleAt));
            sb.Append(" how=\"h-g-i-g-o\">"); // Human-generated

            // Point (sender's position, or 0,0 if unknown)
            sb.Append("<point lat=\"0\" lon=\"0\" hae=\"0\" ce=\"9999999\" le=\"9999999\"/>");

            sb.Append("<detail>");

            // Chat element
            sb.Append("<__chat");
            sb.AppendFormat(" senderCallsign=\"{0}\"", senderCallsign);
            if (!string.IsNullOrEmpty(chatRoom))
            {
                sb.AppendFormat(" chatroom=\"{0}\"", chatRoom);
                sb.AppendFormat(" groupOwner=\"false\"");
            }
            sb.AppendFormat(" messageId=\"{0}\"", messageId);
            sb.Append(">");

            // Chat group
            sb.AppendFormat("<chatgrp uid0=\"{0}\"", senderUid);
            if (!string.IsNullOrEmpty(destinationUid))
                sb.AppendFormat(" uid1=\"{0}\"", destinationUid);
            sb.Append("/>");

            sb.Append("</__chat>");

            // Link to sender
            sb.AppendFormat("<link uid=\"{0}\" type=\"a-f-G-U-C\" relation=\"p-p\"/>", senderUid);

            // Remarks contains the actual message
            sb.AppendFormat("<remarks source=\"{0}\" time=\"{1}\">{2}</remarks>",
                senderUid, FormatCotTime(now), message);

            sb.Append("</detail>");
            sb.Append("</event>");

            return sb.ToString();
        }

        /// <summary>
        /// Get the CoT type string for a device role.
        /// Per CLAUDE.md CoT Schema Rules.
        /// </summary>
        public static string GetCotTypeForRole(DeviceRole role)
        {
            return role switch
            {
                DeviceRole.Router => "a-f-G-U-C-I",      // Infrastructure
                DeviceRole.RouterClient => "a-f-G-U-C-I",
                DeviceRole.Repeater => "a-f-G-U-C-I",
                DeviceRole.Tracker => "a-f-G-E-S",       // Equipment/sensor
                DeviceRole.TakTracker => "a-f-G-E-S",
                DeviceRole.Sensor => "a-f-G-E-S",
                DeviceRole.Client => "a-f-G-U-C",        // Friendly ground civilian
                DeviceRole.ClientMute => "a-f-G-U-C",
                DeviceRole.ClientHidden => "a-f-G-U-C",
                DeviceRole.Tak => "a-f-G-U-C",
                DeviceRole.LostAndFound => "a-f-G-U-C",
                _ => "a-f-G"                             // Unknown: friendly ground unspecified
            };
        }

        /// <summary>
        /// Get the team color for a channel index.
        /// Per CLAUDE.md channel-to-team-color defaults.
        /// </summary>
        public static string GetTeamColorForChannel(int channelIndex)
        {
            return channelIndex switch
            {
                0 => "Cyan",
                1 => "Green",
                2 => "Yellow",
                3 => "Orange",
                4 => "Red",
                5 => "Purple",
                6 => "White",
                7 => "Magenta",
                _ => "Cyan"
            };
        }

        /// <summary>
        /// Format a DateTime as a CoT timestamp (ISO 8601 with Z suffix).
        /// </summary>
        public static string FormatCotTime(DateTime time)
        {
            return time.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        /// <summary>
        /// XML-escape a string to prevent injection attacks (SEC-07).
        /// </summary>
        public static string XmlEscape(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return SecurityElement.Escape(input);
        }

        /// <summary>
        /// Get the display callsign for a node based on display mode.
        /// ShortName mode: ShortName → LongName → hex ID
        /// LongName mode: LongName → ShortName → hex ID
        /// </summary>
        private static string GetDisplayCallsign(NodeState nodeState, Models.DisplayNameMode mode)
        {
            if (mode == Models.DisplayNameMode.LongName)
            {
                // LongName mode: prefer LongName
                if (!string.IsNullOrEmpty(nodeState.LongName))
                    return nodeState.LongName;
                if (!string.IsNullOrEmpty(nodeState.ShortName))
                    return nodeState.ShortName;
            }
            else
            {
                // ShortName mode: prefer ShortName
                if (!string.IsNullOrEmpty(nodeState.ShortName))
                    return nodeState.ShortName;
                if (!string.IsNullOrEmpty(nodeState.LongName))
                    return nodeState.LongName;
            }
            return nodeState.NodeIdHex;
        }

        /// <summary>
        /// Build remarks text for a node (telemetry summary).
        /// Never includes PSK values (SEC-04).
        /// Format (ShortName mode - callsign is ShortName):
        /// LongName (ShortName)
        /// Battery: 85% (4.1V) | Uptime: 2d 5h
        /// ...
        /// Format (LongName mode - callsign is LongName):
        /// ShortName: HnRp
        /// Battery: 85% (4.1V) | Uptime: 2d 5h
        /// ...
        /// </summary>
        private static string BuildRemarksText(NodeState nodeState, Models.DisplayNameMode mode)
        {
            var lines = new System.Collections.Generic.List<string>();

            // Line 0: Complementary name info (the name NOT shown in callsign)
            if (mode == Models.DisplayNameMode.ShortName)
            {
                // Callsign is ShortName, so show "LongName (ShortName)" in remarks
                if (!string.IsNullOrEmpty(nodeState.LongName))
                {
                    var shortPart = !string.IsNullOrEmpty(nodeState.ShortName) ? $" ({nodeState.ShortName})" : "";
                    lines.Add($"{nodeState.LongName}{shortPart}");
                }
            }
            else
            {
                // Callsign is LongName, so show "ShortName: HnRp" in remarks
                if (!string.IsNullOrEmpty(nodeState.ShortName))
                {
                    lines.Add($"ShortName: {nodeState.ShortName}");
                }
            }

            // Line 1: Battery and uptime
            var line1 = new System.Collections.Generic.List<string>();
            if (nodeState.DeviceTelemetry != null)
            {
                var tel = nodeState.DeviceTelemetry;
                if (tel.BatteryLevel.HasValue)
                {
                    var batteryStr = tel.Voltage.HasValue
                        ? $"Battery: {tel.BatteryLevel}% ({tel.Voltage:F1}V)"
                        : $"Battery: {tel.BatteryLevel}%";
                    line1.Add(batteryStr);
                }
                if (tel.UptimeSeconds.HasValue)
                {
                    line1.Add($"Uptime: {tel.UptimeFormatted}");
                }
            }
            if (line1.Count > 0)
                lines.Add(string.Join(" | ", line1));

            // Line 2: Channel utilization
            var line2 = new System.Collections.Generic.List<string>();
            if (nodeState.DeviceTelemetry != null)
            {
                var tel = nodeState.DeviceTelemetry;
                if (tel.ChannelUtilization.HasValue)
                    line2.Add($"ChUtil: {tel.ChannelUtilization:F0}%");
                if (tel.AirUtilTx.HasValue)
                    line2.Add($"AirTX: {tel.AirUtilTx:F0}%");
            }
            if (line2.Count > 0)
                lines.Add(string.Join(" | ", line2));

            // Line 3: Temperature and humidity
            var line3 = new System.Collections.Generic.List<string>();
            if (nodeState.EnvironmentTelemetry != null)
            {
                var env = nodeState.EnvironmentTelemetry;
                if (env.Temperature.HasValue)
                    line3.Add($"Temp: {env.TemperatureFahrenheit:F0}°F / {env.Temperature:F0}°C");
                if (env.RelativeHumidity.HasValue)
                    line3.Add($"Hum: {env.RelativeHumidity:F0}%");
            }
            if (line3.Count > 0)
                lines.Add(string.Join(" | ", line3));

            // Line 4: Pressure
            if (nodeState.EnvironmentTelemetry?.BarometricPressure.HasValue == true)
            {
                var env = nodeState.EnvironmentTelemetry;
                lines.Add($"Pressure: {env.PressureInHg:F2} inHg");
            }

            // Line 5: Hardware and firmware
            var line5 = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(nodeState.HardwareModel))
                line5.Add($"Hardware: {nodeState.HardwareModel}");
            if (!string.IsNullOrEmpty(nodeState.FirmwareVersion))
                line5.Add($"FW: {nodeState.FirmwareVersion}");
            if (line5.Count > 0)
                lines.Add(string.Join(" | ", line5));

            // Line 6: Neighbors summary
            if (nodeState.Neighbors != null && nodeState.Neighbors.Count > 0)
            {
                var neighborParts = new System.Collections.Generic.List<string>();
                foreach (var neighbor in nodeState.Neighbors.Take(5)) // Show top 5 neighbors
                {
                    var name = !string.IsNullOrEmpty(neighbor.NodeName)
                        ? neighbor.NodeName
                        : neighbor.NodeIdHex;
                    neighborParts.Add($"{name} ({neighbor.Snr:F0}dB)");
                }
                var neighborStr = string.Join(", ", neighborParts);
                if (nodeState.Neighbors.Count > 5)
                    neighborStr += $" +{nodeState.Neighbors.Count - 5} more";
                lines.Add($"Neighbors: {neighborStr}");
            }

            // If no telemetry at all, ensure we have at least node identity
            if (lines.Count == 0)
            {
                // Show whatever name info we have
                if (!string.IsNullOrEmpty(nodeState.LongName))
                    lines.Add(nodeState.LongName);
                else if (!string.IsNullOrEmpty(nodeState.ShortName))
                    lines.Add(nodeState.ShortName);
            }

            return string.Join("\n", lines);
        }
    }

    /// <summary>
    /// Interface for CoT building to support dependency injection and testing.
    /// </summary>
    public interface ICotBuilder
    {
        string BuildNodePli(NodeState nodeState, TimeSpan? staleTime = null);
        string BuildGeoChat(string senderUid, string senderCallsign, string message,
            string? chatRoom = null, string? destinationUid = null);
    }
}
