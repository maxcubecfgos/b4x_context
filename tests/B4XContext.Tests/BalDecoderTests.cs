using B4XContext.Engine;
using Xunit;

namespace B4XContext.Tests
{
    public class BalDecoderTests
    {
        [Fact]
        public void Full_decode_produces_json_with_views_and_variants()
        {
            var json = BalDecoder.Decode(BalFixture.Minimal(), full: true);

            Assert.Contains("\"balVersion\": 3", json);
            Assert.Contains("\"width\": 320", json);
            Assert.Contains("\"height\": 480", json);
            Assert.Contains("\"name\": \"Main\"", json);
            Assert.Contains("\"type\": \"android.view.View\"", json);
            Assert.Contains("\"name\": \"Button1\"", json);
            Assert.Contains("\"type\": \"android.widget.Button\"", json);
            Assert.Contains("\"text\": \"Click me\"", json);
            Assert.Contains("\"width\": 100", json);
            Assert.Contains("\"height\": 40", json);
        }

        [Fact]
        public void Text_decode_produces_layout_outline()
        {
            var outline = BalDecoder.Decode(BalFixture.Minimal(), full: false);

            Assert.Contains("# Layout - version 3, variants: 1", outline);
            Assert.Contains("- Main (android.view.View)", outline);
            Assert.Contains("- Button1 (android.widget.Button) 100x40 @ (10,20)", outline);
        }

        [Fact]
        public void Empty_or_truncated_data_returns_empty_string()
        {
            Assert.Equal("", BalDecoder.Decode(new byte[0], full: true));
            Assert.Equal("", BalDecoder.Decode(BalFixture.TruncatedEarly(), full: false));
        }
    }
}