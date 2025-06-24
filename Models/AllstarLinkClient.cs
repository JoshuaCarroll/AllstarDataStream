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
        public static readonly int CacheExpirationMinutes = 5; // Set the cache expiration time in minutes for each node's information
        private static int _rootNodeNumber = 0; // Default root node number to start the network loading

        public static readonly NodeDictionary NodeDictionary = new();
        private static readonly SemaphoreSlim Semaphore = new(5); // Allow up to 5 concurrent downloads
        private static readonly ConcurrentDictionary<int, bool> VisitedNodes = new();


        public static async Task LoadNodeNetworkAsync(int rootNodeNumber)
        {
            ConsoleHelper.Write($"Loading AllstarLink network starting from node {rootNodeNumber}.", "", ConsoleColor.Green);

            if (rootNodeNumber < 2000)
                return;

            var queue = new Queue<int>();
            queue.Enqueue(rootNodeNumber);

            while (queue.Count > 0)
            {
                int currentNode = queue.Dequeue();

                // Prevent reprocessing
                if (!VisitedNodes.TryAdd(currentNode, true))
                    continue;

                try
                {
                    await Semaphore.WaitAsync();

                    Node? node = await DownloadNodeInfoAsync(currentNode);

                    if (node != null)
                    {
                        NodeDictionary.TryAdd(node.name, node);

                        foreach (var linked in node.data?.linkedNodes ?? Enumerable.Empty<Node>())
                        {
                            if (int.TryParse(linked.name, out int linkedNodeNumber) && linkedNodeNumber >= 2000)
                            {
                                if (!VisitedNodes.ContainsKey(linkedNodeNumber))
                                {
                                    queue.Enqueue(linkedNodeNumber);
                                }
                            }
                            else
                            {
                                // Non-Allstar or private nodes
                                var nonAllstarNode = new Node
                                {
                                    name = linked.name,
                                    node_frequency = "Direct / Private",
                                    Timestamp = DateTime.UtcNow
                                };

                                NodeDictionary.TryAdd(linked.name, nonAllstarNode);
                            }
                        }
                    }
                }
                finally
                {
                    Semaphore.Release();
                }

                // Respect rate limits
                if (!ApiRateLimiter.CanContinue)
                {
                    ConsoleHelper.Write("API Rate limit reached. Pausing network load.", "", ConsoleColor.Yellow);
                    break;
                }
            }
        }


        private static async Task<Node?> DownloadNodeInfoAsync(int _nodeNumber)
        {
            Node? returnNode = null;

            if (_nodeNumber < 2000)
            {
                return returnNode;
            }

            if (ApiRateLimiter.TryAddRequest(_nodeNumber.ToString()))
            {
                ConsoleHelper.Write($"Querying node {_nodeNumber.ToString()}.", "", ConsoleColor.Green);

                string url = $"https://stats.allstarlink.org/api/stats/{_nodeNumber.ToString()}";

                HttpClient _httpClient = new();
                _httpClient.Timeout = TimeSpan.FromSeconds(10); // Set a timeout for the HTTP requests
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    RootNode rootNode = JsonSerializer.Deserialize<AllstarLinkStatsApi.RootNode>(jsonString)!;

                    returnNode = rootNode.node;
                    returnNode.data = rootNode.stats.data;
                    returnNode.Timestamp = DateTime.UtcNow; // Set the timestamp to the current time
                }
                else
                {
                    switch (response.StatusCode)
                    {
                        case System.Net.HttpStatusCode.TooManyRequests:
                            ConsoleHelper.Write("HTTP 429: API RATE LIMIT HIT!!", "", ConsoleColor.Yellow);
                            ApiRateLimiter.FillUpQueue();
                            break;
                        case System.Net.HttpStatusCode.NotFound:
                            ConsoleHelper.Write($"HTTP 404: {response.Content.ToString()}", "", ConsoleColor.Red);
                            break;
                        default:
                            ConsoleHelper.Write($"HTTP Error {response.StatusCode}: {response.ReasonPhrase}", "", ConsoleColor.Red);
                            break;
                    }
                }
            }
            else
            {
                ConsoleHelper.Write($"API rate limit exceeded. Skipping node {_nodeNumber}.", "", ConsoleColor.Yellow);
            }

            return returnNode;
        }

        public static async Task<List<string>> GetNodesTransmittingAsync()
        {
            List<string> keyedNodes = new();

            if (!ApiRateLimiter.TryAddRequest($"GetNodesTransmittingAsync-{DateTime.UtcNow.Minute}-{DateTime.UtcNow.Second}"))
            {
                return keyedNodes;
            }

            using var httpClient = new HttpClient();

            try
            {
                var response = await httpClient.GetAsync("https://stats.allstarlink.org/stats/keyed");

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

                keyedNodes = NodeDictionary.ContainsAny(keyedNodes);
            }
            catch (Exception ex)
            {
                ConsoleHelper.Write($"Exception: {ex.Message}", "", ConsoleColor.Red);
            }

            return keyedNodes;
        }
    }
}
