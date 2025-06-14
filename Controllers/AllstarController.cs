using AsteriskAMIStream.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AsteriskAMIStream.Controllers
{
    [Route("api")]
    [ApiController]
    public class AllstarController : Controller
    {
        private static List<AllstarClient> _allstarClients = new();
        private static List<AllstarLinkClient> _allstarLinkClients = new();
        private readonly List<AMISettings> _amiSettingsList;

        public AllstarController(IConfiguration configuration)
        {
            _amiSettingsList = configuration.GetSection("AMISettings").Get<List<AMISettings>>()
                ?? throw new InvalidOperationException("AMISettings configuration is missing or empty.");

            if (_allstarClients.Count == 0)
            {
                foreach (var settings in _amiSettingsList)
                {
                    _allstarClients.Add(new AllstarClient(
                        settings.Host,
                        settings.Port,
                        settings.Username,
                        settings.Password,
                        settings.NodeNumber
                    ));
                }

                foreach (var settings in _amiSettingsList)
                {
                    _allstarLinkClients.Add(new AllstarLinkClient(
                        int.Parse(settings.NodeNumber)
                    ));
                }
            }
        }

        [HttpGet("")]
        public async Task<ActionResult<List<AllstarConnection>>> GetNodes()
        {
            var allConnections = new List<AllstarConnection>();

            bool hasClearedExpiredConnections = false;
            foreach (var client in _allstarClients)
            {
                if (!hasClearedExpiredConnections)
                {
                    client.ClearExpiredConnections(TimeSpan.FromMinutes(1));
                    hasClearedExpiredConnections = true;
                }

                await client.GetNodeInfoAsync(client.NodeNumber);    
                allConnections.AddRange(client.AllstarConnections);
            }

            return Ok(allConnections);
        }

        [HttpGet("asl")]
        public async Task<ActionResult<List<AllstarLinkStatsRootNode>>> GetAslNodeStatus()
        {
            var allRootNodes = new List<AllstarLinkStatsRootNode>();

            foreach (var client in _allstarLinkClients)
            {
                AllstarLinkStatsRootNode root = await client.GetNodeInfoAsync();
                allRootNodes.Add(root);
            }

            return Ok(allRootNodes);
        }
    }
}
