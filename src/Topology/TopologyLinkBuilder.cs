using System;
using System.Globalization;

namespace WinTakMeshtasticPlugin.Topology
{
    /// <summary>
    /// Helper class for building topology link styling based on SNR quality.
    /// </summary>
    public static class TopologyLinkBuilder
    {
        // ARGB color values as signed 32-bit integers
        private const string ColorGreen = "-16711936";   // 0xFF00FF00
        private const string ColorYellow = "-256";        // 0xFFFFFF00
        private const string ColorOrange = "-32768";      // 0xFFFF8000
        private const string ColorRed = "-65536";         // 0xFFFF0000
        private const string ColorCyan = "-16711681";     // 0xFF00FFFF

        /// <summary>
        /// Get line color based on SNR quality.
        /// Green = excellent (> -5 dB)
        /// Yellow = good (-5 to -10 dB)
        /// Orange = marginal (-10 to -15 dB)
        /// Red = poor (&lt; -15 dB)
        /// </summary>
        /// <param name="snrDb">Signal-to-noise ratio in dB.</param>
        /// <returns>ARGB color value as string.</returns>
        public static string GetSnrColor(double snrDb)
        {
            if (snrDb > -5) return ColorGreen;
            if (snrDb > -10) return ColorYellow;
            if (snrDb > -15) return ColorOrange;
            return ColorRed;
        }

        /// <summary>
        /// Get line weight based on link quality.
        /// Stronger signals get thicker lines.
        /// </summary>
        /// <param name="snrDb">Signal-to-noise ratio in dB.</param>
        /// <returns>Line weight (1.0 - 4.0).</returns>
        public static double GetLineWeight(double snrDb)
        {
            if (snrDb > -5) return 4.0;
            if (snrDb > -10) return 3.0;
            if (snrDb > -15) return 2.0;
            return 1.0;
        }

        /// <summary>
        /// Build a deterministic UID for a topology link between two nodes.
        /// Node IDs are sorted to ensure consistent UID regardless of direction.
        /// </summary>
        /// <param name="nodeIdA">First node ID.</param>
        /// <param name="nodeIdB">Second node ID.</param>
        /// <returns>Link UID in format MESH-LINK-{nodeA:X8}-{nodeB:X8}.</returns>
        public static string BuildLinkUid(uint nodeIdA, uint nodeIdB)
        {
            uint nodeA = Math.Min(nodeIdA, nodeIdB);
            uint nodeB = Math.Max(nodeIdA, nodeIdB);
            return $"MESH-LINK-{nodeA:X8}-{nodeB:X8}";
        }

        /// <summary>
        /// Get default link color (cyan) when SNR is unknown.
        /// </summary>
        public static string DefaultColor => ColorCyan;

        /// <summary>
        /// Get default line weight when SNR is unknown.
        /// </summary>
        public static double DefaultWeight => 2.0;

        /// <summary>
        /// Format SNR value for display in link remarks.
        /// </summary>
        /// <param name="snrDb">Signal-to-noise ratio in dB.</param>
        /// <returns>Formatted string like "SNR: -10.5 dB".</returns>
        public static string FormatSnrRemarks(double snrDb)
        {
            return string.Format(CultureInfo.InvariantCulture, "SNR: {0:F1} dB", snrDb);
        }
    }
}
