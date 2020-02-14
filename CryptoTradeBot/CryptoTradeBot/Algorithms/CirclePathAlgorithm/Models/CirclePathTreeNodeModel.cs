using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTradeBot.Host.Algorithms.CirclePathAlgorithm.Models
{
    /// <summary>
    /// Represents one-directional graph (tree)
    /// </summary>
    public class CirclePathTreeNodeModel
    {
        public CirclePathTreeNodeModel()
        {
            NextNodes = null;
        }

        /// <summary>
        /// Tree node state
        /// </summary>
        public string State { get; set; }

        /// <summary>
        /// Three node connected nodes. Is empty for leaf nodes.
        /// </summary>
        public List<CirclePathTreeNodeModel> NextNodes { get; set; }

        /// <summary>
        /// Path length counted from three root to current node
        /// </summary>
        public int PathLength { get; set; }

        /// <summary>
        /// Old - IsCirclePathEnd.
        /// </summary>
        public bool IsLeafNode => this.NextNodes == null || this.NextNodes.Count == 0;
    }
}
