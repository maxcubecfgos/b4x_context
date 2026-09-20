using System.Collections.Generic;
using System.Linq;
using B4XContext.Engine;
using Xunit;
using Node = B4XContext.Engine.SkeletonGenerator.Node;

namespace B4XContext.Tests
{
    public class SkeletonGeneratorTests
    {
        private static IEnumerable<Node> MapNodes(params B4xParser.B4XNode[] nodes)
        {
            return nodes.Select(n => new Node
            {
                StartLine = n.StartLine,
                EndLine = n.EndLine,
                Kind = n.Kind,
                Name = n.Name,
                LeadingComment = n.LeadingComment
            });
        }

        private const string Sample =
            "'Module\n" +
            "Sub Process_Globals\n" +
            "\tDim gCounter As Int\n" +
            "\tDim gName As String\n" +
            "End Sub\n" +
            "\n" +
            "Sub Foo(x As Int) As Boolean\n" +
            "\tDim r As Int\n" +
            "\tLog(x)\n" +
            "\tReturn r\n" +
            "End Sub\n";

        [Fact]
        public void Globals_blocks_are_emitted_in_full()
        {
            var (root, _) = B4xParser.Parse(Sample);
            var nodes = MapNodes(B4xParser.FlattenSubsAndTypes(root).ToArray());
            var skeleton = SkeletonGenerator.GenerateModuleSkeleton(Sample, nodes);

            Assert.Contains("'Module", skeleton);
            Assert.Contains("Sub Process_Globals", skeleton);
            Assert.Contains("Dim gCounter As Int", skeleton);
            Assert.Contains("Dim gName As String", skeleton);
        }

        [Fact]
        public void Sub_signature_and_dims_kept_body_omitted()
        {
            var (root, _) = B4xParser.Parse(Sample);
            var nodes = MapNodes(B4xParser.FlattenSubsAndTypes(root).ToArray());
            var skeleton = SkeletonGenerator.GenerateModuleSkeleton(Sample, nodes);

            Assert.Contains("Sub Foo(x As Int) As Boolean", skeleton);
            Assert.Contains("\tDim r As Int", skeleton);
            Assert.Contains("End Sub", skeleton);
            Assert.Contains("lines omitted", skeleton);
            Assert.DoesNotContain("Log(x)", skeleton);
            Assert.DoesNotContain("\tReturn r", skeleton);
        }

        [Fact]
        public void Kept_sub_emits_full_body()
        {
            var (root, _) = B4xParser.Parse(Sample);
            var nodes = MapNodes(B4xParser.FlattenSubsAndTypes(root).ToArray());
            var skeleton = SkeletonGenerator.GenerateSkeletonResult(Sample, nodes, new[] { "Foo" }).Skeleton;

            Assert.Contains("Log(x)", skeleton);
            Assert.Contains("\tReturn r", skeleton);
        }

        [Fact]
        public void Leading_comment_above_sub_is_kept()
        {
            var src = "' Do the thing\nSub Run\n\tLog(1)\nEnd Sub\n";
            var (root, _) = B4xParser.Parse(src);
            var nodes = MapNodes(B4xParser.FlattenSubsAndTypes(root).ToArray());
            var skeleton = SkeletonGenerator.GenerateModuleSkeleton(src, nodes);

            Assert.Contains("' Do the thing", skeleton);
            Assert.Contains("Sub Run", skeleton);
        }

        [Fact]
        public void Single_line_type_keeps_signature_and_closer()
        {
            var src = "Type Person(Name As String, Age As Int)\n";
            var (root, _) = B4xParser.Parse(src);
            var nodes = MapNodes(B4xParser.FlattenSubsAndTypes(root).ToArray());
            var skeleton = SkeletonGenerator.GenerateModuleSkeleton(src, nodes);

            Assert.Contains("Type Person(Name As String, Age As Int)", skeleton);
            Assert.Contains("End Type", skeleton);
        }

        [Fact]
        public void Multi_line_type_keeps_dim_fields()
        {
            var src = "Type Money\n\tDim amount As Double\nEnd Type\n";
            var (root, _) = B4xParser.Parse(src);
            var nodes = MapNodes(B4xParser.FlattenSubsAndTypes(root).ToArray());
            var skeleton = SkeletonGenerator.GenerateModuleSkeleton(src, nodes);

            Assert.Contains("Type Money", skeleton);
            Assert.Contains("\tDim amount As Double", skeleton);
            Assert.Contains("End Type", skeleton);
        }

        [Fact]
        public void Region_node_keeps_markers_and_content()
        {
            var src =
                "#Region Helpers\n" +
                "Sub Zap\n" +
                "\tLog(1)\n" +
                "End Sub\n" +
                "#End Region\n";
            var nodes = new[] { new Node { Kind = "Region", Name = "Helpers", StartLine = 1, EndLine = 5 } };
            var skeleton = SkeletonGenerator.GenerateModuleSkeleton(src, nodes);

            Assert.Contains("#Region Helpers", skeleton);
            Assert.Contains("Sub Zap", skeleton);
            Assert.Contains("#End Region", skeleton);
        }

        [Fact]
        public void Skeleton_is_capped_at_200_lines_with_marker()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 1; i <= 70; i++)
                sb.Append($"Sub S{i}\n\tLog({i})\nEnd Sub\n");

            var (root, _) = B4xParser.Parse(sb.ToString());
            var nodes = MapNodes(B4xParser.FlattenSubsAndTypes(root).ToArray());
            var skeleton = SkeletonGenerator.GenerateModuleSkeleton(sb.ToString(), nodes);

            Assert.Contains("'... (skeleton truncated)...", skeleton);
            Assert.True(skeleton.Split('\n').Length <= 205);
        }

        [Fact]
        public void Null_or_empty_nodes_passthrough_source()
        {
            Assert.Equal("Sub Foo\nEnd Sub\n", SkeletonGenerator.GenerateModuleSkeleton("Sub Foo\nEnd Sub\n", null));
            Assert.Equal("", SkeletonGenerator.GenerateModuleSkeleton("", null));
        }

        [Fact]
        public void Module_level_code_between_nodes_is_kept()
        {
            var src = "' B4A\n#VersionCode: 1\nSub Main\n\tLog(1)\nEnd Sub\n";
            var (root, _) = B4xParser.Parse(src);
            var nodes = MapNodes(B4xParser.FlattenSubsAndTypes(root).ToArray());
            var skeleton = SkeletonGenerator.GenerateModuleSkeleton(src, nodes);

            Assert.Contains("' B4A", skeleton);
            Assert.Contains("#VersionCode: 1", skeleton);
        }
    }
}