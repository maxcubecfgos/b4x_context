using System.Collections.Generic;
using B4XContext.Services;
using Xunit;

namespace B4XContext.Tests
{
    public class TokenEstimateCacheTests
    {
        [Fact]
        public void Unchanged_files_are_not_stale()
        {
            var oldSizes = new Dictionary<string, long> { { @"C:\p\a.bas", 100 } };
            var newSizes = new Dictionary<string, long> { { @"C:\p\a.bas", 100 } };

            var stale = TokenEstimateCache.StaleKeys(oldSizes, newSizes, new[] { @"C:\p\a.bas" });

            Assert.Empty(stale);
        }

        [Fact]
        public void Files_with_size_change_are_stale()
        {
            var oldSizes = new Dictionary<string, long> { { @"C:\p\a.bas", 100 } };
            var newSizes = new Dictionary<string, long> { { @"C:\p\a.bas", 250 } };

            var stale = TokenEstimateCache.StaleKeys(oldSizes, newSizes, new[] { @"C:\p\a.bas" });

            Assert.Contains(@"C:\p\a.bas", stale);
        }

        [Fact]
        public void Deleted_files_are_stale()
        {
            var oldSizes = new Dictionary<string, long> { { @"C:\p\gone.txt", 50 } };
            var newSizes = new Dictionary<string, long>();

            var stale = TokenEstimateCache.StaleKeys(oldSizes, newSizes, new[] { @"C:\p\gone.txt" });

            Assert.Contains(@"C:\p\gone.txt", stale);
        }

        [Fact]
        public void Cache_keys_are_compared_case_insensitively()
        {
            var oldSizes = new Dictionary<string, long>(System.StringComparer.OrdinalIgnoreCase) { { @"C:\p\README.md", 80 } };
            var newSizes = new Dictionary<string, long>(System.StringComparer.OrdinalIgnoreCase) { { @"C:\p\readme.md", 80 } };

            var stale = TokenEstimateCache.StaleKeys(oldSizes, newSizes, new[] { @"C:\p\README.md" });

            Assert.Empty(stale);
        }

        [Fact]
        public void Mixed_batch_marks_only_changed_paths()
        {
            var oldSizes = new Dictionary<string, long>
            {
                { @"C:\p\a.bas", 100 },
                { @"C:\p\b.txt", 200 },
                { @"C:\p\c.md", 300 }
            };
            var newSizes = new Dictionary<string, long>
            {
                { @"C:\p\a.bas", 100 },
                { @"C:\p\b.txt", 999 }
            };

            var stale = TokenEstimateCache.StaleKeys(oldSizes, newSizes, oldSizes.Keys);

            Assert.DoesNotContain(@"C:\p\a.bas", stale);
            Assert.Contains(@"C:\p\b.txt", stale);
            Assert.Contains(@"C:\p\c.md", stale);
        }
    }
}