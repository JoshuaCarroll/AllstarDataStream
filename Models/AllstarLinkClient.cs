using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Xml.Linq;

namespace AsteriskAMIStream.Models
{
    public static class AllstarLinkClient
    {
        private static readonly ConcurrentDictionary<string, byte> _processedNodes = new();

        public static async Task<AllstarLinkStatsNode?> GetNodeInfoAsync(int _nodeNumber)
        {
            AllstarLinkStatsNode? returnNode = null;

            try
            {
                if (processedNodesTryAdd(_nodeNumber.ToString()))  // Make sure this isn't already mapped
                {
                    ConsoleHelper.Write($"Querying node {_nodeNumber.ToString()}.", "", ConsoleColor.Green);

                    string url = $"https://stats.allstarlink.org/api/stats/{_nodeNumber.ToString()}";

                    HttpClient _httpClient = new();
                    _httpClient.Timeout = TimeSpan.FromSeconds(10); // Set a timeout for the HTTP requests
                    var response = await _httpClient.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync();
                        AllstarLinkStatsRootNode rootNode = JsonSerializer.Deserialize<AllstarLinkStatsRootNode>(jsonString)!;

                        returnNode = rootNode.node;

                        // Check for any linked nodes and process them if they exist
                        if (rootNode!.stats?.data?.linkedNodes != null)
                        {
                            foreach (var linkedNode in rootNode.stats.data.linkedNodes)
                            {
                                if (!int.TryParse(linkedNode.name, out int intLinkedNodeNumber))
                                {
                                    ConsoleHelper.Write($"Skipping non-numeric node {linkedNode.name}.", "", ConsoleColor.DarkGray);
                                    continue;
                                }
                                else if (intLinkedNodeNumber < 2000)
                                {
                                    ConsoleHelper.Write($"Skipping local node {linkedNode.name}.", "", ConsoleColor.DarkGray);
                                    continue;
                                }
                                else
                                {
                                    var childNode = await GetNodeInfoAsync(intLinkedNodeNumber);
                                    if (childNode != null)
                                    {
                                        returnNode.LinkedNodes.Add(childNode);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Handle the unsuccessful response, e.g., throw an exception or log an error  
                        ConsoleHelper.Write($"HTTP Error: {response.ReasonPhrase} - {response.Content.ToString()}", "", ConsoleColor.Red);

                        switch (response.StatusCode)
                        {
                            case System.Net.HttpStatusCode.TooManyRequests:
                                return default;
                            case System.Net.HttpStatusCode.NotFound:
                                ConsoleHelper.Write($"HTTP 404: {response.Content.ToString()}", "", ConsoleColor.Red);
                                break;
                            default:
                                ConsoleHelper.Write($"HTTP Error: {response.StatusCode}", "", ConsoleColor.Red);
                                return default;
                        }
                    }
                }
                else
                {
                    ConsoleHelper.Write($"Skipping duplicate node {_nodeNumber.ToString()}.", "", ConsoleColor.DarkGray);
                    returnNode = null; // Node already processed, return null
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions during the download or deserialization process  
                ConsoleHelper.Write($"Exception: {ex.Message}", "", ConsoleColor.Red);
            }

            return returnNode;
        }

        /// <summary>
        /// Checks if the node has already been processed and adds it to the dictionary if not.
        /// </summary>
        /// <param name="nodeName"></param>
        /// <returns>Returns false if already exists</returns>
        private static bool processedNodesTryAdd(string nodeName)
        {
            bool rtn = _processedNodes.TryAdd(nodeName, 0);
            return rtn;
        }

        public static void ClearProcessedNodes()
        {
            _processedNodes.Clear();
            ConsoleHelper.Write("Cleared processed nodes.", "", ConsoleColor.Yellow);
        }
    }
}
