﻿namespace AsteriskDataStream.Models
{
    public static class ApiRateLimiter
    {
        public static int MaxRequestsPerPeriod { get; set; } = 30;
        public static TimeSpan Period { get; set; } = TimeSpan.FromMinutes(1);
        private static Dictionary<string, DateTime> RecentQueries = new();

        public static bool CanContinue
        {
            get
            {
                return RecentQueries.Count < MaxRequestsPerPeriod;
            }
        }

        public static bool TryAddRequest()
        {
            return TryAddRequest(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
        }

        public static bool TryAddRequest(int itemKey)
        {
            return TryAddRequest(itemKey.ToString());
        }

        public static bool TryAddRequest(string itemKey)
        {
            if (!CanContinue)
            {
                return false;
            }

            return RecentQueries.TryAdd(itemKey, DateTime.UtcNow);
        }

        public static void RemoveExpired()
        {
            var keysToRemove = RecentQueries
                .Where(kv => kv.Value + Period < DateTime.UtcNow)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                RecentQueries.Remove(key);
            }

        }

        /// <summary>
        /// Uses the rate limiter to fill up the queue with dummy requests. Helps in cases when the queue count is incorrect.
        /// </summary>
        public static void FillUpQueue()
        {
            var availableSlots = MaxRequestsPerPeriod - RecentQueries.Count;
            for (int i = 0; i <= availableSlots; i++)
            {
                RecentQueries.Add($"server-says-stop-{i}", DateTime.UtcNow);
            }
        }
    }
}
