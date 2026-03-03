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
<event version=""2.0""
       uid=""{0}""
       type=""u-d-f""
       time=""{1:yyyy-MM-ddTHH:mm:ss.fffZ}""
       start=""{1:yyyy-MM-ddTHH:mm:ss.fffZ}""
       stale=""{2:yyyy-MM-ddTHH:mm:ss.fffZ}""
       how=""m-g"">
    <point lat=""{3}"" lon=""{4}"" hae=""0"" ce=""9999999"" le=""9999999""/>
    <detail>
        <link point=""{5},{6}""/>
        <link point=""{7},{8}""/>
        <strokeColor value=""{9}""/>
        <strokeWeight value=""{10}""/>
        <fillColor value=""0""/>
        <contact callsign=""Mesh Link""/>
        {11}
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
                remarksElement);

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(cotXml);
            _sendCotAction(xmlDoc);

            System.Diagnostics.Debug.WriteLine($"[Topology] Drew link {uid}");
        }

        /// <summary>
        /// Draw a topology link between two nodes using SNR-based styling.
        /// </summary>
        /// <param name="nodeIdA">First node ID.</param>
        /// <param name="nodeIdB">Second node ID.</param>
        /// <param name="lat1">Latitude of first node.</param>
        /// <param name="lon1">Longitude of first node.</param>
        /// <param name="lat2">Latitude of second node.</param>
        /// <param name="lon2">Longitude of second node.</param>
        /// <param name="snrDb">Signal-to-noise ratio in dB.</param>
        /// <param name="staleMinutes">Minutes until link expires.</param>
        public void DrawTopologyLinkWithSnr(
            uint nodeIdA, uint nodeIdB,
            double lat1, double lon1,
            double lat2, double lon2,
            double snrDb,
            int staleMinutes = 5)
        {
            string uid = TopologyLinkBuilder.BuildLinkUid(nodeIdA, nodeIdB);
            string color = TopologyLinkBuilder.GetSnrColor(snrDb);
            double weight = TopologyLinkBuilder.GetLineWeight(snrDb);
            string remarks = TopologyLinkBuilder.FormatSnrRemarks(snrDb);

            DrawTopologyLink(uid, lat1, lon1, lat2, lon2, color, weight, staleMinutes, remarks);
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
