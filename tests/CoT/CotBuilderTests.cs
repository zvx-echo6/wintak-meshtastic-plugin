using System;
using System.Xml;
using FluentAssertions;
using WinTakMeshtasticPlugin.CoT;
using WinTakMeshtasticPlugin.Models;
using Xunit;

namespace WinTakMeshtasticPlugin.Tests.CoT
{
    public class CotBuilderTests
    {
        private readonly CotBuilder _builder;

        public CotBuilderTests()
        {
            _builder = new CotBuilder();
        }

        [Fact]
        public void BuildNodePli_ValidNode_GeneratesValidXml()
        {
            // Arrange
            var node = CreateTestNode();

            // Act
            var cotXml = _builder.BuildNodePli(node);

            // Assert
            cotXml.Should().NotBeNullOrEmpty();

            var doc = new XmlDocument();
            doc.LoadXml(cotXml); // Should not throw

            var eventNode = doc.SelectSingleNode("/event");
            eventNode.Should().NotBeNull();
        }

        [Fact]
        public void BuildNodePli_ContainsRequiredEventAttributes()
        {
            // Arrange
            var node = CreateTestNode();

            // Act
            var cotXml = _builder.BuildNodePli(node);
            var doc = new XmlDocument();
            doc.LoadXml(cotXml);

            // Assert
            var eventNode = doc.SelectSingleNode("/event");
            eventNode!.Attributes!["version"]!.Value.Should().Be("2.0");
            eventNode.Attributes["uid"]!.Value.Should().Contain("MESH-");
            eventNode.Attributes["type"]!.Value.Should().StartWith("a-");
            eventNode.Attributes["time"]!.Value.Should().NotBeNullOrEmpty();
            eventNode.Attributes["start"]!.Value.Should().NotBeNullOrEmpty();
            eventNode.Attributes["stale"]!.Value.Should().NotBeNullOrEmpty();
            eventNode.Attributes["how"]!.Value.Should().Be("m-g");
        }

        [Fact]
        public void BuildNodePli_ContainsCorrectPosition()
        {
            // Arrange
            var node = CreateTestNode();

            // Act
            var cotXml = _builder.BuildNodePli(node);
            var doc = new XmlDocument();
            doc.LoadXml(cotXml);

            // Assert
            var pointNode = doc.SelectSingleNode("/event/point");
            pointNode.Should().NotBeNull();
            pointNode!.Attributes!["lat"]!.Value.Should().Contain("42.75");
            pointNode.Attributes["lon"]!.Value.Should().Contain("-114.46");
        }

        [Fact]
        public void BuildNodePli_ContainsCallsign()
        {
            // Arrange
            var node = CreateTestNode();
            node.ShortName = "MYNODE";

            // Act
            var cotXml = _builder.BuildNodePli(node);
            var doc = new XmlDocument();
            doc.LoadXml(cotXml);

            // Assert
            var contactNode = doc.SelectSingleNode("/event/detail/contact");
            contactNode.Should().NotBeNull();
            contactNode!.Attributes!["callsign"]!.Value.Should().Be("MYNODE");
        }

        [Fact]
        public void BuildNodePli_DoesNotContainGroupElement()
        {
            // Arrange
            var node = CreateTestNode();
            node.ChannelsMembership.Add(1); // Channel 1 = Green

            // Act
            var cotXml = _builder.BuildNodePli(node);
            var doc = new XmlDocument();
            doc.LoadXml(cotXml);

            // Assert - __group intentionally omitted to allow custom icons
            // (team colors force colored dots that override usericon)
            var groupNode = doc.SelectSingleNode("/event/detail/__group");
            groupNode.Should().BeNull();
        }

        [Fact]
        public void BuildNodePli_ClientRole_UsesRadioUnitCotType()
        {
            // Arrange
            var node = CreateTestNode();
            node.Role = DeviceRole.Client;

            // Act
            var cotXml = _builder.BuildNodePli(node);
            var doc = new XmlDocument();
            doc.LoadXml(cotXml);

            // Assert - Client uses Radio unit type (verified in WinTAK CoTtypes.xml)
            var eventNode = doc.SelectSingleNode("/event");
            eventNode!.Attributes!["type"]!.Value.Should().Be("a-f-G-U-U-S-R");
        }

