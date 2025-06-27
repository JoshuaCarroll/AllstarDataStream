using AsteriskDataStream.Models;
using HtmlAgilityPack;
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

        public AllstarController(IConfiguration configuration)
        {

        }

        [HttpGet("")]
        public ActionResult<List<AllstarConnection>> Index()
        {
            return Ok();
        }

        [HttpGet("ami")]
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
            await AllstarLinkClient.TryLoadNodeNetworkAsync(node);

            return Ok(AllstarLinkClient.NodeDictionary);
        }

        [HttpGet("transmitting")]
        public async Task<ActionResult<List<Models.AllstarLinkStatsApi.Node>>> GetNodesTransmitting([FromQuery] int node = 65017)
        {
            List<string> keyedNodes = new();

            if (!AllstarLinkClient.IsLoadingNetwork && ApiRateLimiter.CanContinue)
            {
                if (AllstarLinkClient.NodeDictionary.Count == 0)
                {
                    await AllstarLinkClient.TryLoadNodeNetworkAsync(node);
                }

                keyedNodes = await AllstarLinkClient.TryGetNodesTransmittingAsync();
            }

            return Ok(keyedNodes);
        }
    }
}
