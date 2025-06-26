namespace AsteriskDataStream.Models
{
    public static class ApiRateLimiter
    {
        public static int MaxRequestsPerPeriod { get; set; } = 30;
        public static TimeSpan Period { get; set; } = TimeSpan.FromMinutes(1);
        private static Dictionary<string, DateTime> RecentQueries = new();
        private static int RequestCount = 0;

        public static bool CanContinue
        {
            get
            {
                return RecentQueries.Count < MaxRequestsPerPeriod;
            }
        }

        public static bool TryAddRequest()
        {
            if (!CanContinue)
            {
                return false;
            }

            string key = $"{RequestCount++.ToString()}";
            return RecentQueries.TryAdd(key, DateTime.UtcNow);
        }

        public static void RemoveExpired()
        {
            foreach (var kv in RecentQueries)
            {
                if (kv.Value + Period < DateTime.UtcNow)
                {
                    RecentQueries.Remove(kv.Key);
                }
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
