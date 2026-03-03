using System;
using System.Threading.Tasks;
using Google.Protobuf;
using Meshtastic.Protobufs;
using WinTakMeshtasticPlugin.Models;

namespace WinTakMeshtasticPlugin.Handlers
{
    /// <summary>
    /// Handler for TELEMETRY_APP (portnum 67) packets.
    /// Deserializes telemetry data (device metrics, environment, power) and updates node state.
    /// </summary>
    public class TelemetryHandler : IPacketHandler
    {
        /// <inheritdoc />
        public PortNum HandledPortNum => PortNum.TelemetryApp;

        /// <inheritdoc />
        public Task<PacketHandlerResult> HandleAsync(MeshPacket packet, PacketHandlerContext context)
        {
            if (packet.Decoded?.Payload == null || packet.Decoded.Payload.IsEmpty)
            {
                System.Diagnostics.Debug.WriteLine("[TelemetryHandler] Empty payload, skipping");
                return Task.FromResult<PacketHandlerResult>(null);
            }

            Telemetry telemetry;
            try
            {
                telemetry = Telemetry.Parser.ParseFrom(packet.Decoded.Payload);
            }
            catch (InvalidProtocolBufferException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TelemetryHandler] Warning: Failed to parse Telemetry: {ex.Message}");
                return Task.FromResult<PacketHandlerResult>(null);
            }

            // Get node state manager
            if (context.NodeStateManager == null)
            {
                System.Diagnostics.Debug.WriteLine("[TelemetryHandler] Warning: No NodeStateManager in context");
                return Task.FromResult<PacketHandlerResult>(null);
            }

            var nodeState = context.NodeStateManager.GetOrCreate(context.ConnectionId, packet.From);
            nodeState.LastHeard = DateTime.UtcNow;

            string debugMessage = "";

            // Handle different telemetry variants
            switch (telemetry.VariantCase)
            {
                case Telemetry.VariantOneofCase.DeviceMetrics:
                    HandleDeviceMetrics(nodeState, telemetry.DeviceMetrics);
                    debugMessage = FormatDeviceMetricsDebug(nodeState, telemetry.DeviceMetrics);
                    break;

                case Telemetry.VariantOneofCase.EnvironmentMetrics:
                    HandleEnvironmentMetrics(nodeState, telemetry.EnvironmentMetrics);
                    debugMessage = FormatEnvironmentMetricsDebug(nodeState, telemetry.EnvironmentMetrics);
                    break;

                case Telemetry.VariantOneofCase.PowerMetrics:
                    HandlePowerMetrics(nodeState, telemetry.PowerMetrics);
                    debugMessage = $"Power metrics from {nodeState.DisplayName}";
                    break;

                case Telemetry.VariantOneofCase.LocalStats:
                    // Local stats from the connected node - could be used for diagnostics
                    debugMessage = $"Local stats from {nodeState.DisplayName}";
                    break;

                case Telemetry.VariantOneofCase.AirQualityMetrics:
                    HandleAirQualityMetrics(nodeState, telemetry.AirQualityMetrics);
                    debugMessage = $"Air quality metrics from {nodeState.DisplayName}";
                    break;

                default:
                    System.Diagnostics.Debug.WriteLine(
                        $"[TelemetryHandler] Unknown telemetry variant: {telemetry.VariantCase}");
                    return Task.FromResult<PacketHandlerResult>(null);
            }

            // Save updated state
            context.NodeStateManager.Update(nodeState);

            System.Diagnostics.Debug.WriteLine($"[TelemetryHandler] {debugMessage}");

            // If node has position, re-inject CoT to update remarks with telemetry data
            string cotXml = null;
            if (nodeState.Latitude.HasValue && nodeState.Longitude.HasValue && context.CotBuilder != null)
            {
                cotXml = context.CotBuilder.BuildNodePli(nodeState);
                System.Diagnostics.Debug.WriteLine($"[TelemetryHandler] Re-injecting CoT for {nodeState.DisplayName} with updated telemetry");
            }

            return Task.FromResult(new PacketHandlerResult
            {
                CotXml = cotXml,
                UpdatesNodeState = true,
                DebugMessage = debugMessage
            });
        }