        [Fact]
        public void BuildNodePli_RouterRole_UsesTowerCotType()
        {
            // Arrange
            var node = CreateTestNode();
            node.Role = DeviceRole.Router;

            // Act
            var cotXml = _builder.BuildNodePli(node);
            var doc = new XmlDocument();
            doc.LoadXml(cotXml);

            // Assert - Router uses Tower type (verified in WinTAK CoTtypes.xml)
            var eventNode = doc.SelectSingleNode("/event");
            eventNode!.Attributes!["type"]!.Value.Should().Be("a-f-G-I-U-T-com-tow");
        }

        [Fact]
        public void BuildNodePli_TrackerRole_UsesSensorCotType()
        {
            // Arrange
            var node = CreateTestNode();
            node.Role = DeviceRole.Tracker;

            // Act
            var cotXml = _builder.BuildNodePli(node);
            var doc = new XmlDocument();
            doc.LoadXml(cotXml);

            // Assert
            var eventNode = doc.SelectSingleNode("/event");
            eventNode!.Attributes!["type"]!.Value.Should().Be("a-f-G-E-S");
        }

        [Fact]
        public void BuildNodePli_StaleTimeDefaultsTo30Minutes()
        {
            // Arrange
            var node = CreateTestNode();

            // Act
            var cotXml = _builder.BuildNodePli(node);
            var doc = new XmlDocument();
            doc.LoadXml(cotXml);

            // Assert
            var eventNode = doc.SelectSingleNode("/event");
            var staleTime = DateTime.Parse(eventNode!.Attributes!["stale"]!.Value);
            var startTime = DateTime.Parse(eventNode.Attributes["start"]!.Value);
            var diff = staleTime - startTime;
            diff.TotalMinutes.Should().BeApproximately(30, 1);
        }

        [Fact]
        public void BuildNodePli_CustomStaleTime_Applied()
        {
            // Arrange
            var node = CreateTestNode();

            // Act
            var cotXml = _builder.BuildNodePli(node, TimeSpan.FromMinutes(60));
            var doc = new XmlDocument();
            doc.LoadXml(cotXml);

            // Assert
            var eventNode = doc.SelectSingleNode("/event");
            var staleTime = DateTime.Parse(eventNode!.Attributes!["stale"]!.Value);
            var startTime = DateTime.Parse(eventNode.Attributes["start"]!.Value);
            var diff = staleTime - startTime;
            diff.TotalMinutes.Should().BeApproximately(60, 1);
        }

        [Fact]
        public void BuildNodePli_XmlEscapesShortName()
        {
            // Arrange
            var node = CreateTestNode();
            node.ShortName = "<script>alert('xss')</script>";

            // Act
            var cotXml = _builder.BuildNodePli(node);

            // Assert - should not contain unescaped tags
            cotXml.Should().NotContain("<script>");
            cotXml.Should().Contain("&lt;script&gt;");
        }

