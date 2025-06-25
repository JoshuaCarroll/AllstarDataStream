using AsteriskDataStream.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;

namespace AsteriskDataStream.Models
{
    public class NodeDictionary : ConcurrentDictionary<string, AllstarLinkStatsApi.Node?>
    {
        /// <summary>
        /// Checks if the node has already been processed and adds it to the dictionary if not.
        /// </summary>
        /// <param name="nodeName"></param>
        /// <returns>Returns false if already exists</returns>
        public new bool TryAdd(string nodeName, AllstarLinkStatsApi.Node? node)
        {
            if (ContainsKey(nodeName))
            {
                return false; // Node already exists
            }

            return base.TryAdd(nodeName, node);
        }

        public void ClearExpired()
        {
            var threshold = DateTime.UtcNow.AddMinutes(AllstarLinkClient.CacheExpirationMinutes * -1);
            Dictionary<string, int> toRemove = new();

            foreach (var kvp in ToArray())
            {
                var node = kvp.Value;
                if (node != null && node.Timestamp < threshold)
                {
                    toRemove.TryAdd(kvp.Key, 0);

                    // Add any linked nodes
                    while (TryGetValue(kvp.Key, out var rootNode) && rootNode?.data?.linkedNodes != null)
                    {
                        foreach (var linkedNode in rootNode.data.linkedNodes)
                        {
                            if (linkedNode != null && linkedNode.name != null && linkedNode.name != kvp.Key)
                            {
                                toRemove.TryAdd(linkedNode.name, 0);
                            }
                        }
                    }
                }
            }

            // Remove the nodes 
            foreach (var kvp in toRemove)
            {
                if (TryRemove(kvp.Key, out _))
                {
                    ConsoleHelper.Write($"Node {kvp.Key} has expired. Removing from node dictionary.", "", ConsoleColor.Gray);
                }
                else
                {
                    ConsoleHelper.Write($"Node {kvp.Key} could not be removed from node dictionary.", "", ConsoleColor.Red);
                }
            }
        }

        public List<string> ContainsAny(List<string> listIn)
        {
            List<string> listOut = new();

            foreach (var s in listIn)
            {
                if (ContainsKey(s))
                {
                    listOut.Add(s);
                }
            }

            return listOut;
        }
    }
}