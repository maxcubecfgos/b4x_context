using System.Linq;
using B4XContext.Services;
using Xunit;

namespace B4XContext.Tests
{
    public class ContextBudgetTests
    {
        [Fact]
        public void Reserved_output_is_25_percent_of_context()
        {
            Assert.Equal(1024, ContextBudget.ReservedOutput(4096));
            Assert.Equal(3072, ContextBudget.Usable(4096));
            Assert.Equal(512, ContextBudget.ReservedOutput(2048));
            Assert.Equal(262144, ContextBudget.ReservedOutput(1048576));
            Assert.Equal(786432, ContextBudget.Usable(1048576));
        }

        [Fact]
        public void Reserved_output_has_floor_for_tiny_contexts()
        {
            Assert.Equal(256, ContextBudget.ReservedOutput(512));
            Assert.True(ContextBudget.Usable(1024) > 0);
        }

        [Theory]
        [InlineData(4096, "4K")]
        [InlineData(2048, "2K")]
        [InlineData(65536, "64K")]
        [InlineData(1048576, "1M")]
        [InlineData(512, "512")]
        public void Labels_match_usual_context_sizes(int context, string expected)
        {
            Assert.Equal(expected, ContextBudget.Label(context));
        }

        [Theory]
        [InlineData(4096, 4096)]
        [InlineData(5000, 4096)]
        [InlineData(7000, 8192)]
        [InlineData(100, 1024)]
        [InlineData(2_000_000, 1048576)]
        public void NearestStop_snaps_to_usual_values(int context, int expected)
        {
            Assert.Equal(expected, ContextBudget.NearestStop(context));
        }

        [Fact]
        public void Stops_run_from_1K_to_1M_monotonically()
        {
            var stops = ContextBudget.Stops;
            Assert.Equal(ContextBudget.DefaultContext, stops[2]);
            Assert.Equal(1024, stops[0]);
            Assert.Equal(1048576, stops[stops.Length - 1]);
            for (int i = 1; i < stops.Length; i++)
                Assert.True(stops[i] > stops[i - 1]);
        }

        [Fact]
        public void Analyze_reports_fit_when_under_usable_budget()
        {
            var report = ContextBudget.Analyze(1000, 4096,
                new[] { ("a.bas", 600), ("b.py", 300) });

            Assert.True(report.Fits);
            Assert.Equal(0, report.Excess);
            Assert.Equal(3072, report.Usable);
            Assert.Empty(report.Offenders);
            Assert.False(report.BaseOverBudget);
        }

        [Fact]
        public void Analyze_lists_largest_files_first_until_it_would_fit()
        {
            // base (preamble/task/tree) 500 + a 2500 + b 1000 = 4000 vs usable 3072
            var report = ContextBudget.Analyze(4000, 4096,
                new[] { ("b.py", 1000), ("a.bas", 2500) });

            Assert.False(report.Fits);
            Assert.Equal(928, report.Excess);
            Assert.Single(report.Offenders);
            Assert.Equal("a.bas", report.Offenders[0].Name);
            Assert.False(report.BaseOverBudget);
        }

        [Fact]
        public void Analyze_flags_when_base_content_alone_exceeds_budget()
        {
            var report = ContextBudget.Analyze(3200, 4096, System.Array.Empty<(string, int)>());

            Assert.False(report.Fits);
            Assert.Equal(128, report.Excess);
            Assert.True(report.BaseOverBudget);
            Assert.Empty(report.Offenders);
        }

        [Fact]
        public void Analyze_never_recommends_dropping_everything_pointlessly()
        {
            // Files cannot fix it: base alone is over budget, so the greedy loop still
            // lists candidates only while it helps — total never drops below base.
            var report = ContextBudget.Analyze(9000, 4096,
                new[] { ("x.bas", 2000), ("y.bas", 2000) });
            var dropped = report.Offenders.Sum(o => o.Tokens);

            Assert.True(report.BaseOverBudget);
            Assert.True(dropped <= 9000);
            Assert.Equal(report.Offenders.Count, report.Offenders.Select(o => o.Name).Distinct().Count());
        }
    }
}
