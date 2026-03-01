using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinTakMeshtasticPlugin.Models
{
    /// <summary>
    /// Plugin settings that persist across WinTAK restarts.
    /// Stored in %appdata%\wintak\plugins\WinTakMeshtasticPlugin\settings.json
    /// </summary>
    public class PluginSettings
    {
        /// <summary>
        /// Settings file path.
        /// </summary>
        public static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "wintak", "plugins", "WinTakMeshtasticPlugin", "settings.json");

        /// <summary>
        /// Meshtastic node hostname or IP address.
        /// </summary>
        public string Hostname { get; set; } = "localhost";

        /// <summary>
        /// Meshtastic node TCP port.
        /// </summary>
        public int Port { get; set; } = 4403;

        /// <summary>
        /// Reconnect interval in seconds (5-60).
        /// </summary>
        public int ReconnectIntervalSeconds { get; set; } = 15;

        /// <summary>
        /// Auto-connect on WinTAK startup.
        /// </summary>
        public bool AutoConnect { get; set; } = false;

        /// <summary>
        /// Selected outbound channel index.
        /// </summary>
        public int SelectedOutboundChannel { get; set; } = 0;

        /// <summary>
        /// Channel receive filter settings.
        /// Key = channel index, Value = enabled state.
        /// </summary>
        public Dictionary<int, bool> ChannelReceiveEnabled { get; set; } = new();

        /// <summary>
        /// Enable outbound PLI to mesh.
        /// </summary>
        public bool OutboundPliEnabled { get; set; } = false;

        /// <summary>
        /// Outbound PLI interval in seconds.
        /// </summary>
        public int OutboundPliIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// Show topology overlay lines.
        /// </summary>
        public bool TopologyOverlayEnabled { get; set; } = true;

        /// <summary>
        /// Stale node cleanup timeout in hours.
        /// </summary>
        public int StaleNodeTimeoutHours { get; set; } = 24;

        /// <summary>
        /// Load settings from disk.
        /// </summary>
        public static PluginSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<PluginSettings>(json, JsonOptions);
                    if (settings != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[Settings] Loaded settings from disk");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Settings] Failed to load settings: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine("[Settings] Using default settings");
            return new PluginSettings();
        }

        /// <summary>
        /// Save settings to disk.
        /// </summary>
        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(this, JsonOptions);
                File.WriteAllText(SettingsPath, json);

                System.Diagnostics.Debug.WriteLine("[Settings] Saved settings to disk");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Settings] Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Validate and clamp settings to valid ranges.
        /// </summary>
        public void Validate()
        {
            Port = Math.Clamp(Port, 1, 65535);
            ReconnectIntervalSeconds = Math.Clamp(ReconnectIntervalSeconds, 5, 60);
            SelectedOutboundChannel = Math.Clamp(SelectedOutboundChannel, 0, 7);
            OutboundPliIntervalSeconds = Math.Clamp(OutboundPliIntervalSeconds, 10, 600);
            StaleNodeTimeoutHours = Math.Clamp(StaleNodeTimeoutHours, 1, 168);
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}
