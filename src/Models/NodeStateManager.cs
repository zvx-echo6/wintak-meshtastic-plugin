using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace WinTakMeshtasticPlugin.Models
{
    /// <summary>
    /// Thread-safe manager for tracking mesh node state.
    /// Nodes are keyed by (connectionId, nodeId) tuple for multi-node support.
    /// </summary>
    public class NodeStateManager : INodeStateManager
    {
        private readonly ConcurrentDictionary<(string connectionId, uint nodeId), NodeState> _nodes = new();

        /// <summary>
        /// Event raised when a node's state changes.
        /// </summary>
        public event EventHandler<NodeStateChangedEventArgs>? NodeStateChanged;

        /// <summary>
        /// Event raised when a node is removed.
        /// </summary>
        public event EventHandler<NodeStateRemovedEventArgs>? NodeRemoved;

        /// <summary>
        /// Get or create a node state entry.
        /// </summary>
        public NodeState GetOrCreate(string connectionId, uint nodeId)
        {
            var key = (connectionId, nodeId);
            return _nodes.GetOrAdd(key, _ => new NodeState
            {
                ConnectionId = connectionId,
                NodeId = nodeId,
                LastHeard = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Get a node state if it exists.
        /// </summary>
        public NodeState? Get(string connectionId, uint nodeId)
        {
            var key = (connectionId, nodeId);
            return _nodes.TryGetValue(key, out var state) ? state : null;
        }

        /// <summary>
        /// Get a node state by node ID only (searches all connections).
        /// Returns the first match, preferring most recently heard.
        /// </summary>
        public NodeState? GetByNodeId(uint nodeId)
        {
            return _nodes.Values
                .Where(n => n.NodeId == nodeId)
                .OrderByDescending(n => n.LastHeard)
                .FirstOrDefault();
        }

        /// <summary>
        /// Update a node state and raise change event.
        /// </summary>
        public void Update(NodeState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var key = (state.ConnectionId, state.NodeId);
            _nodes[key] = state;

            NodeStateChanged?.Invoke(this, new NodeStateChangedEventArgs(state));
        }

        /// <summary>
        /// Remove a node from tracking.
        /// </summary>
        public void Remove(string connectionId, uint nodeId)
        {
            var key = (connectionId, nodeId);
            if (_nodes.TryRemove(key, out var removed))
            {
                NodeRemoved?.Invoke(this, new NodeStateRemovedEventArgs(removed));
            }
        }

        /// <summary>
        /// Get all tracked nodes.
        /// </summary>
        public IEnumerable<NodeState> GetAll()
        {
            return _nodes.Values.ToList();
        }

        /// <summary>
        /// Get all nodes for a specific connection.
        /// </summary>
        public IEnumerable<NodeState> GetByConnection(string connectionId)
        {
            return _nodes.Values.Where(n => n.ConnectionId == connectionId).ToList();
        }

        /// <summary>
        /// Remove all nodes for a specific connection.
        /// </summary>
        public void RemoveByConnection(string connectionId)
        {
            var toRemove = _nodes.Where(kvp => kvp.Key.connectionId == connectionId).ToList();
            foreach (var kvp in toRemove)
            {
                if (_nodes.TryRemove(kvp.Key, out var removed))
                {
                    NodeRemoved?.Invoke(this, new NodeStateRemovedEventArgs(removed));
                }
            }
        }

        /// <summary>
        /// Remove stale nodes that haven't been heard from recently.
        /// </summary>
        public void CleanupStale(TimeSpan cleanupTimeout)
        {
            var now = DateTime.UtcNow;
            var staleNodes = _nodes.Where(kvp => now - kvp.Value.LastHeard > cleanupTimeout).ToList();

            foreach (var kvp in staleNodes)
            {
                if (_nodes.TryRemove(kvp.Key, out var removed))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[NodeStateManager] Removed stale node {removed.DisplayName}");
                    NodeRemoved?.Invoke(this, new NodeStateRemovedEventArgs(removed));
                }
            }
        }

        /// <summary>
        /// Get the total count of tracked nodes.
        /// </summary>
        public int Count => _nodes.Count;
    }

    /// <summary>
    /// Event args for node state change events.
    /// </summary>
    public class NodeStateChangedEventArgs : EventArgs
    {
        public NodeState Node { get; }

        public NodeStateChangedEventArgs(NodeState node)
        {
            Node = node;
        }
    }

    /// <summary>
    /// Event args for node removal events.
    /// </summary>
    public class NodeStateRemovedEventArgs : EventArgs
    {
        public NodeState Node { get; }

        public NodeStateRemovedEventArgs(NodeState node)
        {
            Node = node;
        }
    }
}
