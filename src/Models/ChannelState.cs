using System;
using System.Collections.Generic;
using System.Linq;

namespace WinTakMeshtasticPlugin.Models
{
    /// <summary>
    /// Represents a Meshtastic channel's state and configuration.
    /// </summary>
    public class ChannelState
    {
        /// <summary>
        /// Channel index (0-7 typically).
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Channel name from configuration.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Display name (name if set, otherwise "Channel N").
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(Name) ? Name : $"Channel {Index}";

        /// <summary>
        /// Channel role: Primary, Secondary, or Disabled.
        /// </summary>
        public ChannelRole Role { get; set; } = ChannelRole.Disabled;

        /// <summary>
        /// Whether this is the admin channel.
        /// Admin channel requires special handling per SEC-02.
        /// Detected by name containing "admin" (case-insensitive).
        /// </summary>
        public bool IsAdmin { get; set; }

        /// <summary>
        /// Whether the channel has a PSK configured.
        /// Note: We NEVER store or log the PSK value itself (SEC-04).
        /// </summary>
        public bool HasPsk { get; set; }

        /// <summary>
        /// Whether the channel is enabled for receive in the plugin (CHN-03).
        /// </summary>
        public bool ReceiveEnabled { get; set; } = true;

        /// <summary>
        /// Whether this channel is available for outbound messages.
        /// Admin channel is excluded by default (SEC-02).
        /// </summary>
        public bool TransmitEnabled { get; set; } = true;

        /// <summary>
        /// TAK team color assigned to this channel.
        /// Default mapping: Ch0=Cyan, Ch1=Green, Ch2=Yellow, Ch3=Orange,
        /// Ch4=Red, Ch5=Purple, Ch6=White, Ch7=Magenta
        /// </summary>
        public string TeamColor { get; set; } = "Cyan";

        /// <summary>
        /// When the channel config was last updated.
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Get the default team color for a channel index.
        /// </summary>
        public static string GetDefaultTeamColor(int channelIndex)
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
    }

    /// <summary>
    /// Channel role enumeration matching Meshtastic Channel.Role.
    /// </summary>
    public enum ChannelRole
    {
        Disabled = 0,
        Primary = 1,
        Secondary = 2
    }

    /// <summary>
    /// Interface for channel management.
    /// </summary>
    public interface IChannelManager
    {
        /// <summary>
        /// Update channel information from Meshtastic config.
        /// </summary>
        void UpdateChannel(int index, string? name, ChannelRole role, bool hasPsk);

        /// <summary>
        /// Get a channel by index.
        /// </summary>
        ChannelState? GetChannel(int index);

        /// <summary>
        /// Get all known channels.
        /// </summary>
        IEnumerable<ChannelState> GetAllChannels();

        /// <summary>
        /// Get channels available for transmit.
        /// </summary>
        IEnumerable<ChannelState> GetTransmitChannels();

        /// <summary>
        /// Get channels enabled for receive.
        /// </summary>
        IEnumerable<ChannelState> GetReceiveChannels();

        /// <summary>
        /// Currently selected outbound channel index.
        /// </summary>
        int SelectedOutboundChannel { get; set; }

        /// <summary>
        /// Clear all channel data.
        /// </summary>
        void Clear();

        /// <summary>
        /// Event raised when channel configuration changes.
        /// </summary>
        event EventHandler<ChannelChangedEventArgs>? ChannelChanged;
    }

    /// <summary>
    /// Manages channel state for a connection.
    /// </summary>
    public class ChannelManager : IChannelManager
    {
        private readonly Dictionary<int, ChannelState> _channels = new();
        private readonly object _lock = new();
        private int _selectedOutboundChannel = 0;

        /// <summary>
        /// Event raised when channel configuration changes.
        /// </summary>
        public event EventHandler<ChannelChangedEventArgs>? ChannelChanged;

        /// <summary>
        /// Currently selected outbound channel index.
        /// Default is channel 0 (CHN-06).
        /// </summary>
        public int SelectedOutboundChannel
        {
            get => _selectedOutboundChannel;
            set
            {
                lock (_lock)
                {
                    var channel = GetChannel(value);
                    if (channel != null && channel.TransmitEnabled)
                    {
                        _selectedOutboundChannel = value;
                    }
                }
            }
        }

