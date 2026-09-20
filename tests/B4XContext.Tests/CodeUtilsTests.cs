using System;
using System.IO;
using System.Text;
using B4XContext.Services;
using Xunit;

namespace B4XContext.Tests
{
    public class CodeUtilsTests
    {
        private static string TempFile(byte[] bytes)
        {
            var dir = Path.Combine(Path.GetTempPath(), "b4x_ctx_code_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "f.txt");
            File.WriteAllBytes(path, bytes);
            return path;
        }

        [Fact]
        public void ReadTextSafely_strips_utf8_bom()
        {
            var body = Encoding.UTF8.GetBytes("Sub Foo\nEnd Sub\n");
            var withBom = new byte[3 + body.Length];
            withBom[0] = 0xEF; withBom[1] = 0xBB; withBom[2] = 0xBF;
            Array.Copy(body, 0, withBom, 3, body.Length);

            var text = CodeUtils.ReadTextSafely(TempFile(withBom));

            Assert.Equal("Sub Foo\nEnd Sub\n", text);
        }

        [Fact]
        public void ReadTextSafely_passes_utf8_through()
        {
            var text = "h\u00f3la \u2014 c\u00f3digo \u00e9nfasis\n";
            var got = CodeUtils.ReadTextSafely(TempFile(Encoding.UTF8.GetBytes(text)));
            Assert.Equal(text, got);
        }

        [Fact]
        public void ReadTextSafely_falls_back_to_windows1252_for_invalid_utf8()
        {
            // 'café' with é as Latin-1 byte 0xE9 (invalid standalone UTF-8)
            var bytes = new byte[] { 0x63, 0x61, 0x66, 0xE9, 0x0A };
            var text = CodeUtils.ReadTextSafely(TempFile(bytes));
            Assert.Equal("caf\u00e9\n", text);
        }

        [Fact]
        public void ReadTextSafely_strips_before_end_of_design_text_marker()
        {
            var src = "Sub Globals\n\tDim x As Int\nEnd Sub\n@EndOfDesignText@\n' layout part\n";
            var text = CodeUtils.ReadTextSafely(TempFile(Encoding.UTF8.GetBytes(src)));
            Assert.DoesNotContain("Sub Globals", text);
            Assert.Contains("' layout part", text);
        }

        [Fact]
        public void ReadTextSafely_trims_leading_newlines_after_marker()
        {
            var src = "@EndOfDesignText@\n\n\ncontent";
            var text = CodeUtils.ReadTextSafely(TempFile(Encoding.UTF8.GetBytes(src)));
            Assert.Equal("content", text);
        }

        [Fact]
        public void ReadTextSafely_ignores_files_without_marker()
        {
            var src = "Sub A\nEnd Sub\n";
            Assert.Equal(src, CodeUtils.ReadTextSafely(TempFile(Encoding.UTF8.GetBytes(src))));
        }

        [Theory]
        [InlineData("a\nb\nc\n", 1, 1, "a")]
        [InlineData("a\nb\nc\n", 2, 2, "b")]
        [InlineData("a\nb\nc\n", 1, 3, "a\nb\nc")]
        [InlineData("a\nb\nc\n", 0, 1, "a")]
        [InlineData("a\nb\nc\n", 2, 999, "b\nc\n")]
        [InlineData("a\nb\nc\n", 999, 999, "")]
        public void ExtractSub_clamps_line_bounds(string src, int start, int end, string expected)
        {
            Assert.Equal(expected, CodeUtils.ExtractSub(src, start, end));
        }

        [Fact]
        public void ExtractSub_returns_empty_for_null_source()
        {
            Assert.Equal("", CodeUtils.ExtractSub(null, 1, 2));
        }
    }
}