        private void HandleDeviceMetrics(NodeState nodeState, DeviceMetrics metrics)
        {
            if (nodeState.DeviceTelemetry == null)
            {
                nodeState.DeviceTelemetry = new TelemetryData();
            }

            var telem = nodeState.DeviceTelemetry;
            telem.Timestamp = DateTime.UtcNow;

            // Battery level (0-100%)
            if (metrics.HasBatteryLevel)
            {
                telem.BatteryLevel = (int)metrics.BatteryLevel;
            }

            // Voltage
            if (metrics.HasVoltage)
            {
                telem.Voltage = metrics.Voltage;
            }

            // Channel utilization (0-100%)
            if (metrics.HasChannelUtilization)
            {
                telem.ChannelUtilization = metrics.ChannelUtilization;
            }

            // Air utilization TX (0-100%)
            if (metrics.HasAirUtilTx)
            {
                telem.AirUtilTx = metrics.AirUtilTx;
            }

            // Uptime in seconds
            if (metrics.HasUptimeSeconds)
            {
                telem.UptimeSeconds = metrics.UptimeSeconds;
            }
        }

        private void HandleEnvironmentMetrics(NodeState nodeState, EnvironmentMetrics metrics)
        {
            if (nodeState.EnvironmentTelemetry == null)
            {
                nodeState.EnvironmentTelemetry = new EnvironmentTelemetry();
            }

            var env = nodeState.EnvironmentTelemetry;
            env.Timestamp = DateTime.UtcNow;

            // Temperature (Celsius)
            if (metrics.HasTemperature)
            {
                env.Temperature = metrics.Temperature;
            }

            // Relative humidity (%)
            if (metrics.HasRelativeHumidity)
            {
                env.RelativeHumidity = metrics.RelativeHumidity;
            }

            // Barometric pressure (hPa)
            if (metrics.HasBarometricPressure)
            {
                env.BarometricPressure = metrics.BarometricPressure;
            }

            // Gas resistance (Ohms) - only on BME680/688
            if (metrics.HasGasResistance)
            {
                env.GasResistance = metrics.GasResistance;
            }

            // Indoor Air Quality index - only on BME680/688
            if (metrics.HasIaq)
            {
                env.Iaq = metrics.Iaq;
            }
        }

        private void HandlePowerMetrics(NodeState nodeState, PowerMetrics metrics)
        {
            // Power metrics contain channel voltages/currents from INA sensors
            // For now, store in device telemetry voltage field if ch1 is present
            if (nodeState.DeviceTelemetry == null)
            {
                nodeState.DeviceTelemetry = new TelemetryData();
            }

            if (metrics.HasCh1Voltage)
            {
                nodeState.DeviceTelemetry.Voltage = metrics.Ch1Voltage;
            }

            nodeState.DeviceTelemetry.Timestamp = DateTime.UtcNow;
        }

        private void HandleAirQualityMetrics(NodeState nodeState, AirQualityMetrics metrics)
        {
            // Air quality metrics (PM sensors) - store in environment telemetry for now
            // Could be expanded to a separate AirQualityTelemetry class
            if (nodeState.EnvironmentTelemetry == null)
            {
                nodeState.EnvironmentTelemetry = new EnvironmentTelemetry();
            }

            nodeState.EnvironmentTelemetry.Timestamp = DateTime.UtcNow;
        }

        private string FormatDeviceMetricsDebug(NodeState nodeState, DeviceMetrics metrics)
        {
            var parts = new System.Collections.Generic.List<string>();
            parts.Add($"Device metrics from {nodeState.DisplayName}:");

            if (metrics.HasBatteryLevel)
                parts.Add($"Battery={metrics.BatteryLevel}%");
            if (metrics.HasVoltage)
                parts.Add($"Voltage={metrics.Voltage:F2}V");
            if (metrics.HasChannelUtilization)
                parts.Add($"ChUtil={metrics.ChannelUtilization:F1}%");
            if (metrics.HasAirUtilTx)
                parts.Add($"AirTx={metrics.AirUtilTx:F1}%");
            if (metrics.HasUptimeSeconds)
                parts.Add($"Uptime={TimeSpan.FromSeconds(metrics.UptimeSeconds)}");

            return string.Join(" ", parts);
        }

        private string FormatEnvironmentMetricsDebug(NodeState nodeState, EnvironmentMetrics metrics)
        {
            var parts = new System.Collections.Generic.List<string>();
            parts.Add($"Environment metrics from {nodeState.DisplayName}:");

            if (metrics.HasTemperature)
                parts.Add($"Temp={metrics.Temperature:F1}°C");
            if (metrics.HasRelativeHumidity)
                parts.Add($"Humidity={metrics.RelativeHumidity:F1}%");
            if (metrics.HasBarometricPressure)
                parts.Add($"Pressure={metrics.BarometricPressure:F1}hPa");
            if (metrics.HasIaq)
                parts.Add($"IAQ={metrics.Iaq}");

            return string.Join(" ", parts);
        }
    }
}
