using AsteriskDataStream.Models;
using System.Collections.Concurrent;

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

            foreach (var kvp in ToArray())
            {
                var node = kvp.Value;
                if (node != null && node.Timestamp < threshold)
                {
                    ConsoleHelper.Write($"Data for node {kvp.Key} has expired. Removing from dictionary.", "", ConsoleColor.Gray);
                    TryRemove(kvp.Key, out _);
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