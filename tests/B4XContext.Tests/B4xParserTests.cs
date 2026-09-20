using System.Linq;
using B4XContext.Engine;
using Xunit;

namespace B4XContext.Tests
{
    public class B4xParserTests
    {
        [Fact]
        public void Wrong_closer_reports_expected_and_found()
        {
            var (_, issues) = B4xParser.Parse("If True Then\nEnd While\n");

            Assert.Single(issues);
            Assert.Contains("Expected \"End If\"", issues[0].Message);
            Assert.Contains("\"End While\"", issues[0].Message);
            Assert.Equal(2, issues[0].Line);
            Assert.Equal("error", issues[0].Severity);
        }

        [Fact]
        public void Unclosed_sub_reports_unclosed_block()
        {
            var (_, issues) = B4xParser.Parse("Sub Foo\n\tLog(1)\n");

            Assert.Single(issues);
            Assert.Contains("Unclosed block: expected \"End Sub\"", issues[0].Message);
            Assert.Contains("opened at line 1", issues[0].Message);
        }

        [Fact]
        public void Unexpected_closer_reports_no_open_block()
        {
            var (_, issues) = B4xParser.Parse("End If\n");

            var issue = Assert.Single(issues);
            Assert.Contains("Unexpected \"End If\": no open block to close", issue.Message);
        }

        [Fact]
        public void Parse_collects_multiple_issues_from_nested_stack()
        {
            var (_, issues) = B4xParser.Parse("Sub A\nIf x Then\nEnd Sub\n");

            Assert.Equal(2, issues.Count);
            Assert.Contains(issues, i => i.Message.Contains("Expected \"End If\""));
            Assert.Contains(issues, i => i.Message.Contains("Unclosed block: expected \"End Sub\""));
        }

        [Fact]
        public void Globals_blocks_parse_with_kind_and_endline()
        {
            var (root, issues) = B4xParser.Parse(
                "Sub Process_Globals\n" +
                "\tDim x As Int\n" +
                "End Sub\n");

            Assert.Empty(issues);
            var node = Assert.Single(B4xParser.FlattenSubsAndTypes(root));
            Assert.Equal("Process_Globals", node.Kind);
            Assert.Equal(1, node.StartLine);
            Assert.Equal(3, node.EndLine);
        }

        [Fact]
        public void Nested_regions_build_container_hierarchy()
        {
            var (root, _) = B4xParser.Parse(
                "#Region A\n" +
                "#Region B\n" +
                "Sub Inner\n" +
                "End Sub\n" +
                "#End Region\n" +
                "#End Region\n");

            var a = Assert.Single(root.Children);
            Assert.Equal("Region", a.Kind);
            Assert.Equal("A", a.Name);
            var b = Assert.Single(a.Children);
            Assert.Equal("B", b.Name);
            var sub = Assert.Single(b.Children);
            Assert.Equal("Sub", sub.Kind);
            Assert.Equal("Inner", sub.Name);
            Assert.Equal(3, sub.StartLine);
            Assert.Equal(4, sub.EndLine);
        }

        [Fact]
        public void Private_sub_keeps_params_return_type_and_visibility()
        {
            var (root, _) = B4xParser.Parse("Private Sub DoIt(x As Int) As Boolean\nEnd Sub\n");

            var node = Assert.Single(B4xParser.FlattenSubsAndTypes(root));
            Assert.True(node.IsPrivate);
            Assert.Equal("(x As Int)", node.Params);
            Assert.Equal("Boolean", node.ReturnType);
        }

        [Fact]
        public void Single_line_type_has_endline_equal_startline()
        {
            var (root, _) = B4xParser.Parse("Type Person(Name As String, Age As Int)\n");

            var node = Assert.Single(B4xParser.FlattenSubsAndTypes(root));
            Assert.Equal("Type", node.Kind);
            Assert.Equal("Person", node.Name);
            Assert.Equal(1, node.StartLine);
            Assert.Equal(1, node.EndLine);
        }

        [Fact]
        public void Leading_comment_is_attached_to_following_sub()
        {
            var (root, _) = B4xParser.Parse("' Does a thing\nSub DoIt\nEnd Sub\n");

            var node = Assert.Single(B4xParser.FlattenSubsAndTypes(root));
            Assert.Equal("' Does a thing", node.LeadingComment);
        }
    }
}