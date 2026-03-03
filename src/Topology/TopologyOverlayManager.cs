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
                System.Diagnostics.Debug.WriteLine(
                    $"[Topology] OverlayVisible changing from {_overlayVisible} to {value}, activeLinks={_activeLinks.Count}");

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

        /// <summary>
        /// Set of nodes that have per-node link display enabled.
        /// Used independently of the global overlay toggle.
        /// </summary>
        private readonly HashSet<uint> _perNodeEnabled = new HashSet<uint>();

        /// <summary>
        /// Show links for a specific node (per-node toggle).
        /// Works independently of the global overlay setting.
        /// </summary>
        /// <param name="nodeId">Node ID to show links for.</param>
        public void ShowLinksForNode(uint nodeId)
        {
            lock (_lock)
            {
                _perNodeEnabled.Add(nodeId);

                // Find and draw all links involving this node
                var nodeLinks = _activeLinks
                    .Where(kvp => kvp.Value.NodeIdA == nodeId || kvp.Value.NodeIdB == nodeId)
                    .ToList();

                foreach (var kvp in nodeLinks)
                {
                    DrawLink(kvp.Key, kvp.Value);
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[Topology] Showing {nodeLinks.Count} links for node {nodeId:X8}");
            }
        }

        /// <summary>
        /// Hide links for a specific node (per-node toggle).
        /// Only hides links if neither endpoint has per-node enabled and global is off.
        /// </summary>
        /// <param name="nodeId">Node ID to hide links for.</param>
        public void HideLinksForNode(uint nodeId)
        {
            lock (_lock)
            {
                _perNodeEnabled.Remove(nodeId);

                // Find all links involving this node
                var nodeLinks = _activeLinks
                    .Where(kvp => kvp.Value.NodeIdA == nodeId || kvp.Value.NodeIdB == nodeId)
                    .ToList();

                foreach (var kvp in nodeLinks)
                {
                    var link = kvp.Value;
                    var otherNodeId = link.NodeIdA == nodeId ? link.NodeIdB : link.NodeIdA;

                    // Only hide if neither global nor other endpoint's per-node is enabled
                    if (!_overlayVisible && !_perNodeEnabled.Contains(otherNodeId))
                    {
                        _overlayService.RemoveTopologyLink(kvp.Key);
                    }
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[Topology] Hide request for node {nodeId:X8} (global={_overlayVisible})");
            }
        }

        /// <summary>
        /// Check if per-node links are enabled for a specific node.
        /// </summary>
        public bool IsNodeLinksEnabled(uint nodeId)
        {
            lock (_lock)
            {
                return _perNodeEnabled.Contains(nodeId);
            }
        }

        private void DrawLink(string linkUid, NeighborLink link)
        {
            var nodeA = _getNodeState(link.NodeIdA);
            var nodeB = _getNodeState(link.NodeIdB);

            System.Diagnostics.Debug.WriteLine(
                $"[Topology] DrawLink {linkUid}: nodeA={link.NodeIdA:X8} nodeB={link.NodeIdB:X8}");

            // Can only draw if both nodes have positions
            if (nodeA?.Latitude == null || nodeA?.Longitude == null ||
                nodeB?.Latitude == null || nodeB?.Longitude == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Topology] Cannot draw link {linkUid}: missing position data " +
                    $"(A: lat={nodeA?.Latitude}, lon={nodeA?.Longitude}, " +
                    $"B: lat={nodeB?.Latitude}, lon={nodeB?.Longitude})");
                return;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[Topology] Drawing link with positions: " +
                $"({nodeA.Latitude.Value:F6}, {nodeA.Longitude.Value:F6}) -> " +
                $"({nodeB.Latitude.Value:F6}, {nodeB.Longitude.Value:F6}), SNR={link.SnrDb:F1}");

            _overlayService.DrawTopologyLinkWithSnr(
                link.NodeIdA, link.NodeIdB,
                nodeA.Latitude.Value, nodeA.Longitude.Value,
                nodeB.Latitude.Value, nodeB.Longitude.Value,
                link.SnrDb);
        }

        /// <summary>
        /// Populate links from existing node state.
        /// Call this after enabling overlay to catch any neighbor info received while disabled.
        /// </summary>
        /// <param name="allNodes">All known nodes with their neighbor data.</param>
        public void PopulateFromExistingNodes(IEnumerable<NodeState> allNodes)
        {
            if (allNodes == null) return;

            int addedCount = 0;
            lock (_lock)
            {
                foreach (var node in allNodes)
                {
                    if (node.Neighbors == null || node.Neighbors.Count == 0) continue;

                    foreach (var neighbor in node.Neighbors)
                    {
                        string linkUid = TopologyLinkBuilder.BuildLinkUid(node.NodeId, neighbor.NodeId);

                        if (!_activeLinks.ContainsKey(linkUid))
                        {
                            _activeLinks[linkUid] = new NeighborLink
                            {
                                NodeIdA = Math.Min(node.NodeId, neighbor.NodeId),
                                NodeIdB = Math.Max(node.NodeId, neighbor.NodeId),
                                SnrDb = neighbor.Snr,
                                LastUpdated = neighbor.LastUpdate
                            };
                            addedCount++;
                        }
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine(
                $"[Topology] PopulateFromExistingNodes: added {addedCount} links, total={_activeLinks.Count}");
        }

        private void RedrawAllLinks()
        {
            lock (_lock)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Topology] RedrawAllLinks called with {_activeLinks.Count} links");

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
