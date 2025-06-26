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
        private static bool _automatedTasksRunning = false;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            ConsoleHelper.WriteLine("🔄 Starting Automated Tasks Service...", ConsoleColor.Yellow);

            _timer = new Timer(
                callback: _ => AutomatedTasks(),
                state: null,
                dueTime: TimeSpan.FromSeconds(10),              // Start time
                period: TimeSpan.FromSeconds(15));              // Interval time

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            ConsoleHelper.WriteLine("🛑 Stopping Automated Tasks Service...", ConsoleColor.Yellow);

            _timer?.Change(Timeout.Infinite, 0); // Stop the timer
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        public void AutomatedTasks()
        {
            if (AllstarLinkClient.NodeDictionary.Count == 0)
                return;

            if (!_automatedTasksRunning && !AllstarLinkClient.IsLoadingNetwork)
            {
                _automatedTasksRunning = true;
                ConsoleHelper.Write(" 🔲 ", ConsoleColor.Gray);

                // Clear expired nodes and rate limiter
                AllstarLinkClient.NodeDictionary.ClearExpired();
                ApiRateLimiter.RemoveExpired();

                // Load any null nodes - they haven't loaded yet
                //if (AllstarLinkClient.InitialRootNodeNumber != 0)
                 //   AllstarLinkClient.LoadNodeNetworkAsync(AllstarLinkClient.InitialRootNodeNumber, true).GetAwaiter();
                
                ConsoleHelper.Rewrite("☑ ", 3, ConsoleColor.Gray);
                _automatedTasksRunning = false;
            }
            else
            {
                ConsoleHelper.Write("⏳ ", ConsoleColor.Gray); // Hourglass symbol
            }
        }
    }
}