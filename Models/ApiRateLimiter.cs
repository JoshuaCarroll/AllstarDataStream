namespace AsteriskDataStream.Models
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

        public static bool TryAddRequest(string key)
        {
            if (!CanContinue)
            {
                return false;
            }

            if (RecentQueries.ContainsKey(key))
            {
                RecentQueries[key] = DateTime.UtcNow;
            }
            else
            {
                RecentQueries.Add(key, DateTime.UtcNow);
            }

            return true;
        }

        public static void RemoveExpired()
        {
            bool removedSome = false;

            foreach (var kv in RecentQueries)
            {
                if (kv.Value + Period < DateTime.UtcNow)
                {
                    removedSome = true;
                    RecentQueries.Remove(kv.Key);
                }
            }
            if (removedSome)
                ConsoleHelper.Write($"Rate limiter updated. {MaxRequestsPerPeriod - RecentQueries.Count} queries available.", "", ConsoleColor.Gray);
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
