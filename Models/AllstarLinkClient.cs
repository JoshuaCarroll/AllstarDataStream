using AsteriskDataStream.Models.AllstarLinkStatsApi;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Xml.Linq;

namespace AsteriskDataStream.Models
{
    public static class AllstarLinkClient
    {
        public static readonly NodeDictionary NodeDictionary = new();
        public static readonly int CacheExpirationMinutes = 5; // Set the cache expiration time in minutes for each node's information

        private static bool _stopProcessingNodes = false;

        public static async Task GetNodeInfoAsync(int _nodeNumber)
        {
            try
            {
                AllstarLinkStatsApi.Node? node = null;

                // Attempt to add this to the node dictionary, returns false if it already exists
                if (NodeDictionary.TryAdd(_nodeNumber.ToString(), null))
                {
                    node = await DownloadNodeInfoAsync(_nodeNumber);

                    if (node == null)
                    {
                        ConsoleHelper.Write($"Node {_nodeNumber.ToString()} not found or could not be downloaded.", "", ConsoleColor.Red);
                        return;
                    }

                    if (!NodeDictionary.TryUpdate(_nodeNumber.ToString(), node, null))
                    {
                        ConsoleHelper.Write($"Unable to update Node {_nodeNumber.ToString()} in the dictionary.", "", ConsoleColor.Red);
                    }
                }
                else
                {
                    ConsoleHelper.Write($"Skipping cached node {_nodeNumber.ToString()}.", "", ConsoleColor.DarkGray);
                }

                // If the node has links, process them recursively
                NodeDictionary.TryGetValue(_nodeNumber.ToString(), out node);
                if (node != null && node.data.links != null)
                {
                    foreach (var linkedNode in node.data.links)
                    {
                        if (!int.TryParse(linkedNode, out int intLinkedNodeNumber))
                        {
                            ConsoleHelper.Write($"Skipping non-numeric node {linkedNode}.", "", ConsoleColor.DarkGray);
                            continue;
                        }
                        else if (intLinkedNodeNumber < 2000)
                        {
                            ConsoleHelper.Write($"Skipping private node {linkedNode}.", "", ConsoleColor.DarkGray);
                            continue;
                        }
                        else if (!NodeDictionary.TryGetValue(linkedNode, out node))
                        {
                            await GetNodeInfoAsync(intLinkedNodeNumber);
                        }
                        else
                        {
                            ConsoleHelper.Write($"Skipping cached node {linkedNode}.", "", ConsoleColor.DarkGray);
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions during the download or deserialization process  
                ConsoleHelper.Write($"Exception: {ex.Message}", "", ConsoleColor.Red);
            }
        }

        private static async Task<AllstarLinkStatsApi.Node?> DownloadNodeInfoAsync(int _nodeNumber)
        {
            AllstarLinkStatsApi.Node? returnNode = null;

            ConsoleHelper.Write($"Querying node {_nodeNumber.ToString()}.", "", ConsoleColor.Green);

            string url = $"https://stats.allstarlink.org/api/stats/{_nodeNumber.ToString()}";

            HttpClient _httpClient = new();
            _httpClient.Timeout = TimeSpan.FromSeconds(10); // Set a timeout for the HTTP requests
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                AllstarLinkStatsApi.RootNode rootNode = JsonSerializer.Deserialize<AllstarLinkStatsApi.RootNode>(jsonString)!;

                returnNode = rootNode.node;
                returnNode.data = rootNode.stats.data;
                returnNode.Timestamp = DateTime.UtcNow; // Set the timestamp to the current time
            }
            else
            {
                // Handle the unsuccessful response, e.g., throw an exception or log an error  
                ConsoleHelper.Write($"HTTP Error: {response.ReasonPhrase} - {response.Content.ToString()}", "", ConsoleColor.Red);

                switch (response.StatusCode)
                {
                    case System.Net.HttpStatusCode.TooManyRequests:
                        _stopProcessingNodes = true;
                        break;
                    case System.Net.HttpStatusCode.NotFound:
                        ConsoleHelper.Write($"HTTP 404: {response.Content.ToString()}", "", ConsoleColor.Red);
                        break;
                    default:
                        ConsoleHelper.Write($"HTTP Error: {response.StatusCode}", "", ConsoleColor.Red);
                        break;
                }
            }

            return returnNode;
        }
    }

    public class NodeDictionary : ConcurrentDictionary<string, AllstarLinkStatsApi.Node?>
    {
        /// <summary>
        /// Checks if the node has already been processed and adds it to the dictionary if not.
        /// </summary>
        /// <param name="nodeName"></param>
        /// <returns>Returns false if already exists</returns>
        public new bool TryAdd(string nodeName, AllstarLinkStatsApi.Node? node)
        {
            if (base.ContainsKey(nodeName))
            {
                return false; // Node already exists
            }
            return base.TryAdd(nodeName, node);
        }

        public void ClearExpired()
        {
            var threshold = DateTime.UtcNow.AddMinutes(AllstarLinkClient.CacheExpirationMinutes * -1);

            foreach (var kvp in this.ToArray())
            {
                var node = kvp.Value;
                if (node != null && node.Timestamp < threshold)
                {
                    this.TryRemove(kvp.Key, out _);
                }
            }
        }
    }
}
