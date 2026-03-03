using System;
using System.Globalization;
using System.Xml;

namespace WinTakMeshtasticPlugin.Topology
{
    /// <summary>
    /// Service for drawing topology overlay lines on the WinTAK map.
    /// Uses CoT shape events (type u-d-f) to render polylines between nodes.
    /// </summary>
    public class TopologyOverlayService
    {
        private readonly Action<XmlDocument> _sendCotAction;

        /// <summary>
        /// Create a new topology overlay service.
        /// </summary>
        /// <param name="sendCotAction">Action to send CoT XML to WinTAK (typically ICotMessageSender.Send).</param>
        public TopologyOverlayService(Action<XmlDocument> sendCotAction)
        {
            _sendCotAction = sendCotAction ?? throw new ArgumentNullException(nameof(sendCotAction));
        }

        /// <summary>
        /// Draw a topology link line between two geographic points.
        /// </summary>
        /// <param name="uid">Unique identifier for this link.</param>
        /// <param name="lat1">Latitude of first point.</param>
        /// <param name="lon1">Longitude of first point.</param>
        /// <param name="lat2">Latitude of second point.</param>
        /// <param name="lon2">Longitude of second point.</param>
        /// <param name="strokeColor">Line color as ARGB signed int string.</param>
        /// <param name="strokeWeight">Line weight (1.0 - 4.0).</param>
        /// <param name="staleMinutes">Minutes until link expires (default 5).</param>
        /// <param name="remarks">Optional remarks to embed in the CoT event.</param>
        public void DrawTopologyLink(
            string uid,
            double lat1, double lon1,
            double lat2, double lon2,
            string strokeColor = null,
            double strokeWeight = 2.0,
            int staleMinutes = 5,
            string remarks = null)
        {
            // Center point is midpoint of line
            double centerLat = (lat1 + lat2) / 2;
            double centerLon = (lon1 + lon2) / 2;

            // Use default color if not specified
            strokeColor = strokeColor ?? TopologyLinkBuilder.DefaultColor;

            // XML-escape remarks if present
            string remarksElement = string.IsNullOrEmpty(remarks)
                ? "<remarks/>"
                : $"<remarks>{System.Security.SecurityElement.Escape(remarks)}</remarks>";

            var now = DateTime.UtcNow;
            string cotXml = string.Format(
                CultureInfo.InvariantCulture,
                @"<?xml version=""1.0"" encoding=""UTF-8""?>
<event version=""2.0"" uid=""{0}"" type=""u-d-f"" time=""{1:yyyy-MM-ddTHH:mm:ss.fffZ}"" start=""{1:yyyy-MM-ddTHH:mm:ss.fffZ}"" stale=""{2:yyyy-MM-ddTHH:mm:ss.fffZ}"" how=""h-e"">
  <point lat=""{3:F6}"" lon=""{4:F6}"" hae=""9999999.0"" ce=""9999999.0"" le=""9999999.0""/>
  <detail>
    <link point=""{5:F6},{6:F6}""/>
    <link point=""{7:F6},{8:F6}""/>
    <strokeColor value=""{9}""/>
    <strokeWeight value=""{10:F1}""/>
    <contact callsign=""{11}""/>
    <remarks/>
    <labels_on value=""false""/>
  </detail>
</event>",
                System.Security.SecurityElement.Escape(uid),
                now,
                now.AddMinutes(staleMinutes),
                centerLat,
                centerLon,
                lat1, lon1,
                lat2, lon2,
                strokeColor,
                strokeWeight,
                System.Security.SecurityElement.Escape(remarks ?? "Mesh Link"));

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(cotXml);

            // Diagnostic logging for topology line injection
            System.Diagnostics.Debug.WriteLine($"[Topology] INJECTING CoT for link {uid}");
            System.Diagnostics.Debug.WriteLine($"[Topology] Point1: {lat1},{lon1} Point2: {lat2},{lon2}");
            System.Diagnostics.Debug.WriteLine($"[Topology] Color: {strokeColor}, Weight: {strokeWeight}");
            System.Diagnostics.Debug.WriteLine($"[Topology] CoT XML:\n{cotXml}");

            _sendCotAction(xmlDoc);

            System.Diagnostics.Debug.WriteLine($"[Topology] Drew link {uid}");
        }

        /// <summary>
        /// Draw a topology link between two nodes using SNR-based styling.
        /// </summary>
        /// <param name="nodeIdA">First node ID.</param>
        /// <param name="nodeIdB">Second node ID.</param>
        /// <param name="nodeNameA">Display name of first node.</param>
        /// <param name="nodeNameB">Display name of second node.</param>
        /// <param name="lat1">Latitude of first node.</param>
        /// <param name="lon1">Longitude of first node.</param>
        /// <param name="lat2">Latitude of second node.</param>
        /// <param name="lon2">Longitude of second node.</param>
        /// <param name="snrDb">Signal-to-noise ratio in dB.</param>
        /// <param name="staleMinutes">Minutes until link expires.</param>
        public void DrawTopologyLinkWithSnr(
            uint nodeIdA, uint nodeIdB,
            string nodeNameA, string nodeNameB,
            double lat1, double lon1,
            double lat2, double lon2,
            double snrDb,
            int staleMinutes = 5)
        {
            string uid = TopologyLinkBuilder.BuildLinkUid(nodeIdA, nodeIdB);
            string color = TopologyLinkBuilder.GetSnrColor(snrDb);
            double weight = TopologyLinkBuilder.GetLineWeight(snrDb);

            // Build descriptive callsign like "AIDA→SSL 3.0dB"
            string callsign = string.Format(CultureInfo.InvariantCulture,
                "{0}→{1} {2:F1}dB",
                nodeNameA ?? nodeIdA.ToString("X4"),
                nodeNameB ?? nodeIdB.ToString("X4"),
                snrDb);

            DrawTopologyLink(uid, lat1, lon1, lat2, lon2, color, weight, staleMinutes, callsign);
        }

        /// <summary>
        /// Remove a topology link by making it immediately stale.
        /// </summary>
        /// <param name="uid">Unique identifier of the link to remove.</param>
        public void RemoveTopologyLink(string uid)
        {
            var now = DateTime.UtcNow;
            string cotXml = string.Format(
                CultureInfo.InvariantCulture,
                @"<?xml version=""1.0"" encoding=""UTF-8""?>
<event version=""2.0""
       uid=""{0}""
       type=""u-d-f""
       time=""{1:yyyy-MM-ddTHH:mm:ss.fffZ}""
       start=""{1:yyyy-MM-ddTHH:mm:ss.fffZ}""
       stale=""{2:yyyy-MM-ddTHH:mm:ss.fffZ}""
       how=""m-g"">
    <point lat=""0"" lon=""0"" hae=""0"" ce=""9999999"" le=""9999999""/>
    <detail/>
</event>",
                System.Security.SecurityElement.Escape(uid),
                now,
                now.AddSeconds(-1)); // Stale time in the past = immediate removal

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(cotXml);
            _sendCotAction(xmlDoc);

            System.Diagnostics.Debug.WriteLine($"[Topology] Removed link {uid}");
        }

        /// <summary>
        /// Remove a topology link between two nodes.
        /// </summary>
        /// <param name="nodeIdA">First node ID.</param>
        /// <param name="nodeIdB">Second node ID.</param>
        public void RemoveTopologyLink(uint nodeIdA, uint nodeIdB)
        {
            string uid = TopologyLinkBuilder.BuildLinkUid(nodeIdA, nodeIdB);
            RemoveTopologyLink(uid);
        }
    }
}
