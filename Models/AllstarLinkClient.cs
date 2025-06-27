using AsteriskDataStream.Models.AllstarLinkStatsApi;
using HtmlAgilityPack;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Xml.Linq;

namespace AsteriskDataStream.Models
{
    public static class AllstarLinkClient
    {
        public static readonly int CacheExpirationMinutes = 10; // Set the cache expiration time in minutes for each node's information

        public static readonly NodeDictionary NodeDictionary = new();
        public static int InitialRootNodeNumber = 0; 
        private static readonly SemaphoreSlim Semaphore = new(1); // Allow up to N concurrent downloads
        public static bool IsLoadingNetwork { get; private set; } = false;

        public static async Task TryLoadNodeNetworkAsync(int rootNodeNumber)
        {
            for (int i = 0; i < 3; i++)
            {
                if (!AllstarLinkClient.IsLoadingNetwork && ApiRateLimiter.CanContinue && rootNodeNumber >= 2000)
                {
                    await LoadNodeNetworkAsync(rootNodeNumber, true);
                    break; // Exit the loop early if successful
                }

                // Wait 3 seconds before rechecking
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }

        private static async Task LoadNodeNetworkAsync(int rootNodeNumber, bool isInitialCall = false)
        {
            if (isInitialCall)
            {
                InitialRootNodeNumber = rootNodeNumber;
                IsLoadingNetwork = true;
                ConsoleHelper.Write($"〖", ConsoleColor.Gray);
            }

            try
            {
                Node? rootNode = await GetOrLoadRootNode(rootNodeNumber);

                if (rootNode?.data?.linkedNodes == null)
                    return;

                foreach (var linked in rootNode.data.linkedNodes)
                {
                    if (!ApiRateLimiter.CanContinue)
                        return;

                    if (int.TryParse(linked.name, out int linkedNodeNumber) && linkedNodeNumber >= 2000 && linkedNodeNumber <= 999999)
                    {
                        if (NodeDictionary.TryAdd(linked.name, null))
                            await LoadNodeNetworkAsync(linkedNodeNumber);
                    }
                    else
                    {
                        if (linkedNodeNumber > 0)
                        {
                            NodeDictionary.TryAdd(linked.name, NewNonAllstarNode(linkedNodeNumber));
                        }
                        else
                        {
                            NodeDictionary.TryAdd(linked.name, NewNonAllstarNode(linked.name));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteLine($"Exception: {ex.Message}", ConsoleColor.Red);
            }

            // Local function to simplify root node handling
            static async Task<Node?> GetOrLoadRootNode(int nodeNumber)
            {
                if (nodeNumber < 2000)
                    return NewNonAllstarNode(nodeNumber);

                string key = nodeNumber.ToString();

                if (NodeDictionary.TryAdd(key, null))
                {
                    var node = await DownloadNodeInfoAsync(nodeNumber);
                    if (node != null)
                        NodeDictionary.TryUpdate(key, node, null);
                    return node;
                }
                else
                {
                    var cached = NodeDictionary.GetValueOrDefault(key);
                    if (cached == null)
                    {
                        cached = await DownloadNodeInfoAsync(nodeNumber);
                        if (cached != null)
                            NodeDictionary.TryUpdate(key, cached, null);
                    }
                    return cached;
                }
            }

            if (isInitialCall)
            {
                IsLoadingNetwork = false;
                ConsoleHelper.Write($"〗", ConsoleColor.Gray);
            }
        }

        private static async Task<Node?> DownloadNodeInfoAsync(int _nodeNumber)
        {
            Node? returnNode = null;

            if (_nodeNumber < 2000)
            {
                return returnNode;
            }

            if (ApiRateLimiter.TryAddRequest(_nodeNumber))
            {
                ConsoleHelper.WriteLine($"Querying node {_nodeNumber.ToString()}.", ConsoleColor.Green);

                string url = $"https://stats.allstarlink.org/api/stats/{_nodeNumber.ToString()}";

                HttpClient _httpClient = new();
                _httpClient.Timeout = TimeSpan.FromSeconds(5); // Set a timeout for the HTTP requests
                var response = await _httpClient.GetAsync(url);

                ConsoleHelper.WriteLine($"  HTTP response received.", ConsoleColor.Yellow);

                if (response.IsSuccessStatusCode)
                {
                    ConsoleHelper.WriteLine($"  HTTP response was successful.", ConsoleColor.Yellow);

                    var jsonString = await response.Content.ReadAsStringAsync();
                    RootNode rootNode = JsonSerializer.Deserialize<AllstarLinkStatsApi.RootNode>(jsonString)!;

                    returnNode = rootNode.node;

                    if (rootNode.stats?.data != null)
                    {
                        returnNode.data = rootNode.stats.data;
                    }
                    
                    returnNode.Timestamp = DateTime.UtcNow; // Set the timestamp to the current time
                }
                else
                {
                    ConsoleHelper.WriteLine($"  HTTP response was FAIL.", ConsoleColor.Yellow);

                    switch (response.StatusCode)
                    {
                        case System.Net.HttpStatusCode.TooManyRequests:
                            ConsoleHelper.WriteLine("HTTP 429: API rate limit exceeded. Telling rate limiter...", ConsoleColor.Yellow);
                            ApiRateLimiter.FillUpQueue();
                            break;
                        case System.Net.HttpStatusCode.NotFound:
                            ConsoleHelper.WriteLine($"DownloadNodeInfoAsync(): HTTP 404: {response.ReasonPhrase} ({url})", ConsoleColor.Red);
                            break;
                        default:
                            ConsoleHelper.WriteLine($"DownloadNodeInfoAsync(): HTTP Error {response.StatusCode}: {response.ReasonPhrase}", ConsoleColor.Red);
                            break;
                    }
                }
            }

            return returnNode;
        }

        public static async Task<List<string>> TryGetNodesTransmittingAsync()
        {
            List<string> returnValue = new();

            for (int i = 0; i < 3; i++)
            {
                if (!AllstarLinkClient.IsLoadingNetwork && ApiRateLimiter.CanContinue)
                {
                    returnValue = await GetNodesTransmittingAsync();
                    break; // Exit the loop early if successful
                }

                // Wait 3 seconds before rechecking
                await Task.Delay(TimeSpan.FromSeconds(3));
            }

            return returnValue;
        }

        private static async Task<List<string>> GetNodesTransmittingAsync()
        {
            List<string> keyedNodes = new();

            if (!ApiRateLimiter.TryAddRequest())
            {
                return keyedNodes;
            }

            using var httpClient = new HttpClient();

            try
            {
                var response = await httpClient.GetAsync($"https://stats.allstarlink.org/stats/keyed?_ts={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    ApiRateLimiter.FillUpQueue();
                    return keyedNodes; // Exit early if rate limit is hit
                }

                response.EnsureSuccessStatusCode(); // Will throw if status is 4xx or 5xx

                var html = await response.Content.ReadAsStringAsync();

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                keyedNodes = htmlDoc.DocumentNode
                    .SelectNodes("//table//tr/td[1]/a")
                    ?.Select(li => li.InnerText.Trim())
                    .ToList() ?? new List<string>();

                //keyedNodes = NodeDictionary.ContainsAny(keyedNodes);
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteLine($"Exception in GetNodesTransmittingAsync: {ex.Message}", ConsoleColor.Red);
            }

            return keyedNodes;
        }

        private static Node NewNonAllstarNode(int nodeNumber)
        {
            string nodeDescription = "";

            if (nodeNumber < 2000)
            {
                nodeDescription = "Private";
            }
            else if (nodeNumber > 999999)
            {
                nodeDescription = "Echolink";
            }

            return NewNonAllstarNode(nodeNumber.ToString(), nodeDescription);
        }

        private static Node NewNonAllstarNode(string nodeName, string nodeDescription = "")
        {
            if (nodeDescription == "")
                nodeDescription = "Direct";

            return new Node
            {
                name = nodeName,
                node_frequency = nodeDescription,
                Timestamp = DateTime.UtcNow // Set the timestamp to the current time
            };
        }
    }
}
