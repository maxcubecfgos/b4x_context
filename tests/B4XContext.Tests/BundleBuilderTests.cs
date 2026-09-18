using System.IO;
using System.Linq;
using B4XContext.Models;
using B4XContext.Services;
using Xunit;
using FileMode = B4XContext.Models.FileMode;

namespace B4XContext.Tests
{
    public class BundleBuilderTests
    {
        [Fact]
        public void BuildMarkdown_custom_mode_emits_granular_bundle()
        {
            var dir = Path.Combine(Path.GetTempPath(), "b4x_ctx_test_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "Mod.bas");
            File.WriteAllText(path, "'Header\nSub One\n\tLog(1)\nEnd Sub\n\nSub Two\n\tLog(2)\nEnd Sub\n");

            try
            {
                var pf = new ProjectFile(path)
                {
                    Kind = "bas",
                    Mode = FileMode.Custom,
                    Included = true
                };
                pf.Items.Add(new ModuleItem { Kind = ModuleItemKind.Sub, Name = "One", Signature = "Sub One", StartLine = 2, EndLine = 4 });
                pf.Items.Add(new ModuleItem { Kind = ModuleItemKind.Sub, Name = "Two", Signature = "Sub Two", StartLine = 6, EndLine = 8, IsSelected = true });

                var md = BundleBuilder.BuildMarkdown("", "", new[] { pf });

                Assert.Contains("### Mod.bas   (Custom)", md);
                Assert.Contains("2 items (1 selected)", md);
                Assert.Contains("Log(2)", md);
                Assert.DoesNotContain("Log(1)", md);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}