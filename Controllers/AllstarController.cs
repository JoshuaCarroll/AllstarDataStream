using AsteriskDataStream.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AsteriskDataStream.Controllers
{
    [Route("api")]
    [ApiController]
    public class AllstarController : Controller
    {
        private static List<AllstarAmiClient> _allstarClients = new();
        private readonly List<AMISettings> _amiSettingsList;

        public AllstarController(IConfiguration configuration)
        {
            _amiSettingsList = configuration.GetSection("AMISettings").Get<List<AMISettings>>()
                ?? throw new InvalidOperationException("AMISettings configuration is missing or empty.");

            if (_allstarClients.Count == 0)
            {
                foreach (var settings in _amiSettingsList)
                {
                    _allstarClients.Add(new AllstarAmiClient(
                        settings.Host,
                        settings.Port,
                        settings.Username,
                        settings.Password,
                        settings.NodeNumber
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
        public async Task<ActionResult<List<Models.AllstarLinkStatsApi.Node>>> GetAslNodeStatus([FromQuery] int node = 65017)
        {
            AllstarLinkClient.NodeDictionary.ClearExpired();

            if (node >= 2000)
            {
                await AllstarLinkClient.GetNodeInfoAsync(node);
            }

            return Ok(AllstarLinkClient.NodeDictionary);
        }
    }
}
