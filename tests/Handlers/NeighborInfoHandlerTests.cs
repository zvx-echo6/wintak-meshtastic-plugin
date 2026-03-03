using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Google.Protobuf;
using Meshtastic.Protobufs;
using Moq;
using WinTakMeshtasticPlugin.CoT;
using WinTakMeshtasticPlugin.Handlers;
using WinTakMeshtasticPlugin.Models;
using Xunit;

namespace WinTakMeshtasticPlugin.Tests.Handlers
{
    public class NeighborInfoHandlerTests
    {
        private readonly NeighborInfoHandler _handler;
        private readonly Mock<INodeStateManager> _nodeStateManagerMock;
        private readonly Mock<ICotBuilder> _cotBuilderMock;
        private readonly PacketHandlerContext _context;

        public NeighborInfoHandlerTests()
        {
            _handler = new NeighborInfoHandler();
            _nodeStateManagerMock = new Mock<INodeStateManager>();
            _cotBuilderMock = new Mock<ICotBuilder>();

            _context = new PacketHandlerContext
            {
                ConnectionId = "TEST",
                NodeStateManager = _nodeStateManagerMock.Object,
                CotBuilder = _cotBuilderMock.Object
            };
        }

        [Fact]
        public void HandledPortNum_ShouldBeNeighborinfoApp()
        {
            _handler.HandledPortNum.Should().Be(PortNum.NeighborinfoApp);
        }

        [Fact]
        public async Task HandleAsync_ValidNeighborInfo_UpdatesNodeState()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            var neighborState = new NodeState { ConnectionId = "TEST", NodeId = 0xAABBCCDD, ShortName = "NBOR" };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);
            _nodeStateManagerMock.Setup(m => m.Get("TEST", 0xAABBCCDDu)).Returns(neighborState);

            var neighborInfo = new Meshtastic.Protobufs.NeighborInfo
            {
                NodeId = 0x12345678,
                NodeBroadcastIntervalSecs = 300
            };
            neighborInfo.Neighbors.Add(new Neighbor
            {
                NodeId = 0xAABBCCDD,
                Snr = -5.5f,
                LastRxTime = 1700000000
            });

            var packet = CreatePacket(0x12345678, neighborInfo);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().NotBeNull();
            result!.UpdatesNodeState.Should().BeTrue();
            result.CotXml.Should().BeNull(); // NeighborInfo doesn't generate CoT directly

