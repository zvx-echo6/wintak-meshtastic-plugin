using System.Collections.Generic;

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
        /// Whether this is the admin channel.
        /// Admin channel requires special handling per SEC-02.
        /// </summary>
        public bool IsAdmin { get; set; }

        /// <summary>
        /// Whether the channel is enabled for receive in the plugin.
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
    /// Manages channel state for a connection.
    /// </summary>
    public class ChannelManager
    {
        private readonly Dictionary<int, ChannelState> _channels = new();

        /// <summary>
        /// Update channel information from Meshtastic config.
        /// </summary>
        public void UpdateChannel(int index, string? name, bool isAdmin = false)
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
            channel.IsAdmin = isAdmin;

            // Admin channel excluded from transmit by default (SEC-02)
            if (isAdmin)
            {
                channel.TransmitEnabled = false;
            }
        }

        /// <summary>
        /// Get a channel by index.
        /// </summary>
        public ChannelState? GetChannel(int index)
        {
            return _channels.TryGetValue(index, out var channel) ? channel : null;
        }

        /// <summary>
        /// Get all known channels.
        /// </summary>
        public IEnumerable<ChannelState> GetAllChannels() => _channels.Values;

        /// <summary>
        /// Get channels available for transmit (non-admin or explicitly enabled).
        /// </summary>
        public IEnumerable<ChannelState> GetTransmitChannels()
        {
            foreach (var channel in _channels.Values)
            {
                if (channel.TransmitEnabled)
                {
                    yield return channel;
                }
            }
        }
    }
}
