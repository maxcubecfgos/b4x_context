using B4XContext.Services;
using Xunit;

namespace B4XContext.Tests
{
    public class TextUtilsTests
    {
        [Fact]
        public void AppendBlock_returns_block_when_current_is_empty()
        {
            Assert.Equal("clip", TextUtils.AppendBlock("", "clip"));
        }

        [Fact]
        public void AppendBlock_returns_block_when_current_is_whitespace_only()
        {
            Assert.Equal("clip", TextUtils.AppendBlock("   \n  ", "clip"));
        }

        [Fact]
        public void AppendBlock_returns_block_when_current_is_null()
        {
            Assert.Equal("clip", TextUtils.AppendBlock(null, "clip"));
        }

        [Fact]
        public void AppendBlock_joins_with_blank_line()
        {
            Assert.Equal("first\n\nsecond", TextUtils.AppendBlock("first", "second"));
        }

        [Fact]
        public void AppendBlock_preserves_current_when_block_is_blank()
        {
            Assert.Equal("first", TextUtils.AppendBlock("first", "   "));
        }
    }
}