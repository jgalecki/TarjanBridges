using Assets.Scripts.Logging;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Screens.Conversation.Graph
{
    public class SpanningTreeNode
    {
        public Node Node;
        public int Postorder;
        public List<SpanningTreeNode> Children;
        public List<string> UnusedConnections; // this doesn't count connections to the parent or any children.
        public int NumDescendants;   // includes self
        public int LowestWithJump;
        public int HighestWithJump;

        public SpanningTreeNode(Node node)
        {
            Node = node;
            Children = new List<SpanningTreeNode>();
            UnusedConnections = new List<string>();
        }
    }

    /// <summary>
    /// A class for creating a spanning tree out of graph nodes. Good for connecting separate components and running Tarjan's Bridge Finding algorithm
    /// </summary>    
    public class SpanningTree
    {
        static readonly ILog _log = Log.GetLogger(typeof(SpanningTree));

        private SpanningTreeNode _root;
        private List<Node> _originalNodes;
        private List<Node> _unconnectedNodes;
        private int _counter;

        // For now, we don't use the spanning tree for anything else but fixing bridges and connecting things. To keep code clean,
        // build it in a static function for clarity.
        public static void ConnectAndFixBridges(List<Node> nodes)
        {
            var spanningTree = new SpanningTree(nodes);
        }

        // Creates a spanning tree from graph of nodes. Will add in edges if needed.
        public SpanningTree(List<Node> nodes)
        {
            _originalNodes = nodes;

            _unconnectedNodes = new List<Node>();     // track nodes not connected to the tree, so that we can add unconnected components later
            foreach (var node in nodes)
            {
                _unconnectedNodes.Add(node);
            }

            _root = new SpanningTreeNode(_originalNodes[0]);
            _unconnectedNodes.Remove(_originalNodes[0]);
            Build(_root, null);

            _log.Write("");
            // We might have two (or more) unconnected components in this graph. If that's the case, then build a bridge.
            // Another edge will be added to fix this new bridge later.
            // Right now, there's no order to the unconnected nodes. Later on we could order by distance that to ensure the smallest edges are added.
            while (_unconnectedNodes.Count > 0)
            {
                var node = _unconnectedNodes[0];
                var closeTreeNode = ClosestTo(node, _root);
                closeTreeNode.Node.ConnectedNodes.Add(node);        // Possible improvement: avoid bad overlapping angles. See seed 208382959 on WorkTutorial
                _log.Write("Adding edge between {0} and {1} to connect components", closeTreeNode.Node.name, node.name);
                Build(closeTreeNode, null);
            }

            _log.Write("Spanning tree");
            Print(_root);

            // Tarjan magic

            _counter = 1;
            MarkPostorder(_root);
            CountDescendants(_root);

            MarkMaxLowJump();
            CountLowJump(_root, false);
            CountHighJump(_root, false);

            _log.Write("Tree node details");
            PrintNodeDetail(_root);
            
            FixBridges(_root, null);
            _log.Write("Graph connections, w/ new connections and no bridges");
            PrintConnections(_root);
            _log.Write("");
        }

        // Depth first search
        // Check if each connected Node is already in the tree. For any that aren't, they are this treeNode's children. Recurse on.
        // If they are already in the tree and the node in question isn't a parent, then add it to UnusedConnections.

        // Can call twice to update with new connections to previously-unconnected components
        private void Build(SpanningTreeNode treeNode, SpanningTreeNode parentTreeNode)
        {
            foreach (var connection in treeNode.Node.ConnectedNodes)
            {
                if (treeNode.UnusedConnections.Contains(connection.name))
                {
                    // already noted on a previous Build call
                }
                else if (Contains(connection.name))
                {
                    // Connection in question is stored elsewhere in our tree. If it's not the parent, note this.
                    if (parentTreeNode != null && parentTreeNode.Node != connection)
                    {
                        treeNode.UnusedConnections.Add(connection.name);
                    }
                }
                else
                {
                    var child = new SpanningTreeNode(connection);
                    _unconnectedNodes.Remove(connection);
                    treeNode.Children.Add(child);
                    Build(child, treeNode);
                }
            }
        }

        // The raison d'etre of this class (mostly). Add edges if an edge to a child is identified as a bridge, preferring using grandchildren to this node's parent
        private void FixBridges(SpanningTreeNode treeNode, SpanningTreeNode parent)
        {
            foreach (var child in treeNode.Children)
            {
                if (child.HighestWithJump <= child.Postorder && child.LowestWithJump > child.Postorder - child.NumDescendants)
                {
                    // Bring out the bridge brigade, yo
                    if (parent != null)
                    {
                        parent.Node.ConnectedNodes.Add(child.Node);
                        child.Node.ConnectedNodes.Add(parent.Node);
                        _log.Write("Fixed bridge between {0} and {1} by connecting nodes {2} and {3}", treeNode.Node.name, child.Node.name, parent.Node.name, child.Node.name);
                    }
                    else if (child.Children.Count > 0)
                    {
                        treeNode.Node.ConnectedNodes.Add(child.Children[0].Node);
                        child.Children[0].Node.ConnectedNodes.Add(treeNode.Node);
                        _log.Write("Fixed bridge between {0} and {1} by connecting nodes {2} and {3}", treeNode.Node.name, child.Node.name, treeNode.Node.name, child.Children[0].Node.name);

                    }
                    else  // panic and grab a random node
                    {
                        var panicNode = _originalNodes.Where(n => n != child.Node && n != treeNode.Node).First();

                        panicNode.ConnectedNodes.Add(child.Node);
                        child.Node.ConnectedNodes.Add(panicNode);
                        _log.Write("Fixed bridge between {0} and {1} by connecting nodes {2} and {3}", treeNode.Node.name, child.Node.name, panicNode.name, child.Node.name);
                    }
                }

                FixBridges(child, treeNode);
            }
        }

        private void MarkMaxLowJump()
        {
            MarkMaxLowJump(_root, _root.Postorder + 1);
        }

        private void MarkMaxLowJump(SpanningTreeNode treeNode, int max)
        {
            treeNode.LowestWithJump = max;

            foreach (var child in treeNode.Children)
            {
                MarkMaxLowJump(child, max);
            }
        }

        // Calculates the lowest NumDescendants value of the node, any children, and any connected nodes not connected in the spanning tree.
        // HasJumped prevents cyclical checks by tracking the one non-tree edge jump.
        private int CountLowJump(SpanningTreeNode treeNode, bool hasJumped)
        {
            int lowestMark = _root.NumDescendants + 1;    // 1 more than max value here
            if (!hasJumped)
            {
                foreach (var child in treeNode.Children)
                {
                    int childLow = CountLowJump(child, hasJumped);
                    if (childLow < lowestMark)
                    {
                        lowestMark = childLow;
                    }
                }
                foreach (var connection in treeNode.Node.ConnectedNodes)
                {
                    if (treeNode.UnusedConnections.Contains(connection.name))
                    {
                        var jumpNode = Find(connection.name);
                        int jumpLow = CountLowJump(jumpNode, true);
                        if (jumpLow < lowestMark)
                        {
                            lowestMark = jumpLow;
                        }
                    }
                }
            }
            if (treeNode.Postorder < lowestMark)
            {
                lowestMark = treeNode.Postorder;
            }
            if (lowestMark < treeNode.LowestWithJump)
            {
                treeNode.LowestWithJump = lowestMark;
            }
            return lowestMark;
        }

        // analog to CountLowJump
        private int CountHighJump(SpanningTreeNode treeNode, bool hasJumped)
        {
            int highestMark = 0;  
            if (!hasJumped)
            {
                foreach (var child in treeNode.Children)
                {
                    int childHigh = CountHighJump(child, hasJumped);
                    if (childHigh > highestMark)
                    {
                        highestMark = childHigh;
                    }
                }
                foreach (var connection in treeNode.Node.ConnectedNodes)
                {
                    if (treeNode.UnusedConnections.Contains(connection.name))
                    {
                        var jumpNode = Find(connection.name);
                        int jumpHigh = CountHighJump(jumpNode, true);
                        if (jumpHigh > highestMark)
                        {
                            highestMark = jumpHigh;
                        }
                    }
                }
            }
            if (treeNode.Postorder > highestMark)
            {
                highestMark = treeNode.Postorder;
            }
            if (highestMark > treeNode.HighestWithJump)
            {
                treeNode.HighestWithJump = highestMark;
            }
            return highestMark;
        }

        private int CountDescendants(SpanningTreeNode treeNode)
        {
            int sum = 0;
            foreach (var child in treeNode.Children)
            {
                sum += CountDescendants(child);
            }

            treeNode.NumDescendants = sum + 1;  // Includes self
            return treeNode.NumDescendants;
        }

        private void MarkPostorder(SpanningTreeNode treeNode)
        {
            foreach (var child in treeNode.Children)
            {
                MarkPostorder(child);
            }
            treeNode.Postorder = _counter;
            _counter++;
        }

        private void Print(SpanningTreeNode treeNode)
        {
            _log.Write("Node {0} w/ children {1}, unused connections to {2}", 
                treeNode.Node.name, 
                treeNode.Children.Count > 0 ? treeNode.Children.Select(c => c.Node.name).Aggregate((x, y) => x + ", " + y) : "NONE",
                treeNode.UnusedConnections.Count > 0 ? treeNode.UnusedConnections.Aggregate((x, y) => x + ", " + y) : "NONE");
            foreach (var child in treeNode.Children)
            {
                Print(child);
            }
        }

        private void PrintNodeDetail(SpanningTreeNode treeNode)
        {
            _log.Write("Node {0} Postorder {1} numDesc {2} low {3} high {4}",
                treeNode.Node.name, treeNode.Postorder, treeNode.NumDescendants, treeNode.LowestWithJump, treeNode.HighestWithJump);
            foreach (var child in treeNode.Children)
            {
                PrintNodeDetail(child);
            }
        }

        private void PrintConnections(SpanningTreeNode treeNode)
        {
            _log.Write("Node {0} w/ graph connections {1}",
                treeNode.Node.name,
                treeNode.Node.ConnectedNodes.Count > 0 ? treeNode.Node.ConnectedNodes.Select(c => c.name).Aggregate((x, y) => x + ", " + y) : "NONE");
            foreach (var child in treeNode.Children)
            {
                PrintConnections(child);
            }
        }

        private SpanningTreeNode ClosestTo(Node node, SpanningTreeNode treeNode)
        {
            SpanningTreeNode closest = treeNode;
            float distance = Vector3.Distance(treeNode.Node.transform.position, node.transform.position);

            foreach (var child in treeNode.Children)
            {
                var closestChild = ClosestTo(node, child);
                float childDistance = Vector3.Distance(closestChild.Node.transform.position, node.transform.position);
                if (childDistance < distance)
                {
                    closest = closestChild;
                    distance = childDistance;
                }
            }
            return closest;
        }

        private SpanningTreeNode Find(string name)
        {
            var treeNode = Find(name, _root);
            if (treeNode == null)
            {
                _log.Write("Could not find node with name {0}", name);
            }
            return treeNode;
        }

        private SpanningTreeNode Find(string name, SpanningTreeNode treeNode)
        {
            if (treeNode.Node.name == name) return treeNode;

            foreach (var child in treeNode.Children)
            {
                var childResult = Find(name, child);
                if (childResult != null)
                {
                    return childResult;
                }
            }

            return null;
        }

        // Using 'name' instead of node or treeNode here lets us go back and forth between the two mostly seamlessly
        private bool Contains(string name)
        {
            return Contains(name, _root);
        }

        private bool Contains(string name, SpanningTreeNode treeNode)
        {
            if (treeNode.Node.name == name) return true;

            bool childContains = false;
            foreach (var child in treeNode.Children)
            {
                if (Contains(name, child))
                {
                    childContains = true;
                }
            }
            return childContains;
        }
    }
}
