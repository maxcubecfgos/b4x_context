using B4XContext.Services;
using Xunit;

namespace B4XContext.Tests
{
    public class TokenCounterTests
    {
        [Fact]
        public void Counts_short_code_line()
        {
            Assert.Equal(4, TokenCounter.Count("Sub Process_Globals"));
        }

        [Fact]
        public void Counts_b4x_snippet()
        {
            var src = "Sub btnStart_Click\n    Dim r As Random\n    r.Initialize(321)\n    Log(r.Next(100))\nEnd Sub";
            Assert.Equal(26, TokenCounter.Count(src));
        }

        [Fact]
        public void Counts_prose()
        {
            Assert.Equal(9, TokenCounter.Count("Hello, world! This is a test."));
        }

        [Fact]
        public void Empty_and_null_yield_zero()
        {
            Assert.Equal(0, TokenCounter.Count(""));
            Assert.Equal(0, TokenCounter.Count(null));
        }
    }
}