using System.IO;
using System.Linq;
using B4XContext.Models;
using B4XContext.Services;
using Xunit;
using FileMode = B4XContext.Models.FileMode;

namespace B4XContext.Tests
{
    public class ProjectScannerTests
    {
        [Fact]
        public void EndToEnd_scan_select_and_build_custom_bundle()
        {
            var dir = Path.Combine(Path.GetTempPath(), "b4x_ctx_e2e_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Demo.b4a"), "#ApplicationLabel: Demo\n#VersionCode: 1\n");
            File.WriteAllText(Path.Combine(dir, "Main.bas"),
                "'Main module\n" +
                "Sub Process_Globals\n" +
                "\tDim x As Int\n" +
                "End Sub\n" +
                "\n" +
                "Sub Activity_Create\n" +
                "\tLog(\"create\")\n" +
                "End Sub\n" +
                "\n" +
                "Sub Helper\n" +
                "\tLog(\"helper\")\n" +
                "End Sub\n");

            try
            {
                var files = ProjectScanner.ScanProject(dir);
                Assert.Equal(2, files.Count);

                var main = files.Single(f => f.Name == "Main.bas");
                main.Mode = FileMode.Custom;
                main.Included = true;
                var items = BundleBuilder.GetItems(main, File.ReadAllText(main.Path));
                items.Single(i => i.Name == "Activity_Create").IsSelected = true;

                var md = BundleBuilder.BuildMarkdown("ctx", "task", files);

                Assert.Contains("### Main.bas   (Custom)", md);
                Assert.Contains("3 items (1 selected)", md);
                Assert.Contains("Log(\"create\")", md);
                Assert.DoesNotContain("Log(\"helper\")", md);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}