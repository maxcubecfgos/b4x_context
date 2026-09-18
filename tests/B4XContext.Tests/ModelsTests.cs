using System.Linq;
using B4XContext.Models;
using Xunit;

namespace B4XContext.Tests
{
    public class ModelsTests
    {
        [Fact]
        public void ResetCustom_clears_selection_and_returns_to_skeleton()
        {
            var pf = new ProjectFile(@"C:\proj\Main.bas")
            {
                Mode = FileMode.Custom,
                Included = true,
                Kind = "bas"
            };
            pf.Items.Add(new ModuleItem { Kind = ModuleItemKind.Sub, Name = "Foo", IsSelected = true });

            pf.ResetCustom();

            Assert.False(pf.Items.Single().IsSelected);
            Assert.Equal(FileMode.Skeleton, pf.Mode);
        }

        [Fact]
        public void Item_groups_are_four_distinct_labels()
        {
            Assert.Equal("SUBS", new ModuleItem { Kind = ModuleItemKind.Sub }.Group);
            Assert.Equal("VARIABLES", new ModuleItem { Kind = ModuleItemKind.Variable }.Group);
            Assert.Equal("TYPES", new ModuleItem { Kind = ModuleItemKind.Type }.Group);
            Assert.Equal("REGIONS", new ModuleItem { Kind = ModuleItemKind.Region }.Group);
        }

        [Fact]
        public void Filtered_views_and_flag_properties_reflect_items()
        {
            var pf = new ProjectFile(@"C:\proj\Main.bas") { Kind = "bas" };
            pf.Items.Add(new ModuleItem { Kind = ModuleItemKind.Sub, Name = "Foo" });
            pf.Items.Add(new ModuleItem { Kind = ModuleItemKind.Variable, Name = "x" });

            Assert.Single(pf.ItemsSubs);
            Assert.Single(pf.ItemsVariables);
            Assert.Empty(pf.ItemsTypes);
            Assert.Empty(pf.ItemsRegions);
            Assert.True(pf.HasSubs);
            Assert.True(pf.HasVariables);
            Assert.False(pf.HasTypes);
            Assert.False(pf.HasRegions);
        }

        [Fact]
        public void IsCodeFile_covers_code_extensions_only()
        {
            Assert.True(new ProjectFile("x.bas") { Kind = "bas" }.IsCodeFile);
            Assert.True(new ProjectFile("x.b4a") { Kind = "b4a" }.IsCodeFile);
            Assert.True(new ProjectFile("x.b4j") { Kind = "b4j" }.IsCodeFile);
            Assert.True(new ProjectFile("x.b4i") { Kind = "b4i" }.IsCodeFile);
            Assert.False(new ProjectFile("x.bal") { Kind = "bal" }.IsCodeFile);
            Assert.False(new ProjectFile("x.bjl") { Kind = "bjl" }.IsCodeFile);
            Assert.False(new ProjectFile("x.bil") { Kind = "bil" }.IsCodeFile);
        }
    }
}