        /// <summary>
        /// Update channel information from Meshtastic config.
        /// </summary>
        public void UpdateChannel(int index, string? name, ChannelRole role, bool hasPsk)
        {
            lock (_lock)
            {
                if (!_channels.TryGetValue(index, out var channel))
                {
                    channel = new ChannelState
                    {
                        Index = index,
                        TeamColor = ChannelState.GetDefaultTeamColor(index)
                    };
                    _channels[index] = channel;
                }

                channel.Name = name;
                channel.Role = role;
                channel.HasPsk = hasPsk;
                channel.LastUpdated = DateTime.UtcNow;

                // Detect admin channel by name (SEC-02)
                channel.IsAdmin = !string.IsNullOrEmpty(name) &&
                    name.IndexOf("admin", StringComparison.OrdinalIgnoreCase) >= 0;

                // Admin channel excluded from transmit by default (SEC-02)
                if (channel.IsAdmin)
                {
                    channel.TransmitEnabled = false;
                }

                // Disabled channels can't transmit
                if (role == ChannelRole.Disabled)
                {
                    channel.TransmitEnabled = false;
                    channel.ReceiveEnabled = false;
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[ChannelManager] Updated channel {index}: name=\"{name}\", role={role}, " +
                    $"isAdmin={channel.IsAdmin}, hasPsk={hasPsk}");

                ChannelChanged?.Invoke(this, new ChannelChangedEventArgs(channel));
            }
        }

        /// <summary>
        /// Set receive enabled state for a channel (CHN-03).
        /// </summary>
        public void SetReceiveEnabled(int index, bool enabled)
        {
            lock (_lock)
            {
                var channel = GetChannel(index);
                if (channel != null && channel.Role != ChannelRole.Disabled)
                {
                    channel.ReceiveEnabled = enabled;
                    ChannelChanged?.Invoke(this, new ChannelChangedEventArgs(channel));
                }
            }
        }

        /// <summary>
        /// Set transmit enabled state for a channel.
        /// Cannot enable transmit on admin channel unless explicitly overridden.
        /// </summary>
        public void SetTransmitEnabled(int index, bool enabled)
        {
            lock (_lock)
            {
                var channel = GetChannel(index);
                if (channel != null && channel.Role != ChannelRole.Disabled)
                {
                    // Don't allow enabling transmit on admin channel (SEC-02)
                    if (enabled && channel.IsAdmin)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[ChannelManager] Warning: Cannot enable transmit on admin channel {index}");
                        return;
                    }

                    channel.TransmitEnabled = enabled;
                    ChannelChanged?.Invoke(this, new ChannelChangedEventArgs(channel));
                }
            }
        }

        /// <summary>
        /// Get a channel by index.
        /// </summary>
        public ChannelState? GetChannel(int index)
        {
            lock (_lock)
            {
                return _channels.TryGetValue(index, out var channel) ? channel : null;
            }
        }

        /// <summary>
        /// Get all known channels.
        /// </summary>
        public IEnumerable<ChannelState> GetAllChannels()
        {
            lock (_lock)
            {
                return _channels.Values.OrderBy(c => c.Index).ToList();
            }
        }

        /// <summary>
        /// Get channels available for transmit (non-admin and enabled).
        /// Admin channel is excluded by default (SEC-02).
        /// </summary>
        public IEnumerable<ChannelState> GetTransmitChannels()
        {
            lock (_lock)
            {
                return _channels.Values
                    .Where(c => c.TransmitEnabled && c.Role != ChannelRole.Disabled)
                    .OrderBy(c => c.Index)
                    .ToList();
            }
        }

        /// <summary>
        /// Get channels enabled for receive (CHN-03).
        /// </summary>
        public IEnumerable<ChannelState> GetReceiveChannels()
        {
            lock (_lock)
            {
                return _channels.Values
                    .Where(c => c.ReceiveEnabled && c.Role != ChannelRole.Disabled)
                    .OrderBy(c => c.Index)
                    .ToList();
            }
        }

        /// <summary>
        /// Check if a channel is enabled for receive.
        /// Used to filter incoming messages (CHN-03).
        /// </summary>
        public bool IsReceiveEnabled(int index)
        {
            lock (_lock)
            {
                var channel = GetChannel(index);
                return channel?.ReceiveEnabled ?? true; // Default to enabled if unknown
            }
        }

        /// <summary>
        /// Get the primary channel (first enabled channel).
        /// </summary>
        public ChannelState? GetPrimaryChannel()
        {
            lock (_lock)
            {
                return _channels.Values
                    .FirstOrDefault(c => c.Role == ChannelRole.Primary);
            }
        }

        /// <summary>
        /// Clear all channel data.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _channels.Clear();
                _selectedOutboundChannel = 0;
            }
        }

        /// <summary>
        /// Get the count of known channels.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _channels.Count;
                }
            }
        }
    }

    /// <summary>
    /// Event args for channel change events.
    /// </summary>
    public class ChannelChangedEventArgs : EventArgs
    {
        public ChannelState Channel { get; }

        public ChannelChangedEventArgs(ChannelState channel)
        {
            Channel = channel;
        }
    }
}