            nodeState.Neighbors.Should().HaveCount(1);
            nodeState.Neighbors[0].NodeId.Should().Be(0xAABBCCDD);
            nodeState.Neighbors[0].Snr.Should().BeApproximately(-5.5f, 0.01f);
            nodeState.Neighbors[0].NodeName.Should().Be("NBOR");
        }

        [Fact]
        public async Task HandleAsync_MultipleNeighbors_StoresAll()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);
            _nodeStateManagerMock.Setup(m => m.Get(It.IsAny<string>(), It.IsAny<uint>())).Returns((NodeState)null);

            var neighborInfo = new Meshtastic.Protobufs.NeighborInfo
            {
                NodeId = 0x12345678
            };
            neighborInfo.Neighbors.Add(new Neighbor { NodeId = 0xAABBCCDD, Snr = -3.0f });
            neighborInfo.Neighbors.Add(new Neighbor { NodeId = 0x11223344, Snr = -12.5f });
            neighborInfo.Neighbors.Add(new Neighbor { NodeId = 0x55667788, Snr = -8.0f });

            var packet = CreatePacket(0x12345678, neighborInfo);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().NotBeNull();
            nodeState.Neighbors.Should().HaveCount(3);

            nodeState.Neighbors.Should().Contain(n => n.NodeId == 0xAABBCCDD && Math.Abs(n.Snr - (-3.0f)) < 0.01);
            nodeState.Neighbors.Should().Contain(n => n.NodeId == 0x11223344 && Math.Abs(n.Snr - (-12.5f)) < 0.01);
            nodeState.Neighbors.Should().Contain(n => n.NodeId == 0x55667788 && Math.Abs(n.Snr - (-8.0f)) < 0.01);
        }

        [Fact]
        public async Task HandleAsync_UsesPacketFromIfNodeIdZero()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0xDEADBEEF };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0xDEADBEEFu)).Returns(nodeState);

            var neighborInfo = new Meshtastic.Protobufs.NeighborInfo
            {
                NodeId = 0 // Zero, should use packet.From instead
            };
            neighborInfo.Neighbors.Add(new Neighbor { NodeId = 0xAABBCCDD, Snr = -5.0f });

            var packet = CreatePacket(0xDEADBEEF, neighborInfo);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().NotBeNull();
            _nodeStateManagerMock.Verify(m => m.GetOrCreate("TEST", 0xDEADBEEFu), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_SkipsZeroNodeIdNeighbors()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);
            _nodeStateManagerMock.Setup(m => m.Get(It.IsAny<string>(), It.IsAny<uint>())).Returns((NodeState)null);

            var neighborInfo = new Meshtastic.Protobufs.NeighborInfo
            {
                NodeId = 0x12345678
            };
            neighborInfo.Neighbors.Add(new Neighbor { NodeId = 0, Snr = -5.0f }); // Invalid, should skip
            neighborInfo.Neighbors.Add(new Neighbor { NodeId = 0xAABBCCDD, Snr = -3.0f }); // Valid

            var packet = CreatePacket(0x12345678, neighborInfo);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().NotBeNull();
            nodeState.Neighbors.Should().HaveCount(1);
            nodeState.Neighbors[0].NodeId.Should().Be(0xAABBCCDD);
        }

        [Fact]
        public async Task HandleAsync_ReplacesExistingNeighbors()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            nodeState.Neighbors.Add(new WinTakMeshtasticPlugin.Models.NeighborInfo { NodeId = 0x99999999, Snr = -10.0f }); // Existing neighbor
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);
            _nodeStateManagerMock.Setup(m => m.Get(It.IsAny<string>(), It.IsAny<uint>())).Returns((NodeState)null);

            var neighborInfo = new Meshtastic.Protobufs.NeighborInfo
            {
                NodeId = 0x12345678
            };
            neighborInfo.Neighbors.Add(new Neighbor { NodeId = 0xAABBCCDD, Snr = -5.0f });

            var packet = CreatePacket(0x12345678, neighborInfo);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().NotBeNull();
            nodeState.Neighbors.Should().HaveCount(1);
            nodeState.Neighbors[0].NodeId.Should().Be(0xAABBCCDD);
            nodeState.Neighbors.Should().NotContain(n => n.NodeId == 0x99999999);
        }

        [Fact]
        public async Task HandleAsync_EmptyPayload_ReturnsNull()
        {
            // Arrange
            var packet = new MeshPacket
            {
                From = 0x12345678,
                Decoded = new Data
                {
                    Portnum = PortNum.NeighborinfoApp,
                    Payload = ByteString.Empty
                }
            };

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task HandleAsync_MalformedProtobuf_ReturnsNull()
        {
            // Arrange
            var packet = new MeshPacket
            {
                From = 0x12345678,
                Decoded = new Data
                {
                    Portnum = PortNum.NeighborinfoApp,
                    Payload = ByteString.CopyFrom(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF })
                }
            };

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert - should not throw, just return null
            result.Should().BeNull();
        }

        [Fact]
        public async Task HandleAsync_NoNodeStateManager_ReturnsNull()
        {
            // Arrange
            var contextWithoutManager = new PacketHandlerContext
            {
                ConnectionId = "TEST",
                NodeStateManager = null,
                CotBuilder = _cotBuilderMock.Object
            };

            var neighborInfo = new Meshtastic.Protobufs.NeighborInfo { NodeId = 0x12345678 };
            neighborInfo.Neighbors.Add(new Neighbor { NodeId = 0xAABBCCDD, Snr = -5.0f });
            var packet = CreatePacket(0x12345678, neighborInfo);

            // Act
            var result = await _handler.HandleAsync(packet, contextWithoutManager);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task HandleAsync_UpdatesLastHeard()
        {
            // Arrange
            var nodeState = new NodeState
            {
                ConnectionId = "TEST",
                NodeId = 0x12345678,
                LastHeard = DateTime.MinValue
            };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var neighborInfo = new Meshtastic.Protobufs.NeighborInfo { NodeId = 0x12345678 };
            neighborInfo.Neighbors.Add(new Neighbor { NodeId = 0xAABBCCDD, Snr = -5.0f });
            var packet = CreatePacket(0x12345678, neighborInfo);

            var beforeCall = DateTime.UtcNow;

            // Act
            await _handler.HandleAsync(packet, _context);

            // Assert
            nodeState.LastHeard.Should().BeAfter(beforeCall.AddSeconds(-1));
            nodeState.LastHeard.Should().BeBefore(DateTime.UtcNow.AddSeconds(1));
        }

        [Fact]
        public async Task HandleAsync_CallsNodeStateManagerUpdate()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);

            var neighborInfo = new Meshtastic.Protobufs.NeighborInfo { NodeId = 0x12345678 };
            neighborInfo.Neighbors.Add(new Neighbor { NodeId = 0xAABBCCDD, Snr = -5.0f });
            var packet = CreatePacket(0x12345678, neighborInfo);

            // Act
            await _handler.HandleAsync(packet, _context);

            // Assert
            _nodeStateManagerMock.Verify(m => m.Update(nodeState), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_SetsNeighborLastUpdate()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);
            _nodeStateManagerMock.Setup(m => m.Get(It.IsAny<string>(), It.IsAny<uint>())).Returns((NodeState)null);

            var neighborInfo = new Meshtastic.Protobufs.NeighborInfo { NodeId = 0x12345678 };
            neighborInfo.Neighbors.Add(new Neighbor { NodeId = 0xAABBCCDD, Snr = -5.0f });
            var packet = CreatePacket(0x12345678, neighborInfo);

            var beforeCall = DateTime.UtcNow;

            // Act
            await _handler.HandleAsync(packet, _context);

            // Assert
            nodeState.Neighbors.Should().HaveCount(1);
            nodeState.Neighbors[0].LastUpdate.Should().BeAfter(beforeCall.AddSeconds(-1));
            nodeState.Neighbors[0].LastUpdate.Should().BeBefore(DateTime.UtcNow.AddSeconds(1));
        }

        [Fact]
        public async Task HandleAsync_DebugMessageIncludesNeighborCount()
        {
            // Arrange
            var nodeState = new NodeState { ConnectionId = "TEST", NodeId = 0x12345678 };
            _nodeStateManagerMock.Setup(m => m.GetOrCreate("TEST", 0x12345678u)).Returns(nodeState);
            _nodeStateManagerMock.Setup(m => m.Get(It.IsAny<string>(), It.IsAny<uint>())).Returns((NodeState)null);

            var neighborInfo = new Meshtastic.Protobufs.NeighborInfo { NodeId = 0x12345678 };
            neighborInfo.Neighbors.Add(new Neighbor { NodeId = 0xAABBCCDD, Snr = -5.0f });
            neighborInfo.Neighbors.Add(new Neighbor { NodeId = 0x11223344, Snr = -8.0f });
            var packet = CreatePacket(0x12345678, neighborInfo);

            // Act
            var result = await _handler.HandleAsync(packet, _context);

            // Assert
            result.Should().NotBeNull();
            result!.DebugMessage.Should().Contain("2 neighbors");
        }

        private static MeshPacket CreatePacket(uint fromNodeId, Meshtastic.Protobufs.NeighborInfo neighborInfo)
        {
            return new MeshPacket
            {
                From = fromNodeId,
                Decoded = new Data
                {
                    Portnum = PortNum.NeighborinfoApp,
                    Payload = neighborInfo.ToByteString()
                }
            };
        }
    }
}
