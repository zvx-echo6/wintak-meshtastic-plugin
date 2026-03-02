using System;
using System.Collections.Generic;
using System.Linq;

namespace WinTakMeshtasticPlugin.Models
{
    /// <summary>
    /// Represents the current state of a mesh node.
    /// Keyed by (connectionId, nodeId) tuple to support future multi-node connections.
    /// </summary>
    public class NodeState
    {
        /// <summary>
        /// The connection this node was discovered through.
        /// </summary>
        public string ConnectionId { get; set; } = string.Empty;

        /// <summary>
        /// The Meshtastic node number (32-bit unsigned integer).
        /// </summary>
        public uint NodeId { get; set; }

        /// <summary>
        /// Node ID formatted as hex string (e.g., "!a1b2c3d4").
        /// </summary>
        public string NodeIdHex => $"!{NodeId:x8}";

        /// <summary>
        /// Short name from NodeInfo (max 4 chars on device).
        /// </summary>
        public string? ShortName { get; set; }

        /// <summary>
        /// Long name from NodeInfo.
        /// </summary>
        public string? LongName { get; set; }

        /// <summary>
        /// Hardware model string.
        /// </summary>
        public string? HardwareModel { get; set; }

        /// <summary>
        /// Firmware version string.
        /// </summary>
        public string? FirmwareVersion { get; set; }

        /// <summary>
        /// Device role (CLIENT, ROUTER, TRACKER, etc.)
        /// </summary>
        public DeviceRole Role { get; set; } = DeviceRole.Client;

        /// <summary>
        /// Last known latitude in degrees.
        /// </summary>
        public double? Latitude { get; set; }

        /// <summary>
        /// Last known longitude in degrees.
        /// </summary>
        public double? Longitude { get; set; }

        /// <summary>
        /// Last known altitude in meters.
        /// </summary>
        public double? Altitude { get; set; }

        /// <summary>
        /// Channels this node has been heard on (by index).
        /// </summary>
        public HashSet<int> ChannelsMembership { get; set; } = new();

        /// <summary>
        /// Primary channel index (lowest channel this node is heard on).
        /// </summary>
        public int PrimaryChannel => ChannelsMembership.Count > 0
            ? ChannelsMembership.Min()
            : 0;

        /// <summary>
        /// Last device telemetry data.
        /// </summary>
        public TelemetryData? DeviceTelemetry { get; set; }

        /// <summary>
        /// Last environment telemetry data.
        /// </summary>
        public EnvironmentTelemetry? EnvironmentTelemetry { get; set; }

        /// <summary>
        /// Known neighbors with SNR values.
        /// </summary>
        public List<NeighborInfo> Neighbors { get; set; } = new();

        /// <summary>
        /// Timestamp of last received packet from this node.
        /// </summary>
        public DateTime LastHeard { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Timestamp of last position update.
        /// </summary>
        public DateTime LastPositionUpdate { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Check if the node is stale based on the given timeout.
        /// </summary>
        public bool IsStale(TimeSpan staleTimeout)
        {
            return DateTime.UtcNow - LastPositionUpdate > staleTimeout;
        }

        /// <summary>
        /// Get the display name for this node (shortname preferred, then hex ID).
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(ShortName) ? ShortName : NodeIdHex;
    }

    /// <summary>
    /// Device role enumeration matching Meshtastic Config.DeviceConfig.Role.
    /// </summary>
    public enum DeviceRole
    {
        Client = 0,
        ClientMute = 1,
        Router = 2,
        RouterClient = 3,
        Repeater = 4,
        Tracker = 5,
        Sensor = 6,
        Tak = 7,
        ClientHidden = 8,
        LostAndFound = 9,
        TakTracker = 10
    }

    /// <summary>
    /// Device telemetry data (battery, uptime, channel utilization).
    /// </summary>
    public class TelemetryData
    {
        public int? BatteryLevel { get; set; }
        public float? Voltage { get; set; }
        public float? ChannelUtilization { get; set; }
        public float? AirUtilTx { get; set; }
        public uint? UptimeSeconds { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string UptimeFormatted
        {
            get
            {
                if (!UptimeSeconds.HasValue) return "Unknown";
                var ts = TimeSpan.FromSeconds(UptimeSeconds.Value);
                return $"{ts.Days}d {ts.Hours}h {ts.Minutes}m";
            }
        }
    }

    /// <summary>
    /// Environment sensor telemetry (temperature, humidity, pressure, IAQ).
    /// </summary>
    public class EnvironmentTelemetry
    {
        public float? Temperature { get; set; }
        public float? RelativeHumidity { get; set; }
        public float? BarometricPressure { get; set; }
        public float? GasResistance { get; set; }
        public float? Iaq { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Temperature in Fahrenheit.
        /// </summary>
        public float? TemperatureFahrenheit => Temperature.HasValue
            ? (Temperature.Value * 9 / 5) + 32
            : null;

        /// <summary>
        /// Barometric pressure in inHg.
        /// </summary>
        public float? PressureInHg => BarometricPressure.HasValue
            ? BarometricPressure.Value * 0.02953f
            : null;
    }

    /// <summary>
    /// Neighbor information with SNR.
    /// </summary>
    public class NeighborInfo
    {
        public uint NodeId { get; set; }
        public string NodeIdHex => $"!{NodeId:x8}";
        public string? NodeName { get; set; }
        public float Snr { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Interface for managing node state.
    /// </summary>
    public interface INodeStateManager
    {
        NodeState GetOrCreate(string connectionId, uint nodeId);
        NodeState? Get(string connectionId, uint nodeId);
        void Update(NodeState state);
        void Remove(string connectionId, uint nodeId);
        IEnumerable<NodeState> GetAll();
        IEnumerable<NodeState> GetByConnection(string connectionId);
        void CleanupStale(TimeSpan cleanupTimeout);
    }
}
