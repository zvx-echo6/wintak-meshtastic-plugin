using System;
using System.Linq;
using FluentAssertions;
using WinTakMeshtasticPlugin.Models;
using Xunit;

namespace WinTakMeshtasticPlugin.Tests.Models
{
    public class NodeStateManagerTests
    {
        [Fact]
        public void GetOrCreate_NewNode_CreatesNodeState()
        {
            // Arrange
            var manager = new NodeStateManager();

            // Act
            var node = manager.GetOrCreate("conn1", 0x12345678);

            // Assert
            node.Should().NotBeNull();
            node.ConnectionId.Should().Be("conn1");
            node.NodeId.Should().Be(0x12345678u);
            manager.Count.Should().Be(1);
        }

        [Fact]
        public void GetOrCreate_ExistingNode_ReturnsSameInstance()
        {
            // Arrange
            var manager = new NodeStateManager();
            var first = manager.GetOrCreate("conn1", 0x12345678);
            first.ShortName = "TEST";

            // Act
            var second = manager.GetOrCreate("conn1", 0x12345678);

            // Assert
            second.Should().BeSameAs(first);
            second.ShortName.Should().Be("TEST");
            manager.Count.Should().Be(1);
        }

        [Fact]
        public void GetOrCreate_DifferentConnections_CreatesSeparateNodes()
        {
            // Arrange
            var manager = new NodeStateManager();

            // Act
            var node1 = manager.GetOrCreate("conn1", 0x12345678);
            var node2 = manager.GetOrCreate("conn2", 0x12345678);

            // Assert
            node1.Should().NotBeSameAs(node2);
            manager.Count.Should().Be(2);
        }

        [Fact]
        public void Get_ExistingNode_ReturnsNode()
        {
            // Arrange
            var manager = new NodeStateManager();
            manager.GetOrCreate("conn1", 0x12345678);

            // Act
            var node = manager.Get("conn1", 0x12345678);

            // Assert
            node.Should().NotBeNull();
            node!.NodeId.Should().Be(0x12345678u);
        }

        [Fact]
        public void Get_NonExistingNode_ReturnsNull()
        {
            // Arrange
            var manager = new NodeStateManager();

            // Act
            var node = manager.Get("conn1", 0x12345678);

            // Assert
            node.Should().BeNull();
        }

        [Fact]
        public void GetByNodeId_ExistingNode_ReturnsNode()
        {
            // Arrange
            var manager = new NodeStateManager();
            manager.GetOrCreate("conn1", 0x12345678);

            // Act
            var node = manager.GetByNodeId(0x12345678);

            // Assert
            node.Should().NotBeNull();
            node!.NodeId.Should().Be(0x12345678u);
        }

        [Fact]
        public void GetByNodeId_MultipleConnections_ReturnsMostRecent()
        {
            // Arrange
            var manager = new NodeStateManager();
            var node1 = manager.GetOrCreate("conn1", 0x12345678);
            node1.LastHeard = DateTime.UtcNow.AddMinutes(-10);
            manager.Update(node1);

            var node2 = manager.GetOrCreate("conn2", 0x12345678);
            node2.LastHeard = DateTime.UtcNow;
            manager.Update(node2);

            // Act
            var result = manager.GetByNodeId(0x12345678);

            // Assert
            result.Should().NotBeNull();
            result!.ConnectionId.Should().Be("conn2");
        }

        [Fact]
        public void Update_RaisesNodeStateChangedEvent()
        {
            // Arrange
            var manager = new NodeStateManager();
            var node = manager.GetOrCreate("conn1", 0x12345678);

            NodeState? eventNode = null;
            manager.NodeStateChanged += (s, e) => eventNode = e.Node;

            // Act
            node.ShortName = "TEST";
            manager.Update(node);

            // Assert
            eventNode.Should().NotBeNull();
            eventNode!.ShortName.Should().Be("TEST");
        }

        [Fact]
        public void Remove_ExistingNode_RemovesAndRaisesEvent()
        {
            // Arrange
            var manager = new NodeStateManager();
            manager.GetOrCreate("conn1", 0x12345678);

            NodeState? removedNode = null;
            manager.NodeRemoved += (s, e) => removedNode = e.Node;

            // Act
            manager.Remove("conn1", 0x12345678);

            // Assert
            manager.Count.Should().Be(0);
            manager.Get("conn1", 0x12345678).Should().BeNull();
            removedNode.Should().NotBeNull();
            removedNode!.NodeId.Should().Be(0x12345678u);
        }

        [Fact]
        public void Remove_NonExistingNode_DoesNothing()
        {
            // Arrange
            var manager = new NodeStateManager();
            var eventRaised = false;
            manager.NodeRemoved += (s, e) => eventRaised = true;

            // Act
            manager.Remove("conn1", 0x12345678);

            // Assert
            eventRaised.Should().BeFalse();
        }

