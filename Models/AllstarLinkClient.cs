using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Xml.Linq;

namespace AsteriskAMIStream.Models
{
    public class AllstarLinkClient
    {
        private readonly HttpClient _httpClient;

        public AllstarLinkStatsRootNode? RootNode { get; set; }

        private int _nodeNumber;

        public AllstarLinkClient(int nodeNumber)
        {
            _nodeNumber = nodeNumber;

            RootNode = new AllstarLinkStatsRootNode();

            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10); // Set a timeout for the HTTP requests
        }

        public async Task<AllstarLinkStatsRootNode> GetNodeInfoAsync()
        {
            string url = $"https://stats.allstarlink.org/api/stats/{_nodeNumber.ToString()}";

            try
            {
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    RootNode = JsonSerializer.Deserialize<AllstarLinkStatsRootNode>(jsonString);
                }
                else
                {
                    // Handle the unsuccessful response, e.g., throw an exception or log an error  
                    ConsoleHelper.Write($"Error: {response.StatusCode} - {response.ReasonPhrase}", "", ConsoleColor.Red);  
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions during the download or deserialization process  
                ConsoleHelper.Write($"An error occurred: {ex.Message}", "", ConsoleColor.Red);
            }

            return RootNode;
        }
    }
}
