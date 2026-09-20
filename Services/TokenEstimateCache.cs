using System;
using System.Collections.Generic;

namespace B4XContext.Services
{
    public static class TokenEstimateCache
    {
        public static HashSet<string> StaleKeys(
            IReadOnlyDictionary<string, long> oldSizes,
            IReadOnlyDictionary<string, long> newSizes,
            IEnumerable<string> cacheKeys)
        {
            var stale = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in cacheKeys)
            {
                if (!newSizes.TryGetValue(key, out var newSize)
                    || !oldSizes.TryGetValue(key, out var oldSize)
                    || oldSize != newSize)
                    stale.Add(key);
            }
            return stale;
        }
    }
}