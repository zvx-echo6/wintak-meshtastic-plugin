using System;
using Meshtastic.Protobufs;
using WinTakMeshtasticPlugin.Models;

namespace WinTakMeshtasticPlugin.Handlers
{
    /// <summary>
    /// Handles channel configuration received from FromRadio.
    /// This is not a portnum handler - it processes FromRadio.Channel payloads.
    /// </summary>
    public class ChannelHandler
    {
        private readonly IChannelManager _channelManager;

        public ChannelHandler(IChannelManager channelManager)
        {
            _channelManager = channelManager ?? throw new ArgumentNullException(nameof(channelManager));
        }

        /// <summary>
        /// Process a Channel message from FromRadio.
        /// </summary>
        public void HandleChannel(Channel channel)
        {
            if (channel == null) return;

            var role = MapChannelRole(channel.Role);

            // Check if PSK is present (but NEVER store the value - SEC-04)
            bool hasPsk = channel.Settings?.Psk != null && !channel.Settings.Psk.IsEmpty;

            // Get channel name from settings
            string? name = channel.Settings?.Name;

            _channelManager.UpdateChannel(channel.Index, name, role, hasPsk);

            System.Diagnostics.Debug.WriteLine(
                $"[ChannelHandler] Received channel {channel.Index}: name=\"{name}\", role={role}");
        }

        /// <summary>
        /// Map Meshtastic Channel.Role to our ChannelRole enum.
        /// </summary>
        private static ChannelRole MapChannelRole(Channel.Types.Role protoRole)
        {
            return protoRole switch
            {
                Channel.Types.Role.Primary => ChannelRole.Primary,
                Channel.Types.Role.Secondary => ChannelRole.Secondary,
                Channel.Types.Role.Disabled => ChannelRole.Disabled,
                _ => ChannelRole.Disabled
            };
        }
    }
}