        [Fact]
        public void GetAll_ReturnsAllNodes()
        {
            // Arrange
            var manager = new NodeStateManager();
            manager.GetOrCreate("conn1", 0x11111111);
            manager.GetOrCreate("conn1", 0x22222222);
            manager.GetOrCreate("conn2", 0x33333333);

            // Act
            var nodes = manager.GetAll().ToList();

            // Assert
            nodes.Should().HaveCount(3);
            nodes.Select(n => n.NodeId).Should().BeEquivalentTo(
                new[] { 0x11111111u, 0x22222222u, 0x33333333u });
        }

        [Fact]
        public void GetByConnection_ReturnsOnlyConnectionNodes()
        {
            // Arrange
            var manager = new NodeStateManager();
            manager.GetOrCreate("conn1", 0x11111111);
            manager.GetOrCreate("conn1", 0x22222222);
            manager.GetOrCreate("conn2", 0x33333333);

            // Act
            var nodes = manager.GetByConnection("conn1").ToList();

            // Assert
            nodes.Should().HaveCount(2);
            nodes.All(n => n.ConnectionId == "conn1").Should().BeTrue();
        }

        [Fact]
        public void RemoveByConnection_RemovesAllConnectionNodes()
        {
            // Arrange
            var manager = new NodeStateManager();
            manager.GetOrCreate("conn1", 0x11111111);
            manager.GetOrCreate("conn1", 0x22222222);
            manager.GetOrCreate("conn2", 0x33333333);

            var removedCount = 0;
            manager.NodeRemoved += (s, e) => removedCount++;

            // Act
            manager.RemoveByConnection("conn1");

            // Assert
            manager.Count.Should().Be(1);
            manager.GetByConnection("conn1").Should().BeEmpty();
            manager.GetByConnection("conn2").Should().HaveCount(1);
            removedCount.Should().Be(2);
        }

        [Fact]
        public void CleanupStale_RemovesOldNodes()
        {
            // Arrange
            var manager = new NodeStateManager();

            var freshNode = manager.GetOrCreate("conn1", 0x11111111);
            freshNode.LastHeard = DateTime.UtcNow;
            manager.Update(freshNode);

            var staleNode = manager.GetOrCreate("conn1", 0x22222222);
            staleNode.LastHeard = DateTime.UtcNow.AddHours(-25);
            manager.Update(staleNode);

            var removedCount = 0;
            manager.NodeRemoved += (s, e) => removedCount++;

            // Act
            manager.CleanupStale(TimeSpan.FromHours(24));

            // Assert
            manager.Count.Should().Be(1);
            manager.Get("conn1", 0x11111111).Should().NotBeNull();
            manager.Get("conn1", 0x22222222).Should().BeNull();
            removedCount.Should().Be(1);
        }

        [Fact]
        public void CleanupStale_KeepsFreshNodes()
        {
            // Arrange
            var manager = new NodeStateManager();

            var node = manager.GetOrCreate("conn1", 0x11111111);
            node.LastHeard = DateTime.UtcNow;
            manager.Update(node);

            // Act
            manager.CleanupStale(TimeSpan.FromHours(24));

            // Assert
            manager.Count.Should().Be(1);
        }
    }

    public class NodeStateTests
    {
        [Fact]
        public void NodeIdHex_FormatsCorrectly()
        {
            // Arrange
            var node = new NodeState { NodeId = 0x12345678 };

            // Assert
            node.NodeIdHex.Should().Be("!12345678");
        }

        [Fact]
        public void DisplayName_PrefersShortName()
        {
            // Arrange
            var node = new NodeState
            {
                NodeId = 0x12345678,
                ShortName = "TEST"
            };

            // Assert
            node.DisplayName.Should().Be("TEST");
        }

        [Fact]
        public void DisplayName_FallsBackToHexId()
        {
            // Arrange
            var node = new NodeState
            {
                NodeId = 0x12345678,
                ShortName = null
            };

            // Assert
            node.DisplayName.Should().Be("!12345678");
        }

        [Fact]
        public void PrimaryChannel_ReturnsLowestChannel()
        {
            // Arrange
            var node = new NodeState();
            node.ChannelsMembership.Add(3);
            node.ChannelsMembership.Add(1);
            node.ChannelsMembership.Add(5);

            // Assert
            node.PrimaryChannel.Should().Be(1);
        }

        [Fact]
        public void PrimaryChannel_NoChannels_ReturnsZero()
        {
            // Arrange
            var node = new NodeState();

            // Assert
            node.PrimaryChannel.Should().Be(0);
        }

        [Fact]
        public void IsStale_RecentUpdate_ReturnsFalse()
        {
            // Arrange
            var node = new NodeState
            {
                LastPositionUpdate = DateTime.UtcNow
            };

            // Assert
            node.IsStale(TimeSpan.FromMinutes(30)).Should().BeFalse();
        }

        [Fact]
        public void IsStale_OldUpdate_ReturnsTrue()
        {
            // Arrange
            var node = new NodeState
            {
                LastPositionUpdate = DateTime.UtcNow.AddHours(-1)
            };

            // Assert
            node.IsStale(TimeSpan.FromMinutes(30)).Should().BeTrue();
        }
    }
}
