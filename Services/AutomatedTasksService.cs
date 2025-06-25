using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using AsteriskDataStream.Models;

namespace AsteriskDataStream.Services
{
    public class AutomatedTasksService : IHostedService, IDisposable
    {
        private Timer? _timer;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(
                callback: _ => AutomatedTasks(),
                state: null,
                dueTime: TimeSpan.Zero,              // Start immediately
                period: TimeSpan.FromSeconds(15));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0); // Stop the timer
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        public void AutomatedTasks()
        {
            // Clear expired nodes and rate limiter
            AllstarLinkClient.NodeDictionary.ClearExpired();
            ApiRateLimiter.RemoveExpired();

            // Load any null nodes - they haven't loaded yet
            AllstarLinkClient.LoadNodeNetworkAsync(AllstarLinkClient.InitialRootNodeNumber).GetAwaiter();

            ConsoleHelper.Write(".", "", ConsoleColor.Gray, ConsoleColor.Black, false);
        }
    }
}