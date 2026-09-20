using System.Linq;
using B4XContext.Engine;
using B4XContext.Models;
using Xunit;

namespace B4XContext.Tests
{
    public class B4xGranularBuilderTests
    {
        private const string Sample =
            "'Header comment\n" +
            "Sub Process_Globals\n" +
            "\t Dim counter As Int\n" +
            "End Sub\n" +
            "\n" +
            "Sub Foo(x As Int)\n" +
            "\tLog(x)\n" +
            "End Sub\n" +
            "\n" +
            "Private Sub Bar\n" +
            "\tLog(\"bar\")\n" +
            "End Sub\n" +
            "\n" +
            "#Region R1\n" +
            "Sub Inside\n" +
            "\tLog(\"in\")\n" +
            "End Sub\n" +
            "#End Region\n";

        private static (string code, int chars) BuildWithSelection(bool selectFoo, bool selectRegion)
        {
            var (root, _) = B4xParser.Parse(Sample);
            var items = B4xItemExtractor.ExtractItems(Sample, root);
            if (selectFoo) items.First(i => i.Name == "Foo").IsSelected = true;
            if (selectRegion) items.First(i => i.Kind == ModuleItemKind.Region && i.Name == "R1").IsSelected = true;
            return B4xGranularBuilder.BuildCustom(Sample, items, "Mod.bas");
        }

        [Fact]
        public void BuildCustom_emits_preamble_index_and_only_selected_items()
        {
            var (code, chars) = BuildWithSelection(selectFoo: true, selectRegion: false);

            Assert.Contains("'Header comment", code);
            Assert.DoesNotContain("Dim counter As Int", code);
            Assert.Contains("### Index of 'Mod.bas': 5 items (1 selected)", code);
            Assert.Contains("- [x] Sub Foo(x As Int) — line 6", code);
            Assert.DoesNotContain("- [ ]", code);
            Assert.Contains("## SELECTED ITEMS", code);
            Assert.Contains("Log(x)", code);
            Assert.DoesNotContain("Log(\"bar\")", code);
            Assert.DoesNotContain("Log(\"in\")", code);
            Assert.True(chars > 0);
        }

        [Fact]
        public void Region_selection_suppresses_separate_emission_of_contained_sub()
        {
            var (root, _) = B4xParser.Parse(Sample);
            var items = B4xItemExtractor.ExtractItems(Sample, root);
            items.First(i => i.Kind == ModuleItemKind.Region && i.Name == "R1").IsSelected = true;
            items.First(i => i.Name == "Inside").IsSelected = true;

            var (code, _) = B4xGranularBuilder.BuildCustom(Sample, items, "Mod.bas");

            Assert.Contains("- [x] #Region R1 — line 14", code);
            Assert.Contains("- [x] Sub Inside — line 15 (in R1)", code);
            var afterSelected = code.Split("## SELECTED ITEMS")[1];
            int insideCount = afterSelected.Split(new[] { "Sub Inside" }, System.StringSplitOptions.None).Length - 1;
            Assert.Equal(1, insideCount);
            Assert.Contains("Log(\"in\")", afterSelected);
        }

        [Fact]
        public void BuildCustom_globals_block_is_single_selectable_item()
        {
            var (root, _) = B4xParser.Parse(Sample);
            var items = B4xItemExtractor.ExtractItems(Sample, root);
            items.First(i => i.Name == "Process_Globals").IsSelected = true;

            var (code, _) = B4xGranularBuilder.BuildCustom(Sample, items, "Mod.bas");

            Assert.Contains("- [x] Sub Process_Globals — line 2", code);
            Assert.Contains("Dim counter As Int", code);
            Assert.Contains("End Sub", code);
            Assert.DoesNotContain("Log(x)", code);
        }

        [Fact]
        public void BuildCustom_with_no_selection_has_no_index_and_minimal_output()
        {
            var (code, _) = BuildWithSelection(selectFoo: false, selectRegion: false);

            Assert.DoesNotContain("## SELECTED ITEMS", code);
            Assert.DoesNotContain("Index of", code);
            Assert.DoesNotContain("[ ]", code);
            Assert.DoesNotContain("Sub Foo", code);
            Assert.Contains("<- EOF module ->", code);
            Assert.True(code.Length < 500);
        }
    }
}