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
        private static AllstarClient? _client;
        private readonly AMISettings _amiSettings;

        public AllstarController(IConfiguration configuration)
        {
            // Fetch the first AMI server configuration from appsettings.json
            var amiSettingsList = configuration.GetSection("AMISettings").Get<List<AMISettings>>();

            if (amiSettingsList == null || !amiSettingsList.Any())
            {
                throw new InvalidOperationException("AMISettings configuration is missing or empty.");
            }

            _amiSettings = amiSettingsList.First();

            // Initialize the AsteriskClient with settings
            if (_client == null)
            {
                _client = new AllstarClient(
                    _amiSettings.Host,
                    _amiSettings.Port,
                    _amiSettings.Username,
                    _amiSettings.Password,
                    _amiSettings.NodeNumber
                );
            }
        }

        [HttpGet("")]
        public async Task<ActionResult<List<AllstarConnection>>> GetNodes()
        {
            await _client!.GetNodeInfoAsync(_amiSettings.NodeNumber);

            // Remove any connections that are older than 1 minute
            _client.ClearExpiredConnections(TimeSpan.FromMinutes(1));

            // Return all messages as a response
            return Ok(_client.AllstarConnections);
        }
    }
}
