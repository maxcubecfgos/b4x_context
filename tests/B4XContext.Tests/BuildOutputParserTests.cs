using System.Collections.Generic;
using B4XContext.Engine;
using Xunit;

namespace B4XContext.Tests
{
    public class BuildOutputParserTests
    {
        [Fact]
        public void Parses_version_header_and_java_version()
        {
            var r = BuildOutputParser.Parse("B4A version: 12.50\nJava Version: 17.0.10\n");

            Assert.Equal("B4A", r["platform"]);
            Assert.Equal("12.50", r["version"]);
            Assert.Equal("17.0.10", r["java_version"]);
            Assert.True((bool)r["success"]);
            Assert.Empty((List<Dictionary<string, object>>)r["errors"]);
        }

        [Fact]
        public void Parses_b4x_line_error_with_source_line()
        {
            var r = BuildOutputParser.Parse(
                "Error line: 42\n" +
                "\tDim x As Int = Log(\n" +
                "Error description: Expected ')'.\n");

            Assert.False((bool)r["success"]);
            var e = Assert.Single((List<Dictionary<string, object>>)r["errors"]);
            Assert.Equal("syntax", e["kind"]);
            Assert.Equal(42, e["b4x_line"]);
            Assert.Equal("Dim x As Int = Log(", e["source_line"]);
            Assert.Contains("Expected ')'", (string)e["message"]);
        }

        [Fact]
        public void B4a_error_block_keeps_description_occurred_line_and_word()
        {
            var r = BuildOutputParser.Parse(
                "Error description: Unknown word or missing reference.\n" +
                "Error occurred on line: 88\n" +
                "Word: fooBar\n" +
                "\tfooBar(1)\n");

            var e = Assert.Single((List<Dictionary<string, object>>)r["errors"]);
            Assert.Equal("syntax", e["kind"]);
            Assert.Equal(88, e["b4x_line"]);
            Assert.Equal("fooBar(1)", e["source_line"]);
            var msg = (string)e["message"];
            Assert.Contains("Unknown word or missing reference", msg);
            Assert.Contains("token: fooBar", msg);
        }

        [Fact]
        public void Parses_javac_error_block_and_infers_module_name()
        {
            var r = BuildOutputParser.Parse(
                "B4J version: 9.30\n" +
                "Error line: 12\n" +
                "\tLog(z)\n" +
                "Main_subs_1.java:12: error: cannot find symbol\n" +
                "\tsymbol:   variable z\n" +
                "\tlocation: class Main\n" +
                "1 error\n");

            Assert.Equal("B4J", r["platform"]);
            Assert.False((bool)r["success"]);
            var e = Assert.Single((List<Dictionary<string, object>>)r["errors"]);
            Assert.Equal("javac", e["kind"]);
            Assert.Equal("Main", e["module"]);
            Assert.Equal("Main_subs_1.java", e["java_file"]);
            Assert.Equal(12, e["java_line"]);
            Assert.Equal(12, e["b4x_line"]);
            Assert.Equal("cannot find symbol", e["message"]);
            Assert.Equal("variable z", e["symbol"]);
            Assert.Equal("class Main", e["location"]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Empty_or_null_output_is_successful(string output)
        {
            var r = BuildOutputParser.Parse(output);
            Assert.True((bool)r["success"]);
            Assert.Empty((List<Dictionary<string, object>>)r["errors"]);
        }

        [Fact]
        public void Nested_errors_accumulate_in_list()
        {
            var r = BuildOutputParser.Parse(
                "Error line: 3\n\tSub Foo(\nError description: Expected ')'.\n" +
                "Error line: 7\n\tEnd\nError description: Expected 'Sub'.\n");

            Assert.False((bool)r["success"]);
            Assert.Equal(2, ((List<Dictionary<string, object>>)r["errors"]).Count);
        }
    }
}