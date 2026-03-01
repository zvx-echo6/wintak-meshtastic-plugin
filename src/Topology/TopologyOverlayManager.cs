using System;
using System.Collections.Generic;
using System.Linq;
using WinTakMeshtasticPlugin.Models;

namespace WinTakMeshtasticPlugin.Topology
{
    /// <summary>
    /// Entry in the neighbor graph representing a link between two nodes.
    /// </summary>
    public class NeighborLink
    {
        public uint NodeIdA { get; set; }
        public uint NodeIdB { get; set; }
        public double SnrDb { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Manages the topology overlay, tracking neighbor relationships
    /// and rendering/updating links on the map.
    /// </summary>
    public class TopologyOverlayManager
    {
        private readonly TopologyOverlayService _overlayService;
        private readonly Func<uint, NodeState> _getNodeState;
        private readonly Dictionary<string, NeighborLink> _activeLinks = new Dictionary<string, NeighborLink>();
        private readonly object _lock = new object();
        private bool _overlayVisible = true;

        /// <summary>
        /// Create a new topology overlay manager.
        /// </summary>
        /// <param name="overlayService">Service for drawing/removing links.</param>
        /// <param name="getNodeState">Function to retrieve node state by ID.</param>
        public TopologyOverlayManager(
            TopologyOverlayService overlayService,
            Func<uint, NodeState> getNodeState)
        {
            _overlayService = overlayService ?? throw new ArgumentNullException(nameof(overlayService));
            _getNodeState = getNodeState ?? throw new ArgumentNullException(nameof(getNodeState));
        }

        /// <summary>
        /// Gets or sets whether the topology overlay is visible.
        /// </summary>
        public bool OverlayVisible
        {
            get => _overlayVisible;
            set
            {
                if (_overlayVisible != value)
                {
                    _overlayVisible = value;
                    if (value)
                    {
                        RedrawAllLinks();
                    }
                    else
                    {
                        HideAllLinks();
                    }
                }
            }
        }

        /// <summary>
        /// Update topology from neighbor info packet.
        /// Called when a node reports its neighbors.
        /// </summary>
        /// <param name="reportingNodeId">Node ID that sent the neighbor info.</param>
        /// <param name="neighbors">List of neighbor entries with SNR data.</param>
        public void UpdateFromNeighborInfo(uint reportingNodeId, IEnumerable<NeighborEntry> neighbors)
        {
            if (neighbors == null) return;

            lock (_lock)
            {
                foreach (var neighbor in neighbors)
                {
                    string linkUid = TopologyLinkBuilder.BuildLinkUid(reportingNodeId, neighbor.NodeId);

                    // Update or create link entry
                    if (!_activeLinks.TryGetValue(linkUid, out var link))
                    {
                        link = new NeighborLink
                        {
                            NodeIdA = Math.Min(reportingNodeId, neighbor.NodeId),
                            NodeIdB = Math.Max(reportingNodeId, neighbor.NodeId)
                        };
                        _activeLinks[linkUid] = link;
                    }

                    link.SnrDb = neighbor.Snr;
                    link.LastUpdated = DateTime.UtcNow;

                    // Draw the link if overlay is visible
                    if (_overlayVisible)
                    {
                        DrawLink(linkUid, link);
                    }
                }
            }
        }

        /// <summary>
        /// Called when a node's position changes.
        /// Updates any links connected to that node.
        /// </summary>
        /// <param name="nodeId">Node ID that moved.</param>
        public void OnNodePositionChanged(uint nodeId)
        {
            if (!_overlayVisible) return;

            lock (_lock)
            {
                // Find all links involving this node
                var affectedLinks = _activeLinks
                    .Where(kvp => kvp.Value.NodeIdA == nodeId || kvp.Value.NodeIdB == nodeId)
                    .ToList();

                foreach (var kvp in affectedLinks)
                {
                    DrawLink(kvp.Key, kvp.Value);
                }
            }
        }

        /// <summary>
        /// Remove a node and all its links from the topology.
        /// Called when a node goes stale.
        /// </summary>
        /// <param name="nodeId">Node ID to remove.</param>
        public void RemoveNode(uint nodeId)
        {
            lock (_lock)
            {
                // Find and remove all links involving this node
                var linksToRemove = _activeLinks
                    .Where(kvp => kvp.Value.NodeIdA == nodeId || kvp.Value.NodeIdB == nodeId)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var linkUid in linksToRemove)
                {
                    _overlayService.RemoveTopologyLink(linkUid);
                    _activeLinks.Remove(linkUid);
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[Topology] Removed {linksToRemove.Count} links for node {nodeId:X8}");
            }
        }

        /// <summary>
        /// Clear all topology links.
        /// </summary>
        public void ClearAllLinks()
        {
            lock (_lock)
            {
                foreach (var linkUid in _activeLinks.Keys.ToList())
                {
                    _overlayService.RemoveTopologyLink(linkUid);
                }
                _activeLinks.Clear();

                System.Diagnostics.Debug.WriteLine("[Topology] Cleared all links");
            }
        }

        /// <summary>
        /// Get all active link UIDs.
        /// </summary>
        public IEnumerable<string> GetActiveLinkUids()
        {
            lock (_lock)
            {
                return _activeLinks.Keys.ToList();
            }
        }

        /// <summary>
        /// Get the count of active links.
        /// </summary>
        public int ActiveLinkCount
        {
            get
            {
                lock (_lock)
                {
                    return _activeLinks.Count;
                }
            }
        }

        private void DrawLink(string linkUid, NeighborLink link)
        {
            var nodeA = _getNodeState(link.NodeIdA);
            var nodeB = _getNodeState(link.NodeIdB);

            // Can only draw if both nodes have positions
            if (nodeA?.Latitude == null || nodeA?.Longitude == null ||
                nodeB?.Latitude == null || nodeB?.Longitude == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Topology] Cannot draw link {linkUid}: missing position data");
                return;
            }

            _overlayService.DrawTopologyLinkWithSnr(
                link.NodeIdA, link.NodeIdB,
                nodeA.Latitude.Value, nodeA.Longitude.Value,
                nodeB.Latitude.Value, nodeB.Longitude.Value,
                link.SnrDb);
        }

        private void RedrawAllLinks()
        {
            lock (_lock)
            {
                foreach (var kvp in _activeLinks)
                {
                    DrawLink(kvp.Key, kvp.Value);
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[Topology] Redrew {_activeLinks.Count} links");
            }
        }

        private void HideAllLinks()
        {
            lock (_lock)
            {
                foreach (var linkUid in _activeLinks.Keys)
                {
                    _overlayService.RemoveTopologyLink(linkUid);
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[Topology] Hidden {_activeLinks.Count} links");
            }
        }
    }

    /// <summary>
    /// Represents a neighbor entry from a NeighborInfo packet.
    /// </summary>
    public class NeighborEntry
    {
        public uint NodeId { get; set; }
        public double Snr { get; set; }
    }
}
