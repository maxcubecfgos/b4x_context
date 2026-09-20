using System;
using System.Collections.Generic;
using B4XContext.Engine;
using B4XContext.Services;
using Xunit;

namespace B4XContext.Tests
{
    public class BuildFormatterTests
    {
        private static Dictionary<string, object> Parse(string output) => BuildOutputParser.Parse(output);

        [Fact]
        public void Format_empty_for_null_or_success()
        {
            Assert.Equal("", BuildFormatter.Format(null));
            Assert.Equal("", BuildFormatter.Format(Parse("B4A version: 12.50\n")));
        }

        [Fact]
        public void Format_renders_error_sections_to_markdown()
        {
            var result = Parse(
                "B4A version: 12.50\n" +
                "Error line: 12\n" +
                "\tLog(z)\n" +
                "Main_subs_1.java:12: error: cannot find symbol\n" +
                "\tsymbol:   variable z\n" +
                "\tlocation: class Main\n");

            var md = BuildFormatter.Format(result);

            Assert.Contains("## COMPILATION ERRORS (B4A 12.50)", md);
            Assert.Contains("### Main line 12", md);
            Assert.Contains("```b4x", md);
            Assert.Contains("Log(z)", md);
            Assert.Contains("**cannot find symbol**", md);
            Assert.Contains("- symbol: variable z", md);
            Assert.Contains("- location: class Main", md);
        }

        [Fact]
        public void Format_error_without_source_omits_code_block()
        {
            var result = Parse(
                "Error description: Unknown word.\n" +
                "Error occurred on line: 4\n");

            var md = BuildFormatter.Format(result);

            Assert.Contains("### (unknown module) line 4", md);
            Assert.DoesNotContain("```", md);
            Assert.Contains("**Unknown word.", md);
        }
    }
}