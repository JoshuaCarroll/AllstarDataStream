using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using AsteriskDataStream.Models;

namespace AsteriskDataStream.Services
{
    public class AutomatedTasksService : IHostedService, IDisposable
    {
        private static bool _automatedTasksRunning = false;
        private CancellationTokenSource _cts;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = RunPeriodicTaskAsync(_cts.Token);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            ConsoleHelper.WriteLine("🛑 Stopping Automated Tasks Service...", ConsoleColor.Yellow);

            _cts?.Cancel();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            
        }

        private async Task RunPeriodicTaskAsync(CancellationToken token)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(29));
            while (await timer.WaitForNextTickAsync(token))
            {
                try
                {
                    await AutomatedTasksAsync();
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteLine($"Automated task error: {ex.Message}", ConsoleColor.Red);
                }
            }
        }

        public async Task AutomatedTasksAsync()
        {
            if (AllstarLinkClient.NodeDictionary.Count == 0 || AllstarLinkClient.InitialRootNodeNumber == 0)
                return;

            if (!_automatedTasksRunning && !AllstarLinkClient.IsLoadingNetwork)
            {
                _automatedTasksRunning = true;
                ConsoleHelper.Write(" 🔲 ", ConsoleColor.Gray);

                // Clear expired nodes and rate limiter
                AllstarLinkClient.NodeDictionary.ClearExpired();
                ApiRateLimiter.RemoveExpired();

                // Load any null nodes - they haven't loaded yet
                await AllstarLinkClient.TryLoadNodeNetworkAsync(AllstarLinkClient.InitialRootNodeNumber);
                
                ConsoleHelper.Rewrite("☑ ", 3, ConsoleColor.Gray);
                _automatedTasksRunning = false;
            }
            else
            {
                ConsoleHelper.WriteLine("⏳", ConsoleColor.Gray); // Hourglass symbol
            }
        }
    }
}