        [Fact]
        public void BuildNodePli_NullPosition_ThrowsArgumentException()
        {
            // Arrange
            var node = new NodeState
            {
                ConnectionId = "TEST",
                NodeId = 0x12345678,
                Latitude = null,
                Longitude = null
            };

            // Act & Assert
            Action act = () => _builder.BuildNodePli(node);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void BuildNodePli_NullNode_ThrowsArgumentNullException()
        {
            // Act & Assert
            Action act = () => _builder.BuildNodePli(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void BuildNodePli_ContainsMeshtasticMetadata()
        {
            // Arrange
            var node = CreateTestNode();
            node.LongName = "Test Long Name";
            node.HardwareModel = "TBEAM";
            node.FirmwareVersion = "2.3.0";

            // Act
            var cotXml = _builder.BuildNodePli(node);
            var doc = new XmlDocument();
            doc.LoadXml(cotXml);

            // Assert
            var meshNode = doc.SelectSingleNode("/event/detail/__meshtastic");
            meshNode.Should().NotBeNull();

            var nodeIdNode = doc.SelectSingleNode("/event/detail/__meshtastic/nodeId");
            nodeIdNode.Should().NotBeNull();
            nodeIdNode!.InnerText.Should().Contain("12345678");

            var longNameNode = doc.SelectSingleNode("/event/detail/__meshtastic/longName");
            longNameNode.Should().NotBeNull();
            longNameNode!.InnerText.Should().Be("Test Long Name");
        }

        [Theory]
        [InlineData(0, "Cyan")]
        [InlineData(1, "Green")]
        [InlineData(2, "Yellow")]
        [InlineData(3, "Orange")]
        [InlineData(4, "Red")]
        [InlineData(5, "Purple")]
        [InlineData(6, "White")]
        [InlineData(7, "Magenta")]
        [InlineData(8, "Cyan")] // Default for out of range
        public void GetTeamColorForChannel_MapsCorrectly(int channel, string expectedColor)
        {
            CotBuilder.GetTeamColorForChannel(channel).Should().Be(expectedColor);
        }

        [Theory]
        // Verified against WinTAK's C:\Program Files\WinTAK\Assets\cot\CoTtypes.xml
        [InlineData(DeviceRole.Client, "a-f-G-U-U-S-R")]       // Radio unit
        [InlineData(DeviceRole.ClientMute, "a-f-G-U")]         // Unit
        [InlineData(DeviceRole.ClientHidden, "a-f-G-U")]       // Unit
        [InlineData(DeviceRole.ClientBase, "a-f-G-I-c")]       // Civilian (lowercase c)
        [InlineData(DeviceRole.Router, "a-f-G-I-U-T-com-tow")] // Tower
        [InlineData(DeviceRole.RouterClient, "a-f-G-I-U-T-com-tow")] // Tower (ROUTER_LATE)
        [InlineData(DeviceRole.Repeater, "a-f-G-I-U-T-com-tow")] // Tower
        [InlineData(DeviceRole.Tracker, "a-f-G-E-S")]          // Sensor
        [InlineData(DeviceRole.LostAndFound, "a-f-G-E-S")]     // Sensor
        [InlineData(DeviceRole.Sensor, "a-f-G-E-S")]           // Sensor
        [InlineData(DeviceRole.Tak, "a-f-G-U-C")]              // Combat
        [InlineData(DeviceRole.TakTracker, "a-f-G-E-V")]       // Ground vehicle
        public void GetCotTypeForRole_MapsCorrectly(DeviceRole role, string expectedType)
        {
            CotBuilder.GetCotTypeForRole(role).Should().Be(expectedType);
        }

        [Fact]
        public void FormatCotTime_ReturnsIso8601WithZ()
        {
            // Arrange
            var time = new DateTime(2024, 6, 15, 12, 30, 45, 123, DateTimeKind.Utc);

            // Act
            var formatted = CotBuilder.FormatCotTime(time);

            // Assert
            formatted.Should().Be("2024-06-15T12:30:45.123Z");
        }

        [Fact]
        public void XmlEscape_EscapesSpecialCharacters()
        {
            CotBuilder.XmlEscape("<>&'\"").Should().Be("&lt;&gt;&amp;&apos;&quot;");
        }

        [Fact]
        public void XmlEscape_NullInput_ReturnsEmpty()
        {
            CotBuilder.XmlEscape(null).Should().BeEmpty();
        }

        [Fact]
        public void BuildGeoChat_GeneratesValidChatCot()
        {
            // Arrange
            var senderUid = "MESH-12345678";
            var senderCallsign = "NODE1";
            var message = "Hello World";

            // Act
            var cotXml = _builder.BuildGeoChat(senderUid, senderCallsign, message);

            // Assert
            var doc = new XmlDocument();
            doc.LoadXml(cotXml);

            var eventNode = doc.SelectSingleNode("/event");
            eventNode!.Attributes!["type"]!.Value.Should().Be("b-t-f");
            eventNode.Attributes["uid"]!.Value.Should().StartWith("GeoChat.");

            var remarksNode = doc.SelectSingleNode("/event/detail/remarks");
            remarksNode.Should().NotBeNull();
            remarksNode!.InnerText.Should().Be("Hello World");
        }

        [Fact]
        public void BuildNodePli_ShortNameMode_UsesShortNameAsCallsign()
        {
            // Arrange
            var builder = new CotBuilder { DisplayNameMode = DisplayNameMode.ShortName };
            var node = CreateTestNode();
            node.ShortName = "HnRp";
            node.LongName = "Hansen Repeater";

            // Act
            var cotXml = builder.BuildNodePli(node);
            var doc = new XmlDocument();
            doc.LoadXml(cotXml);

            // Assert
            var contactNode = doc.SelectSingleNode("/event/detail/contact");
            contactNode!.Attributes!["callsign"]!.Value.Should().Be("HnRp");

            var remarksNode = doc.SelectSingleNode("/event/detail/remarks");
            remarksNode!.InnerText.Should().Contain("Hansen Repeater");
            remarksNode.InnerText.Should().Contain("(HnRp)");
        }

        [Fact]
        public void BuildNodePli_LongNameMode_UsesLongNameAsCallsign()
        {
            // Arrange
            var builder = new CotBuilder { DisplayNameMode = DisplayNameMode.LongName };
            var node = CreateTestNode();
            node.ShortName = "HnRp";
            node.LongName = "Hansen Repeater";

            // Act
            var cotXml = builder.BuildNodePli(node);
            var doc = new XmlDocument();
            doc.LoadXml(cotXml);

            // Assert
            var contactNode = doc.SelectSingleNode("/event/detail/contact");
            contactNode!.Attributes!["callsign"]!.Value.Should().Be("Hansen Repeater");

            var remarksNode = doc.SelectSingleNode("/event/detail/remarks");
            remarksNode!.InnerText.Should().Contain("ShortName: HnRp");
        }

        [Fact]
        public void BuildNodePli_ShortNameMode_FallsBackToLongName()
        {
            // Arrange - no ShortName set
            var builder = new CotBuilder { DisplayNameMode = DisplayNameMode.ShortName };
            var node = CreateTestNode();
            node.ShortName = null;
            node.LongName = "Hansen Repeater";

            // Act
            var cotXml = builder.BuildNodePli(node);
            var doc = new XmlDocument();
            doc.LoadXml(cotXml);

            // Assert - should fall back to LongName
            var contactNode = doc.SelectSingleNode("/event/detail/contact");
            contactNode!.Attributes!["callsign"]!.Value.Should().Be("Hansen Repeater");
        }

        [Fact]
        public void BuildNodePli_LongNameMode_FallsBackToShortName()
        {
            // Arrange - no LongName set
            var builder = new CotBuilder { DisplayNameMode = DisplayNameMode.LongName };
            var node = CreateTestNode();
            node.ShortName = "HnRp";
            node.LongName = null;

            // Act
            var cotXml = builder.BuildNodePli(node);
            var doc = new XmlDocument();
            doc.LoadXml(cotXml);

            // Assert - should fall back to ShortName
            var contactNode = doc.SelectSingleNode("/event/detail/contact");
            contactNode!.Attributes!["callsign"]!.Value.Should().Be("HnRp");
        }

        private static NodeState CreateTestNode()
        {
            var node = new NodeState
            {
                ConnectionId = "TEST",
                NodeId = 0x12345678,
                ShortName = "TEST",
                LongName = "Test Node",
                Latitude = 42.75,
                Longitude = -114.46,
                Altitude = 1200,
                Role = DeviceRole.Client,
                LastHeard = DateTime.UtcNow,
                LastPositionUpdate = DateTime.UtcNow
            };
            node.ChannelsMembership.Add(0);
            return node;
        }
    }
}
