using System.Linq;
using B4XContext.Engine;
using B4XContext.Models;
using Xunit;

namespace B4XContext.Tests
{
    public class B4xItemExtractorTests
    {
        private const string Sample =
            "'Code module for testing\n" +
            "#Region Project Attributes\n" +
            "\t#If B4A\n" +
            "#End Region\n" +
            "\n" +
            "Sub Process_Globals\n" +
            "\tDim appName As String = \"Demo\"\n" +
            "\tDim counter As Int\n" +
            "\tPrivate Dim secret As Long\n" +
            "End Sub\n" +
            "\n" +
            "Type Person(Name As String, Age As Int)\n" +
            "\n" +
            "Sub Foo(x As Int) As Boolean\n" +
            "\tLog(x)\n" +
            "End Sub\n" +
            "\n" +
            "Private Sub Bar\n" +
            "\tLog(\"bar\")\n" +
            "End Sub\n" +
            "\n" +
            "#Region Helpers\n" +
            "Sub Zap\n" +
            "\tLog(\"zap\")\n" +
            "End Sub\n" +
            "#End Region\n";

        [Fact]
        public void ExtractItems_parses_subs_vars_types_and_regions()
        {
            var (root, _) = B4xParser.Parse(Sample);

            var items = B4xItemExtractor.ExtractItems(Sample, root);

            Assert.Equal(7, items.Count);

            var subs = items.Where(i => i.Kind == ModuleItemKind.Sub).ToList();
            Assert.Equal(4, subs.Count);
            Assert.Contains(subs, s => s.Name == "Foo" && s.Signature == "Sub Foo(x As Int) As Boolean");
            Assert.Contains(subs, s => s.Name == "Bar" && s.Container == "");
            Assert.Contains(subs, s => s.Name == "Zap" && s.Container == "Helpers");
            Assert.Contains(subs, s => s.Name == "Process_Globals" && s.Signature == "Sub Process_Globals" && s.StartLine == 6 && s.EndLine == 10);

            var vars = items.Where(i => i.Kind == ModuleItemKind.Variable).ToList();
            Assert.Empty(vars);

            var types = items.Where(i => i.Kind == ModuleItemKind.Type).ToList();
            var t = Assert.Single(types);
            Assert.Equal("Person", t.Name);
            Assert.Equal("Type Person(Name As String, Age As Int)", t.Signature);

            var regions = items.Where(i => i.Kind == ModuleItemKind.Region).ToList();
            Assert.Equal(2, regions.Count);
            Assert.Contains(regions, r => r.Name == "Helpers");
        }

        [Fact]
        public void ExtractItems_handles_null_root_gracefully()
        {
            var items = B4xItemExtractor.ExtractItems("Sub Foo\nEnd Sub", null);
            Assert.Empty(items);
        }
    }
}